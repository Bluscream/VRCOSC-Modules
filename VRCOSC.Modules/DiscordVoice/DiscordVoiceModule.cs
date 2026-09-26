// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0),
// DISCORDOSC.cs. Attribution and the upstream licence notice are in README.md.
//
// Talks to the running Discord client over its local RPC/IPC socket: mute, deafen, join
// channels, query voice settings, and mirror voice events into OSC parameters and ChatBox
// events.

using System.Text.Json;
using Bluscream.Modules.DiscordVoice.Providers;
using Bluscream.Modules.Utilities;
using Bluscream.Modules.DiscordVoice.Rpc;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.DiscordVoice;

[ModuleTitle("Discord Voice")]
[ModuleDescription("Mute, deafen and voice-channel state for the running Discord client over its local RPC socket. Fork of Yeusepe's DiscordOSC.")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules/tree/beta/VRCOSC.Modules/DiscordVoice")]
public sealed partial class DiscordVoiceModule : Module
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);

    private DiscordIpcClient? _client;
    private static readonly TimeSpan ReconnectInterval = TimeSpan.FromSeconds(10);
    private bool _rpcReady;
    private bool _rpcEnabled;
    private bool _connecting;
    private readonly List<IVoiceProvider> _providers = [];
    private CancellationTokenSource? _lifetime;
    private string _clientId = string.Empty;

    private string _lastGuildId = string.Empty;
    private string _lastChannelId = string.Empty;
    private bool _autoUpdateDefaults;
    private bool _polling;

    protected override void OnPreLoad()
    {
        RegisterParameters();

        CreateTextBox(DiscordVoiceSetting.DefaultGuildId, "Default Guild ID", "Guild ID used for guild-scoped subscriptions (GUILD_STATUS).", string.Empty);
        CreateTextBox(DiscordVoiceSetting.DefaultChannelId, "Default Channel ID", "Channel ID used for channel-scoped subscriptions (VOICE_STATE_*, SPEAKING_*, MESSAGE_*).", string.Empty);
        CreateToggle(DiscordVoiceSetting.AutoUpdateDefaults, "Auto Update Defaults", "Update the default guild and channel whenever you join a voice channel.", false);

        CreateSlider(DiscordVoiceSetting.MaxSpeakingNames, "Max Speaking Names", "How many speakers the Speaking variable lists before collapsing the rest into \"+N\". 0 = unlimited.", 3, 0, 10);
        CreateTextBox(DiscordVoiceSetting.SpeakingHoldMs, "Speaking Hold (ms)", "How long a speaker stays listed after they stop talking, so short pauses do not flicker.", 300);

        CreateDropdown(DiscordVoiceSetting.VoiceSource, "Voice Source", "Auto uses Discord RPC when it is authenticated and falls back to an Equicord plugin bridge otherwise. See README for what each fallback can and cannot provide.", VoiceSource.Auto);
        CreateTextBox(DiscordVoiceSetting.OrbolayPort, "OrbolayBridge Port", "Port the Equicord OrbolayBridge plugin connects to (its 'Port to connect to' setting).", OrbolayBridgeProvider.DefaultPort);
        CreateTextBox(DiscordVoiceSetting.DevCompanionPort, "DevCompanion MCP Port", "Port of the devcompanionExtended plugin's in-app MCP HTTP server.", DevCompanionProvider.DefaultPort);
        CreateGroup("Fallback Sources", "Used when Discord RPC is unavailable (no application credentials, Vesktop/Equibop, or Wine without the IPC bridge).", DiscordVoiceSetting.VoiceSource, DiscordVoiceSetting.OrbolayPort, DiscordVoiceSetting.DevCompanionPort);

        CreateTextBox(DiscordVoiceSetting.ClientId, "Client ID", "Client ID of your Discord application (Developer Portal, OAuth2 tab). Required for RPC.", string.Empty);
        CreatePasswordTextBox(DiscordVoiceSetting.ClientSecret, "Client Secret", "Client secret of the same application. Required for RPC.", string.Empty);
        CreateTextBox(DiscordVoiceSetting.IpcBridgePort, "Wine IPC Bridge Port", "Linux/Wine only: the module deploys vrcosc_discord_ipc_bridge.sh to ~/.local/bin, which forwards 127.0.0.1:<port> to Discord's native discord-ipc socket with socat, because Wine's named pipes never reach it.", DiscordIpcBridge.DefaultPort);
        CreateGroup("Discord Application", "OAuth2 credentials of the application used for the RPC connection.", DiscordVoiceSetting.ClientId, DiscordVoiceSetting.ClientSecret, DiscordVoiceSetting.IpcBridgePort);
    }

    protected override void OnPostLoad()
    {
        RegisterChatBox();
        RegisterVoiceVariables();
    }

    protected override async Task<bool> OnModuleStart()
    {
        _lifetime = new CancellationTokenSource();
        _autoUpdateDefaults = GetSettingValue<bool>(DiscordVoiceSetting.AutoUpdateDefaults);
        _lastGuildId = GetSettingValue<string>(DiscordVoiceSetting.DefaultGuildId) ?? string.Empty;
        _lastChannelId = GetSettingValue<string>(DiscordVoiceSetting.DefaultChannelId) ?? string.Empty;
        _voice.SpeakingHold = TimeSpan.FromMilliseconds(Math.Max(0, GetSettingValue<int>(DiscordVoiceSetting.SpeakingHoldMs)));
        ResetVoice();
        await StartProvidersAsync(_lifetime.Token).ConfigureAwait(false);

        var source = GetSettingValue<VoiceSource>(DiscordVoiceSetting.VoiceSource);
        _rpcEnabled = source is VoiceSource.Auto or VoiceSource.Rpc && HasRpcCredentials();
        if (!_rpcEnabled && _providers.Count == 0)
        {
            Log("Neither Discord RPC nor a fallback source is configured; stopping.");
            return false;
        }

        if (_rpcEnabled && !await ConnectRpcAsync(_lifetime.Token).ConfigureAwait(false))
            Log($"Will retry the RPC connection every {ReconnectInterval.TotalSeconds:0}s.");
        return true;
    }

    private bool HasRpcCredentials()
    {
        var clientId = GetSettingValue<string>(DiscordVoiceSetting.ClientId)?.Trim() ?? string.Empty;
        var clientSecret = GetSettingValue<string>(DiscordVoiceSetting.ClientSecret)?.Trim() ?? string.Empty;
        if (clientId.Length > 0 && clientSecret.Length > 0) return true;
        Log("RPC disabled: set a Discord application Client ID and Client Secret in the module settings (https://discord.com/developers/applications, OAuth2 tab).");
        return false;
    }

    /// <summary>Re-establishes RPC after Discord restarts or was not running at module start.</summary>
    [ModuleUpdate(ModuleUpdateMode.Custom, false, 10000)]
    private void ReconnectTick()
    {
        if (!_rpcEnabled || _rpcReady || _connecting) return;
        var ct = _lifetime?.Token ?? CancellationToken.None;
        if (ct.IsCancellationRequested) return;
        _connecting = true;
        _ = ReconnectAsync(ct);
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        try
        {
            DisposeClient();
            await ConnectRpcAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _connecting = false;
        }
    }

    private void DisposeClient()
    {
        var client = _client;
        _client = null;
        if (client is null) return;
        client.EventReceived -= HandleRpcEvent;
        client.Disconnected -= OnClientDisconnected;
        client.Dispose();
    }

    private async Task StartProvidersAsync(CancellationToken ct)
    {
        var source = GetSettingValue<VoiceSource>(DiscordVoiceSetting.VoiceSource);
        if (source is VoiceSource.Auto or VoiceSource.OrbolayBridge)
            _providers.Add(new OrbolayBridgeProvider(GetSettingValue<int>(DiscordVoiceSetting.OrbolayPort)));
        if (source is VoiceSource.Auto or VoiceSource.DevCompanion)
            _providers.Add(new DevCompanionProvider(GetSettingValue<int>(DiscordVoiceSetting.DevCompanionPort)));

        foreach (var provider in _providers)
            await provider.StartAsync(Log, ct).ConfigureAwait(false);
    }

    /// <summary>Token, IPC connect, handshake, authenticate, subscriptions. False (with a log line) on any failure.</summary>
    private async Task<bool> ConnectRpcAsync(CancellationToken ct)
    {
        var clientId = GetSettingValue<string>(DiscordVoiceSetting.ClientId)?.Trim() ?? string.Empty;
        var clientSecret = GetSettingValue<string>(DiscordVoiceSetting.ClientSecret)?.Trim() ?? string.Empty;
        _clientId = clientId;

        var ok = false;
        try
        {
            var token = await DiscordAuth.FetchAccessTokenAsync(clientId, clientSecret, ct).ConfigureAwait(false);
            LogDebug("Access token retrieved.");

            var client = await ConnectAsync(ct).ConfigureAwait(false);
            if (client is null)
            {
                Log("Could not reach a Discord IPC pipe (discord-ipc-0..9). Is Discord running?");
                return false;
            }

            client.EventReceived += HandleRpcEvent;
            client.Disconnected += OnClientDisconnected;
            _client = client;

            using (await client.HandshakeAsync(clientId, CommandTimeout, ct).ConfigureAwait(false)) { }
            LogDebug("Handshake complete.");

            using (var auth = await client.SendAsync(Payload.Authenticate(token), CommandTimeout, ct).ConfigureAwait(false))
            {
                if (auth.IsError)
                {
                    Log($"Authentication failed: {auth.ErrorCode} {auth.ErrorMessage}");
                    return false;
                }
            }
            Log($"Authenticated with Discord over {client.Transport}.");

            await SubscribeDefaultsAsync(client, ct).ConfigureAwait(false);
            await SendAndLogAsync(client, Payload.GetVoiceSettings(), ct).ConfigureAwait(false);
            await SendAndLogAsync(client, Payload.GetSelectedVoiceChannel(), ct).ConfigureAwait(false);
            _rpcReady = true;
            SendParameter(DiscordVoiceParameter.Ready, true);
            SetVariableValue(DiscordVoiceVariable.Ready, true);
            ok = true;
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is TimeoutException || ex is InvalidOperationException || ex is System.IO.IOException || ex is ObjectDisposedException || ex is JsonException)
        {
            Log($"Discord RPC connection failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (!ok) DisposeClient();
        }
    }

    protected override Task OnModuleStop()
    {
        _lifetime?.Cancel();
        _rpcReady = false;
        _rpcEnabled = false;
        foreach (var provider in _providers) provider.Dispose();
        _providers.Clear();
        DisposeClient();
        _lifetime?.Dispose();
        _lifetime = null;
        SendParameter(DiscordVoiceParameter.Ready, false);
        SetVariableValue(DiscordVoiceVariable.Ready, false);
        ResetVoice();
        return Task.CompletedTask;
    }

    /// <summary>Named pipes first (Windows, or Wine with a pipe bridge), then the socat TCP bridge on Wine.</summary>
    private async Task<DiscordIpcClient?> ConnectAsync(CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var client = await TryConnectAsync(c => c.ConnectPipeAsync($"discord-ipc-{i}", ConnectTimeout, ct), $"discord-ipc-{i}").ConfigureAwait(false);
            if (client is not null) return client;
        }

        if (!LinuxUtils.IsWineOnLinux) return null;
        var port = GetSettingValue<int>(DiscordVoiceSetting.IpcBridgePort);
        if (!await DiscordIpcBridge.EnsureRunningAsync(port, Log, ct).ConfigureAwait(false)) return null;
        return await TryConnectAsync(c => c.ConnectTcpAsync("127.0.0.1", port, ConnectTimeout, ct), $"tcp bridge :{port}").ConfigureAwait(false);
    }

    private async Task<DiscordIpcClient?> TryConnectAsync(Func<DiscordIpcClient, Task> connect, string what)
    {
        var client = new DiscordIpcClient();
        try
        {
            await connect(client).ConfigureAwait(false);
            return client;
        }
        catch (Exception ex) when (ex is TimeoutException || ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is System.Net.Sockets.SocketException || ex is OperationCanceledException)
        {
            LogDebug($"{what}: {ex.Message}");
            client.Dispose();
            return null;
        }
    }

    private async Task SubscribeDefaultsAsync(DiscordIpcClient client, CancellationToken ct)
    {
        foreach (var evt in new[] { "VOICE_CHANNEL_SELECT", "VOICE_SETTINGS_UPDATE", "VOICE_CONNECTION_STATUS", "GUILD_CREATE", "CHANNEL_CREATE", "NOTIFICATION_CREATE", "ACTIVITY_JOIN", "ACTIVITY_SPECTATE", "ACTIVITY_JOIN_REQUEST" })
            await SubscribeAsync(client, evt, null, ct).ConfigureAwait(false);

        if (_lastGuildId.Length > 0)
            await SubscribeAsync(client, "GUILD_STATUS", new { guild_id = _lastGuildId }, ct).ConfigureAwait(false);

        if (_lastChannelId.Length > 0)
            await SubscribeChannelEventsAsync(client, _lastChannelId, ct).ConfigureAwait(false);
    }

    private async Task SubscribeChannelEventsAsync(DiscordIpcClient client, string channelId, CancellationToken ct)
    {
        var args = new { channel_id = channelId };
        foreach (var evt in new[] { "VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP", "MESSAGE_CREATE", "MESSAGE_UPDATE", "MESSAGE_DELETE" })
            await SubscribeAsync(client, evt, args, ct).ConfigureAwait(false);
    }

    private async Task SubscribeAsync(DiscordIpcClient client, string evt, object? args, CancellationToken ct)
    {
        try
        {
            using var response = await client.SendAsync(Payload.Subscribe(evt, args), CommandTimeout, ct).ConfigureAwait(false);
            if (response.IsError) Log($"Subscribe {evt} refused: {response.ErrorCode} {response.ErrorMessage}");
        }
        catch (Exception ex) when (ex is TimeoutException || ex is System.IO.IOException || ex is InvalidOperationException || ex is ObjectDisposedException)
        {
            Log($"Subscribe {evt}: {ex.Message}");
        }
    }

    private void OnClientDisconnected(Exception? cause)
    {
        _rpcReady = false;
        _voice.ClearChannel();
        _trackedChannelId = string.Empty;
        Log(cause is null ? "Discord closed the RPC connection." : $"Discord RPC connection lost: {cause.Message}");
        SendParameter(DiscordVoiceParameter.Ready, false);
        SetVariableValue(DiscordVoiceVariable.Ready, false);
    }

    /// <summary>Fire-and-forget command from the OSC thread; failures are logged, never thrown.</summary>
    private void Send(RpcCommand command)
    {
        var client = _client;
        var ct = _lifetime?.Token ?? CancellationToken.None;
        if (client is null || !client.IsConnected)
        {
            LogDebug($"{command.Cmd} ignored: not connected.");
            return;
        }

        _ = SendAndLogAsync(client, command, ct);
    }

    private async Task SendAndLogAsync(DiscordIpcClient client, RpcCommand command, CancellationToken ct)
    {
        try
        {
            using var response = await client.SendAsync(command, CommandTimeout, ct).ConfigureAwait(false);
            if (response.IsError) Log($"{command.Cmd} failed: {response.ErrorCode} {response.ErrorMessage}");
            else ApplyCommandResponse(command.Cmd, response.Data);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is TimeoutException || ex is System.IO.IOException || ex is InvalidOperationException || ex is JsonException || ex is ObjectDisposedException)
        {
            LogDebug($"{command.Cmd}: {ex.Message}");
        }
    }

    /// <summary>Responses to the request-style parameters land here (GET_GUILDS -> GuildCount and so on).</summary>
    private void ApplyCommandResponse(string cmd, JsonElement data)
    {
        switch (cmd)
        {
            case "GET_GUILDS" when data.TryGetProperty("guilds", out var guilds):
                SetCount(DiscordVoiceParameter.GuildCount, DiscordVoiceVariable.GuildCount, guilds.GetArrayLength());
                break;
            case "GET_CHANNELS" when data.TryGetProperty("channels", out var channels):
                SetCount(DiscordVoiceParameter.ChannelCount, DiscordVoiceVariable.ChannelCount, channels.GetArrayLength());
                break;
            case "GET_SELECTED_VOICE_CHANNEL":
                var id = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                _lastChannelId = id;
                SetCount(DiscordVoiceParameter.SelectedVoiceChannelId, DiscordVoiceVariable.SelectedVoiceChannelId, SnowflakeToInt(id));
                if (id != _trackedChannelId) OnVoiceChannelChanged(id);
                break;
            case "GET_CHANNEL":
                if (Nested(data, "id") is { } channelId && channelId == _trackedChannelId) ApplyChannel(channelId, data);
                if (data.TryGetProperty("voice_states", out var states))
                    SetCount(DiscordVoiceParameter.ChannelUserCount, DiscordVoiceVariable.ChannelUserCount, states.GetArrayLength());
                if (data.TryGetProperty("type", out var type) && type.TryGetInt32(out var typeValue))
                    SetCount(DiscordVoiceParameter.ChannelType, DiscordVoiceVariable.ChannelType, typeValue);
                break;
            case "GET_GUILD":
                var hasIcon = data.TryGetProperty("icon_url", out var icon) && icon.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(icon.GetString());
                SendParameter(DiscordVoiceParameter.GuildHasIcon, hasIcon);
                break;
            case "GET_VOICE_SETTINGS":
            case "SET_VOICE_SETTINGS":
                ApplyVoiceSettings(data);
                OnVoiceSettings(data);
                break;
        }
    }

    private void SetCount(DiscordVoiceParameter parameter, DiscordVoiceVariable variable, int value)
    {
        SendParameter(parameter, value);
        SetVariableValue(variable, value);
    }

    private void ApplyVoiceSettings(JsonElement data)
    {
        if (data.TryGetProperty("input", out var input) && input.TryGetProperty("volume", out var inVol) && inVol.TryGetSingle(out var inputVolume))
        {
            SendParameter(DiscordVoiceParameter.InputVolume, inputVolume);
            SetVariableValue(DiscordVoiceVariable.InputVolume, inputVolume);
        }
        if (data.TryGetProperty("output", out var output) && output.TryGetProperty("volume", out var outVol) && outVol.TryGetSingle(out var outputVolume))
        {
            SendParameter(DiscordVoiceParameter.OutputVolume, outputVolume);
            SetVariableValue(DiscordVoiceVariable.OutputVolume, outputVolume);
        }
    }

    /// <summary>Snowflakes do not fit an OSC int; the low 32 bits are what DiscordOSC exposed, kept for compatibility.</summary>
    private static int SnowflakeToInt(string? snowflake) => long.TryParse(snowflake, out var id) ? unchecked((int)id) : 0;

    /// <summary>
    /// DiscordOSC polled GET_GUILDS, GET_CHANNELS, GET_SELECTED_VOICE_CHANNEL, GET_CHANNEL and
    /// GET_VOICE_SETTINGS on every ChatBox tick. Kept, but non-blocking and never overlapping.
    /// </summary>
    [ModuleUpdate(ModuleUpdateMode.ChatBox)]
    private void ChatBoxUpdate()
    {
        var client = _client;
        if (_polling || client is null || !client.IsConnected) return;
        _polling = true;
        _ = PollAsync(client, _lifetime?.Token ?? CancellationToken.None);
    }

    private async Task PollAsync(DiscordIpcClient client, CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested) return;
            await SendAndLogAsync(client, Payload.GetGuilds(), ct).ConfigureAwait(false);
            if (_lastGuildId.Length > 0) await SendAndLogAsync(client, Payload.GetChannels(_lastGuildId), ct).ConfigureAwait(false);
            await SendAndLogAsync(client, Payload.GetSelectedVoiceChannel(), ct).ConfigureAwait(false);
            if (_lastChannelId.Length > 0) await SendAndLogAsync(client, Payload.GetChannel(_lastChannelId), ct).ConfigureAwait(false);
            await SendAndLogAsync(client, Payload.GetVoiceSettings(), ct).ConfigureAwait(false);
        }
        finally
        {
            _polling = false;
        }
    }
}
