// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox-parity Twitch stats for the ChatBox: live flag, game, title, viewers,
// followers and uptime, polled from Helix. Authenticates with its own token through the
// device code grant flow (modules cannot share the official Twitch module's token).

using System.Net;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.TwitchStats;

[ModuleTitle("Twitch Stats")]
[ModuleDescription("Live status, game, title, viewers, followers and uptime of a Twitch channel for the ChatBox. Logs in through the Twitch device code flow.")]
[ModuleType(ModuleType.Integrations)]
public class TwitchStatsModule : Module
{
    /// <summary>The official VRCOSC Twitch app. Any Twitch app with the device code grant enabled works.</summary>
    private const string DefaultClientId = "6y51jdzkdtlwv56akwerab47wwov1w";

    private const string Scopes = "moderator:read:followers";

    // MagicChatbox placeholder keys; the converter maps them to {<package>.<module>_<key>}.
    private const string VarLive = "twitch_live";
    private const string VarChannel = "twitch_channel";
    private const string VarGame = "twitch_game";
    private const string VarTitle = "twitch_title";
    private const string VarViewers = "twitch_viewers";
    private const string VarFollowers = "twitch_followers";
    private const string VarUptime = "twitch_uptime";

    [ModulePersistent("twitch_access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [ModulePersistent("twitch_refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    private TwitchHelixClient? _client;
    private CancellationTokenSource? _cts;
    private TokenInfo? _tokenInfo;
    private HelixUser? _channelUser;
    private string _resolvedChannelFor = string.Empty;

    private bool _authenticating;
    private bool _polling;
    private bool? _wasLive;
    private DateTime _nextPoll = DateTime.MinValue;

    protected override void OnPreLoad()
    {
        CreateTextBox(TwitchSetting.Channel, "Channel", "Twitch login name to watch. Leave empty to use the account you logged in with.", string.Empty);
        CreateTextBox(TwitchSetting.PollInterval, "Poll interval (seconds)", "How often Helix is asked for stream, channel and follower data", 60);
        CreateTextBox(TwitchSetting.ClientId, "Client ID", "Twitch application client ID used for the device code login. The default is the official VRCOSC app; use your own if the login fails.", DefaultClientId);
        CreatePasswordTextBox(TwitchSetting.ManualToken, "Access token (manual)", "Optional. A user access token pasted by hand (needs the moderator:read:followers scope for followers). When set, the device code login is skipped.", string.Empty);
        CreateToggle(TwitchSetting.ForgetToken, "Forget saved login", "Drop the stored token on the next start and log in again through the device code flow", false);

        CreateGroup("Login", "Authentication; watch the log for the verification URL and code", TwitchSetting.ClientId, TwitchSetting.ManualToken, TwitchSetting.ForgetToken);

        RegisterParameter<bool>(TwitchParameter.Live, "VRCOSC/Twitch/Live", ParameterMode.Write, "Live", "True while the channel is streaming");
    }

    protected override void OnPostLoad()
    {
        CreateVariable<bool>(VarLive, "Live");
        var channel = CreateVariable<string>(VarChannel, "Channel (display name)")!;
        var game = CreateVariable<string>(VarGame, "Game")!;
        var title = CreateVariable<string>(VarTitle, "Title")!;
        var viewers = CreateVariable<int>(VarViewers, "Viewers")!;
        var followers = CreateVariable<int>(VarFollowers, "Followers")!;
        var uptime = CreateVariable<string>(VarUptime, "Uptime (h:mm)")!;

        CreateState(TwitchState.Live, "Live", "{0} is live: {1}\n{2} | {3} viewers | {4}", new[] { channel, game, title, viewers, uptime });
        CreateState(TwitchState.Offline, "Offline", "{0} is offline\n{1} followers", new[] { channel, followers });
        CreateState(TwitchState.Unauthenticated, "Unauthenticated", string.Empty);

        CreateEvent(TwitchEvent.WentLive, "Went live", "{0} went live: {1}", new[] { channel, game });
        CreateEvent(TwitchEvent.WentOffline, "Went offline", "{0} went offline", new[] { channel });
    }

    protected override Task<bool> OnModuleStart()
    {
        _cts = new CancellationTokenSource();
        _client = new TwitchHelixClient(ClientId());
        _tokenInfo = null;
        _channelUser = null;
        _resolvedChannelFor = string.Empty;
        _wasLive = null;
        _nextPoll = DateTime.MinValue;
        _authenticating = false;
        _polling = false;

        if (GetSettingValue<bool>(TwitchSetting.ForgetToken))
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            Log("Saved login forgotten as requested; turn the setting off again to keep the next one.");
        }

        ChangeState(TwitchState.Unauthenticated);
        SendParameter(TwitchParameter.Live, false);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        ChangeState(TwitchState.Unauthenticated);
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void Tick()
    {
        if (_cts is null || _client is null) return;

        if (_tokenInfo is null)
        {
            if (_authenticating) return;
            _authenticating = true;
            _ = RunGuarded(() => AuthenticateAsync(_cts.Token), () => _authenticating = false);
            return;
        }

        if (_polling || DateTime.UtcNow < _nextPoll) return;
        _polling = true;
        _ = RunGuarded(() => PollAsync(_cts.Token), () => _polling = false);
    }

    /// <summary>Background work must never throw: an unhandled exception on a pool thread kills VRCOSC.</summary>
    private async Task RunGuarded(Func<Task> work, Action done)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // module stopped
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.Text.Json.JsonException or HelixException or InvalidOperationException)
        {
            Log($"Twitch request failed: {ex.Message}");
            _nextPoll = DateTime.UtcNow + Interval();
        }
        finally
        {
            done();
        }
    }

    // ─────────────────────────── Auth ───────────────────────────

    private async Task AuthenticateAsync(CancellationToken ct)
    {
        var manual = GetSettingValue<string>(TwitchSetting.ManualToken)?.Trim() ?? string.Empty;
        if (manual.Length > 0 && await TryUseTokenAsync(manual, "manual token", ct).ConfigureAwait(false)) return;

        if (AccessToken.Length > 0 && await TryUseTokenAsync(AccessToken, "saved token", ct).ConfigureAwait(false)) return;

        if (RefreshToken.Length > 0 && await TryRefreshAsync(ct).ConfigureAwait(false)) return;

        await DeviceCodeLoginAsync(ct).ConfigureAwait(false);
    }

    private async Task<bool> TryUseTokenAsync(string token, string source, CancellationToken ct)
    {
        var info = await _client!.ValidateAsync(token, ct).ConfigureAwait(false);
        if (info is null)
        {
            Log($"The {source} is expired or revoked.");
            return false;
        }

        if (!info.Scopes.Contains(Scopes, StringComparer.OrdinalIgnoreCase))
            Log($"The {source} lacks the {Scopes} scope; the follower count will stay 0.");

        AccessToken = token;
        _tokenInfo = info;
        Log($"Logged in as {info.Login}.");
        _nextPoll = DateTime.MinValue;
        return true;
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        var (token, error) = await _client!.RefreshAsync(RefreshToken, ct).ConfigureAwait(false);
        if (token is null)
        {
            Log($"Token refresh failed ({error}); logging in again.");
            RefreshToken = string.Empty;
            return false;
        }

        RefreshToken = token.RefreshToken ?? RefreshToken;
        return await TryUseTokenAsync(token.AccessToken, "refreshed token", ct).ConfigureAwait(false);
    }

    private async Task DeviceCodeLoginAsync(CancellationToken ct)
    {
        var (code, error) = await _client!.RequestDeviceCodeAsync(Scopes, ct).ConfigureAwait(false);
        if (code is null)
        {
            Log($"Could not start the Twitch login ({error}). Retrying in 60 s; alternatively paste a token into the module settings.");
            await Task.Delay(TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);
            return;
        }

        Log($"Twitch login: open {code.VerificationUri} and enter the code {code.UserCode} (valid for {(int)code.ExpiresIn.TotalMinutes} min).");

        var deadline = DateTime.UtcNow + code.ExpiresIn;
        var interval = code.Interval;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(interval, ct).ConfigureAwait(false);
            var (status, token, pollError) = await _client.PollDeviceTokenAsync(code, ct).ConfigureAwait(false);

            switch (status)
            {
                case DevicePollStatus.Pending:
                    continue;

                case DevicePollStatus.SlowDown:
                    interval += TimeSpan.FromSeconds(5);
                    continue;

                case DevicePollStatus.Granted:
                    RefreshToken = token!.RefreshToken ?? string.Empty;
                    await TryUseTokenAsync(token.AccessToken, "new token", ct).ConfigureAwait(false);
                    return;

                case DevicePollStatus.Denied:
                    Log("Twitch login was denied. Starting a new login.");
                    return;

                case DevicePollStatus.Expired:
                    Log("The Twitch login code expired. Starting a new login.");
                    return;

                default:
                    Log($"Twitch login poll failed ({pollError}); retrying.");
                    continue;
            }
        }

        Log("The Twitch login code expired. Starting a new login.");
    }

    // ─────────────────────────── Polling ───────────────────────────

    private async Task PollAsync(CancellationToken ct)
    {
        try
        {
            await PollOnceAsync(ct).ConfigureAwait(false);
            _nextPoll = DateTime.UtcNow + Interval();
        }
        catch (HelixException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var wait = ex.RateLimitReset is { } reset && reset > DateTime.UtcNow ? reset - DateTime.UtcNow : Interval();
            Log($"Twitch rate limit hit; retrying in {(int)wait.TotalSeconds} s.");
            _nextPoll = DateTime.UtcNow + wait;
        }
        catch (HelixException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log($"Twitch rejected the token ({ex.Message}); re-authenticating.");
            _tokenInfo = null;
            AccessToken = string.Empty;
            ChangeState(TwitchState.Unauthenticated);
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var token = AccessToken;
        var info = _tokenInfo;
        if (info is null || token.Length == 0) return;
        var channelSetting = GetSettingValue<string>(TwitchSetting.Channel)?.Trim() ?? string.Empty;
        var login = channelSetting.Length > 0 ? channelSetting : info.Login;

        if (_channelUser is null || _resolvedChannelFor != login)
        {
            _channelUser = await _client!.GetUserAsync(token, login, ct).ConfigureAwait(false);
            _resolvedChannelFor = login;
            if (_channelUser is null)
            {
                Log($"Twitch channel '{login}' does not exist.");
                ChangeState(TwitchState.Offline);
                return;
            }
        }

        var stream = await _client!.GetStreamAsync(token, _channelUser.Login, ct).ConfigureAwait(false);
        var channel = await _client.GetChannelAsync(token, _channelUser.Id, ct).ConfigureAwait(false);
        var followers = await FetchFollowersAsync(token, _channelUser.Id, ct).ConfigureAwait(false);

        Apply(stream, channel, followers);
    }

    /// <summary>A missing scope makes this one endpoint answer 401; that must not log the whole module out.</summary>
    private async Task<int?> FetchFollowersAsync(string token, string broadcasterId, CancellationToken ct)
    {
        if (_tokenInfo is not null && !_tokenInfo.Scopes.Contains(Scopes, StringComparer.OrdinalIgnoreCase)) return null;

        try
        {
            return await _client!.GetFollowerTotalAsync(token, broadcasterId, ct).ConfigureAwait(false);
        }
        catch (HelixException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Log($"Follower count unavailable ({ex.Message}).");
            return null;
        }
    }

    private void Apply(HelixStream? stream, HelixChannel? channel, int? followers)
    {
        var live = stream is not null;
        var displayName = channel?.DisplayName ?? _channelUser?.DisplayName ?? string.Empty;

        SetVariableValue(VarLive, live);
        SetVariableValue(VarChannel, displayName);
        SetVariableValue(VarGame, stream?.GameName ?? channel?.GameName ?? string.Empty);
        SetVariableValue(VarTitle, stream?.Title ?? channel?.Title ?? string.Empty);
        SetVariableValue(VarViewers, stream?.ViewerCount ?? 0);
        if (followers is { } total) SetVariableValue(VarFollowers, total);
        SetVariableValue(VarUptime, stream is null ? string.Empty : FormatUptime(DateTime.UtcNow - stream.StartedAt));

        SendParameter(TwitchParameter.Live, live);
        ChangeState(live ? TwitchState.Live : TwitchState.Offline);

        if (_wasLive is { } was && was != live) TriggerEvent(live ? TwitchEvent.WentLive : TwitchEvent.WentOffline);
        _wasLive = live;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero) uptime = TimeSpan.Zero;
        return $"{(int)uptime.TotalHours}:{uptime.Minutes:00}";
    }

    private TimeSpan Interval() => TimeSpan.FromSeconds(Math.Max(5, GetSettingValue<int>(TwitchSetting.PollInterval)));

    private string ClientId()
    {
        var id = GetSettingValue<string>(TwitchSetting.ClientId)?.Trim() ?? string.Empty;
        return id.Length > 0 ? id : DefaultClientId;
    }

    private enum TwitchSetting { Channel, PollInterval, ClientId, ManualToken, ForgetToken }

    private enum TwitchParameter { Live }

    private enum TwitchState { Live, Offline, Unauthenticated }

    private enum TwitchEvent { WentLive, WentOffline }
}
