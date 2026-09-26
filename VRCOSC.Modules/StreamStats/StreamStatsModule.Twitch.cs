// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Twitch half of Stream Stats: live flag, game, title, viewers, followers and uptime polled
// from Helix. Authenticates with its own token through the device code grant flow (modules
// cannot share the official Twitch module's token).

using System.Net;
using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.StreamStats;

public partial class StreamStatsModule
{
    /// <summary>The official VRCOSC Twitch app. Any Twitch app with the device code grant enabled works.</summary>
    private const string DefaultClientId = "6y51jdzkdtlwv56akwerab47wwov1w";

    private const string TwitchScopes = "moderator:read:followers";

    // MagicChatbox placeholder keys; the converter maps them to {<package>.<module>_<key>}.
    private const string VarTwitchLive = "twitch_live";
    private const string VarTwitchChannel = "twitch_channel";
    private const string VarTwitchGame = "twitch_game";
    private const string VarTwitchTitle = "twitch_title";
    private const string VarTwitchViewers = "twitch_viewers";
    private const string VarTwitchFollowers = "twitch_followers";
    private const string VarTwitchUptime = "twitch_uptime";

    // Persistent keys are unchanged from the former TwitchStats module so a saved login survives.
    [ModulePersistent("twitch_access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [ModulePersistent("twitch_refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    private TwitchHelixClient? _twitchClient;
    private CancellationTokenSource? _twitchCts;
    private TokenInfo? _tokenInfo;
    private HelixUser? _channelUser;
    private string _resolvedChannelFor = string.Empty;

    private bool _twitchActive;
    private bool _twitchAuthenticated;
    private bool _twitchLive;
    private int _twitchViewers;
    private bool _authenticating;
    private bool _twitchPolling;
    private bool? _twitchWasLive;
    private DateTime _nextTwitchPoll = DateTime.MinValue;

    private void CreateTwitchSettings()
    {
        CreateToggle(TwitchSetting.TwitchEnabled, "Twitch enabled", "Watch the Twitch channel below (only runs when a channel is set)", true);
        CreateTextBox(TwitchSetting.Channel, "Channel", "Twitch login name to watch", string.Empty);
        CreateTextBox(TwitchSetting.PollInterval, "Poll interval (seconds)", "How often Helix is asked for stream, channel and follower data", 60);
        CreateTextBox(TwitchSetting.ClientId, "Client ID", "Twitch application client ID used for the device code login. The default is the official VRCOSC app; use your own if the login fails.", DefaultClientId);
        CreatePasswordTextBox(TwitchSetting.ManualToken, "Access token (manual)", "Optional. A user access token pasted by hand (needs the moderator:read:followers scope for followers). When set, the device code login is skipped.", string.Empty);
        CreateToggle(TwitchSetting.ForgetToken, "Forget saved login", "Drop the stored token on the next start and log in again through the device code flow", false);
    }

    private (ClipVariableReference Channel, ClipVariableReference Game) CreateTwitchVariables()
    {
        CreateVariable<bool>(VarTwitchLive, "Twitch Live");
        var channel = CreateVariable<string>(VarTwitchChannel, "Twitch Channel (display name)")!;
        var game = CreateVariable<string>(VarTwitchGame, "Twitch Game")!;
        CreateVariable<string>(VarTwitchTitle, "Twitch Title");
        CreateVariable<int>(VarTwitchViewers, "Twitch Viewers");
        CreateVariable<int>(VarTwitchFollowers, "Twitch Followers");
        CreateVariable<string>(VarTwitchUptime, "Twitch Uptime (h:mm)");
        return (channel, game);
    }

    /// <summary>Starts the Twitch loop when enabled and a channel is set; returns whether it runs.</summary>
    private bool StartTwitch()
    {
        _twitchActive = false;
        _twitchAuthenticated = false;
        _twitchLive = false;
        _twitchViewers = 0;
        _tokenInfo = null;
        _channelUser = null;
        _resolvedChannelFor = string.Empty;
        _twitchWasLive = null;
        _nextTwitchPoll = DateTime.MinValue;
        _authenticating = false;
        _twitchPolling = false;
        SendParameter(StreamParameter.TwitchLive, false);
        SetVariableValue(VarTwitchLive, false);

        if (!GetSettingValue<bool>(TwitchSetting.TwitchEnabled)) return false;

        if (TwitchChannelSetting().Length == 0)
        {
            Log("Twitch: no channel set; the Twitch half stays idle.");
            return false;
        }

        if (GetSettingValue<bool>(TwitchSetting.ForgetToken))
        {
            AccessToken = string.Empty;
            RefreshToken = string.Empty;
            Log("Twitch: saved login forgotten as requested; turn the setting off again to keep the next one.");
        }

        _twitchCts = new CancellationTokenSource();
        _twitchClient = new TwitchHelixClient(ClientId());
        _twitchActive = true;
        return true;
    }

    private void StopTwitch()
    {
        _twitchCts?.Cancel();
        _twitchCts?.Dispose();
        _twitchCts = null;
        _twitchClient = null;
        _twitchActive = false;
        _twitchAuthenticated = false;
        _twitchLive = false;
        _twitchViewers = 0;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void TwitchTick()
    {
        var cts = _twitchCts;
        if (!_twitchActive || cts is null || _twitchClient is null) return;

        if (_tokenInfo is null)
        {
            if (_authenticating) return;
            _authenticating = true;
            _ = RunTwitchGuarded(() => AuthenticateAsync(cts.Token), () => _authenticating = false);
            return;
        }

        if (_twitchPolling || DateTime.UtcNow < _nextTwitchPoll) return;
        _twitchPolling = true;
        _ = RunTwitchGuarded(() => PollTwitchAsync(cts.Token), () => _twitchPolling = false);
    }

    /// <summary>Background work must never throw: an unhandled exception on a pool thread kills VRCOSC.</summary>
    private async Task RunTwitchGuarded(Func<Task> work, Action done)
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
            _nextTwitchPoll = DateTime.UtcNow + TwitchInterval();
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
        var client = _twitchClient;
        if (client is null) return false;

        var info = await client.ValidateAsync(token, ct).ConfigureAwait(false);
        if (info is null)
        {
            Log($"Twitch: the {source} is expired or revoked.");
            return false;
        }

        if (!info.Scopes.Contains(TwitchScopes, StringComparer.OrdinalIgnoreCase))
            Log($"Twitch: the {source} lacks the {TwitchScopes} scope; the follower count will stay 0.");

        AccessToken = token;
        _tokenInfo = info;
        _twitchAuthenticated = true;
        Log($"Twitch: logged in as {info.Login}.");
        _nextTwitchPoll = DateTime.MinValue;
        PublishCombined();
        return true;
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        var client = _twitchClient;
        if (client is null) return false;

        var (token, error) = await client.RefreshAsync(RefreshToken, ct).ConfigureAwait(false);
        if (token is null)
        {
            Log($"Twitch: token refresh failed ({error}); logging in again.");
            RefreshToken = string.Empty;
            return false;
        }

        RefreshToken = token.RefreshToken ?? RefreshToken;
        return await TryUseTokenAsync(token.AccessToken, "refreshed token", ct).ConfigureAwait(false);
    }

    private async Task DeviceCodeLoginAsync(CancellationToken ct)
    {
        var client = _twitchClient;
        if (client is null) return;

        var (code, error) = await client.RequestDeviceCodeAsync(TwitchScopes, ct).ConfigureAwait(false);
        if (code is null)
        {
            Log($"Twitch: could not start the login ({error}). Retrying in 60 s; alternatively paste a token into the module settings.");
            await Task.Delay(TimeSpan.FromSeconds(60), ct).ConfigureAwait(false);
            return;
        }

        Log($"Twitch login: open {code.VerificationUri} and enter the code {code.UserCode} (valid for {(int)code.ExpiresIn.TotalMinutes} min).");

        var deadline = DateTime.UtcNow + code.ExpiresIn;
        var interval = code.Interval;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(interval, ct).ConfigureAwait(false);
            var (status, token, pollError) = await client.PollDeviceTokenAsync(code, ct).ConfigureAwait(false);

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
                    Log("Twitch: login was denied. Starting a new login.");
                    return;

                case DevicePollStatus.Expired:
                    Log("Twitch: the login code expired. Starting a new login.");
                    return;

                default:
                    Log($"Twitch: login poll failed ({pollError}); retrying.");
                    continue;
            }
        }

        Log("Twitch: the login code expired. Starting a new login.");
    }

    // ─────────────────────────── Polling ───────────────────────────

    private async Task PollTwitchAsync(CancellationToken ct)
    {
        try
        {
            await PollTwitchOnceAsync(ct).ConfigureAwait(false);
            _nextTwitchPoll = DateTime.UtcNow + TwitchInterval();
        }
        catch (HelixException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var wait = ex.RateLimitReset is { } reset && reset > DateTime.UtcNow ? reset - DateTime.UtcNow : TwitchInterval();
            Log($"Twitch: rate limit hit; retrying in {(int)wait.TotalSeconds} s.");
            _nextTwitchPoll = DateTime.UtcNow + wait;
        }
        catch (HelixException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log($"Twitch: token rejected ({ex.Message}); re-authenticating.");
            _tokenInfo = null;
            _twitchAuthenticated = false;
            AccessToken = string.Empty;
            PublishCombined();
        }
    }

    private async Task PollTwitchOnceAsync(CancellationToken ct)
    {
        var client = _twitchClient;
        var token = AccessToken;
        var info = _tokenInfo;
        if (client is null || info is null || token.Length == 0) return;

        var login = TwitchChannelSetting();
        if (login.Length == 0) login = info.Login;

        if (_channelUser is null || _resolvedChannelFor != login)
        {
            _channelUser = await client.GetUserAsync(token, login, ct).ConfigureAwait(false);
            _resolvedChannelFor = login;
            if (_channelUser is null)
            {
                Log($"Twitch: channel '{login}' does not exist.");
                ApplyTwitch(null, null, null);
                return;
            }
        }

        var stream = await client.GetStreamAsync(token, _channelUser.Login, ct).ConfigureAwait(false);
        var channel = await client.GetChannelAsync(token, _channelUser.Id, ct).ConfigureAwait(false);
        var followers = await FetchTwitchFollowersAsync(client, token, _channelUser.Id, ct).ConfigureAwait(false);

        ApplyTwitch(stream, channel, followers);
    }

    /// <summary>A missing scope makes this one endpoint answer 401; that must not log the whole module out.</summary>
    private async Task<int?> FetchTwitchFollowersAsync(TwitchHelixClient client, string token, string broadcasterId, CancellationToken ct)
    {
        if (_tokenInfo is not null && !_tokenInfo.Scopes.Contains(TwitchScopes, StringComparer.OrdinalIgnoreCase)) return null;

        try
        {
            return await client.GetFollowerTotalAsync(token, broadcasterId, ct).ConfigureAwait(false);
        }
        catch (HelixException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            Log($"Twitch: follower count unavailable ({ex.Message}).");
            return null;
        }
    }

    private void ApplyTwitch(HelixStream? stream, HelixChannel? channel, int? followers)
    {
        var live = stream is not null;
        var displayName = channel?.DisplayName ?? _channelUser?.DisplayName ?? string.Empty;

        SetVariableValue(VarTwitchLive, live);
        SetVariableValue(VarTwitchChannel, displayName);
        SetVariableValue(VarTwitchGame, stream?.GameName ?? channel?.GameName ?? string.Empty);
        SetVariableValue(VarTwitchTitle, stream?.Title ?? channel?.Title ?? string.Empty);
        SetVariableValue(VarTwitchViewers, stream?.ViewerCount ?? 0);
        if (followers is { } total) SetVariableValue(VarTwitchFollowers, total);
        SetVariableValue(VarTwitchUptime, stream is null ? string.Empty : FormatUptime(DateTime.UtcNow - stream.StartedAt));

        SendParameter(StreamParameter.TwitchLive, live);

        _twitchLive = live;
        _twitchViewers = stream?.ViewerCount ?? 0;
        PublishCombined();

        if (_twitchWasLive is { } was && was != live) TriggerEvent(live ? StreamEvent.WentLive : StreamEvent.WentOffline);
        _twitchWasLive = live;
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero) uptime = TimeSpan.Zero;
        return $"{(int)uptime.TotalHours}:{uptime.Minutes:00}";
    }

    private TimeSpan TwitchInterval() => TimeSpan.FromSeconds(Math.Max(5, GetSettingValue<int>(TwitchSetting.PollInterval)));

    private string TwitchChannelSetting() => GetSettingValue<string>(TwitchSetting.Channel)?.Trim() ?? string.Empty;

    private string ClientId()
    {
        var id = GetSettingValue<string>(TwitchSetting.ClientId)?.Trim() ?? string.Empty;
        return id.Length > 0 ? id : DefaultClientId;
    }

    private enum TwitchSetting { TwitchEnabled, Channel, PollInterval, ClientId, ManualToken, ForgetToken }
}
