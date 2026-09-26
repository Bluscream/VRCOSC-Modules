// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox-compatible voice variables (discord_channel, discord_speaking,
// discord_mute_state, discord_muted, discord_deafened, discord_users) fed from the RPC
// VOICE_CHANNEL_SELECT / VOICE_STATE_* / SPEAKING_* / VOICE_SETTINGS_UPDATE events.

using System.Text.Json;
using Bluscream.Modules.DiscordVoice.Providers;
using Bluscream.Modules.DiscordVoice.Rpc;
using Bluscream.Modules.DiscordVoice.Voice;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.DiscordVoice;

public sealed partial class DiscordVoiceModule
{
    private static readonly string[] ChannelScopedEvents = ["VOICE_STATE_CREATE", "VOICE_STATE_UPDATE", "VOICE_STATE_DELETE", "SPEAKING_START", "SPEAKING_STOP"];

    /// <summary>ChatBox variable keys; these spellings are what MagicChatbox layouts reference.</summary>
    private const string VarChannel = "discord_channel";
    private const string VarSpeaking = "discord_speaking";
    private const string VarMuteState = "discord_mute_state";
    private const string VarMuted = "discord_muted";
    private const string VarDeafened = "discord_deafened";
    private const string VarUsers = "discord_users";

    private readonly VoiceStateTracker _voice = new();
    private string _trackedChannelId = string.Empty;
    private VoiceSnapshot _lastPublished;
    private string _lastSpeakingText = string.Empty;
    private string _lastSource = string.Empty;

    private void RegisterVoiceVariables()
    {
        var channel = CreateVariable<string>(VarChannel, "Voice Channel");
        var speaking = CreateVariable<string>(VarSpeaking, "Speaking");
        var muteState = CreateVariable<string>(VarMuteState, "Mute State");
        CreateVariable<bool>(VarMuted, "Muted");
        CreateVariable<bool>(VarDeafened, "Deafened");
        var users = CreateVariable<int>(VarUsers, "Users In Channel");

        CreateState(DiscordVoiceState.InVoice, "In Voice", "\U0001F50A {0} ({3})\n{1}\n{2}", Vars(channel, speaking, muteState, users));
        CreateState(DiscordVoiceState.NotInVoice, "Not In Voice", string.Empty);
        CreateState(DiscordVoiceState.Disconnected, "Disconnected", string.Empty);
    }

    private void ResetVoice()
    {
        _voice.ClearChannel();
        _voice.SetSelf(false, false);
        _trackedChannelId = string.Empty;
        PublishVoice(force: true);
    }

    /// <summary>Renders the tracker into variables; runs on the update thread so speaking holds expire on time.</summary>
    [ModuleUpdate(ModuleUpdateMode.Custom, true, 250)]
    private void VoiceTick() => PublishVoice(force: false);

    /// <summary>The tracker the variables are rendered from: RPC while authenticated, else the first live fallback.</summary>
    private (VoiceStateTracker Tracker, string Source) ActiveVoiceSource()
    {
        if (_rpcReady) return (_voice, "RPC");
        foreach (var provider in _providers)
        {
            if (provider.IsAvailable) return (provider.Tracker, provider.Name);
        }
        return (_voice, "none");
    }

    private void PublishVoice(bool force)
    {
        var (tracker, source) = ActiveVoiceSource();
        if (source != _lastSource)
        {
            _lastSource = source;
            Log($"Voice variables now come from: {source}");
            force = true;
        }
        var snapshot = tracker.Snapshot(DateTime.UtcNow);
        var speakingText = snapshot.SpeakingText(GetSettingValue<int>(DiscordVoiceSetting.MaxSpeakingNames));
        var changed = force
                      || snapshot.ChannelId != _lastPublished.ChannelId
                      || snapshot.ChannelName != _lastPublished.ChannelName
                      || snapshot.UserCount != _lastPublished.UserCount
                      || snapshot.Muted != _lastPublished.Muted
                      || snapshot.Deafened != _lastPublished.Deafened
                      || speakingText != _lastSpeakingText;
        if (!changed) return;

        _lastPublished = snapshot;
        _lastSpeakingText = speakingText;

        SetVariableValue(VarChannel, snapshot.ChannelName);
        SetVariableValue(VarSpeaking, speakingText);
        SetVariableValue(VarMuteState, snapshot.MuteState);
        SetVariableValue(VarMuted, snapshot.Muted);
        SetVariableValue(VarDeafened, snapshot.Deafened);
        SetVariableValue(VarUsers, snapshot.UserCount);

        SendParameter(DiscordVoiceParameter.Muted, snapshot.Muted);
        SendParameter(DiscordVoiceParameter.Deafened, snapshot.Deafened);
        SendParameter(DiscordVoiceParameter.Mute, snapshot.Muted);
        SendParameter(DiscordVoiceParameter.Deafen, snapshot.Deafened);

        ChangeState(source == "none" ? DiscordVoiceState.Disconnected : snapshot.InVoice ? DiscordVoiceState.InVoice : DiscordVoiceState.NotInVoice);
    }

    /// <summary>Mute/deafen requests go over RPC when authenticated, else to the live fallback provider.</summary>
    private void SetSelfVoiceFlag(bool deafen, bool value)
    {
        if (_rpcReady)
        {
            Send(deafen ? Payload.SetDeafenOnly(value) : Payload.SetMuteOnly(value));
            return;
        }
        var provider = _providers.FirstOrDefault(p => p.IsAvailable);
        if (provider is null)
        {
            LogDebug($"{(deafen ? "Deafen" : "Mute")} ignored: no voice source is connected.");
            return;
        }
        _ = SetSelfViaProviderAsync(provider, deafen, value, _lifetime?.Token ?? CancellationToken.None);
    }

    private async Task SetSelfViaProviderAsync(IVoiceProvider provider, bool deafen, bool value, CancellationToken ct)
    {
        try
        {
            var ok = deafen ? await provider.SetDeafenAsync(value, ct).ConfigureAwait(false) : await provider.SetMuteAsync(value, ct).ConfigureAwait(false);
            if (!ok) Log($"{provider.Name} could not set {(deafen ? "deafen" : "mute")}.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is System.IO.IOException || ex is InvalidOperationException || ex is JsonException)
        {
            Log($"{provider.Name} set {(deafen ? "deafen" : "mute")} failed: {ex.Message}");
        }
    }

    // ── RPC event feed ─────────────────────────────────────────────────────────────

    /// <summary>VOICE_CHANNEL_SELECT: switch the tracked channel, or clear it when channel_id is null.</summary>
    private void OnVoiceChannelChanged(string channelId)
    {
        var client = _client;
        if (client is null) return;
        _ = TrackChannelAsync(client, channelId, _lifetime?.Token ?? CancellationToken.None);
    }

    private async Task TrackChannelAsync(DiscordIpcClient client, string channelId, CancellationToken ct)
    {
        try
        {
            var previous = _trackedChannelId;
            if (previous.Length > 0 && previous != channelId)
            {
                foreach (var evt in ChannelScopedEvents)
                    await SendAndLogAsync(client, Payload.Unsubscribe(evt, new { channel_id = previous }), ct).ConfigureAwait(false);
            }

            if (channelId.Length == 0)
            {
                _trackedChannelId = string.Empty;
                _voice.ClearChannel();
                return;
            }

            _trackedChannelId = channelId;
            using (var channel = await client.SendAsync(Payload.GetChannel(channelId), CommandTimeout, ct).ConfigureAwait(false))
            {
                if (channel.IsError) Log($"GET_CHANNEL {channelId} failed: {channel.ErrorCode} {channel.ErrorMessage}");
                else ApplyChannel(channelId, channel.Data);
            }

            if (previous != channelId)
            {
                foreach (var evt in ChannelScopedEvents)
                    await SubscribeAsync(client, evt, new { channel_id = channelId }, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is TimeoutException || ex is System.IO.IOException || ex is InvalidOperationException || ex is JsonException)
        {
            Log($"Tracking voice channel {channelId} failed: {ex.Message}");
        }
    }

    /// <summary>GET_CHANNEL response: name plus the current voice_states (member list).</summary>
    private void ApplyChannel(string channelId, JsonElement data)
    {
        var name = Nested(data, "name") ?? string.Empty;
        _voice.SetChannel(channelId, name);

        if (data.TryGetProperty("voice_states", out var states) && states.ValueKind == JsonValueKind.Array)
        {
            _voice.ReplaceMembers(states.EnumerateArray()
                .Select(s => (UserId: Nested(s, "user", "id"), Name: DisplayName(s)))
                .Where(m => m.UserId is not null)
                .Select(m => (m.UserId ?? string.Empty, m.Name)));
        }
    }

    /// <summary>Server nickname, else global display name, else username - what Discord itself shows.</summary>
    private static string DisplayName(JsonElement voiceState)
        => Nested(voiceState, "nick") is { Length: > 0 } nick ? nick
            : Nested(voiceState, "user", "global_name") is { Length: > 0 } global ? global
            : Nested(voiceState, "user", "username") ?? Nested(voiceState, "user", "id") ?? "?";

    private void OnVoiceStateEvent(string name, JsonElement data)
    {
        var userId = Nested(data, "user", "id");
        if (userId is null) return;
        if (name == "VOICE_STATE_DELETE") _voice.RemoveMember(userId);
        else _voice.UpsertMember(userId, DisplayName(data));
    }

    private void OnSpeakingEvent(string name, JsonElement data)
    {
        var userId = Nested(data, "user_id");
        if (userId is null) return;
        _voice.SetSpeaking(userId, name == "SPEAKING_START", DateTime.UtcNow);
    }

    private void OnVoiceSettings(JsonElement data)
    {
        bool? mute = data.TryGetProperty("mute", out var m) && m.ValueKind is JsonValueKind.True or JsonValueKind.False ? m.GetBoolean() : null;
        bool? deaf = data.TryGetProperty("deaf", out var d) && d.ValueKind is JsonValueKind.True or JsonValueKind.False ? d.GetBoolean() : null;
        _voice.SetSelf(mute, deaf);
    }
}
