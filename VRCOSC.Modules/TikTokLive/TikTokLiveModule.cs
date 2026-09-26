// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox-parity TikTok LIVE stats (host, viewers, likes, followers, live flag) for the
// ChatBox. Polls TikTok's public endpoints - no login, no websocket - so counters update on
// the poll interval rather than per event. See README.md for the trade-offs.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.TikTokLive;

[ModuleTitle("TikTok Live")]
[ModuleDescription("Viewer, like and follower counts plus a live flag for a TikTok host, polled from TikTok's public pages (no login needed)")]
[ModuleType(ModuleType.Integrations)]
public class TikTokLiveModule : Module
{
    // Exact MagicChatbox placeholder keys; the converter relies on these lookups.
    private const string VarHost = "tiktok_host";
    private const string VarViewers = "tiktok_viewers";
    private const string VarLikes = "tiktok_likes";
    private const string VarFollowers = "tiktok_followers";
    private const string VarLive = "tiktok_live";

    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(5);

    private readonly TikTokPublicClient _client = new();
    private CancellationTokenSource? _cts;
    private Task? _worker;

    private string _host = string.Empty;
    private string? _roomId;
    private bool _live;
    private int _likes;
    private int _followers;
    private bool _haveLikes;
    private bool _haveFollowers;
    private int _failures;
    private DateTime _lastProfileFetch = DateTime.MinValue;

    protected override void OnPreLoad()
    {
        CreateTextBox(TikTokSetting.Host, "Host", "TikTok username of the streamer to watch (the @name, without the @)", string.Empty);
        CreateSlider(TikTokSetting.LivePollSeconds, "Poll interval while live (seconds)", "How often viewers/likes are refreshed while the host is live", 15, 5, 120, 5);
        CreateSlider(TikTokSetting.OfflinePollSeconds, "Poll interval while offline (seconds)", "How often to check whether the host went live", 60, 15, 600, 15);
        CreateSlider(TikTokSetting.FollowersRefreshMinutes, "Followers refresh (minutes)", "How often the follower count is scraped from the public profile page (only used when the live room does not report it)", 5, 1, 60);
        CreateSlider(TikTokSetting.MaxBackoffSeconds, "Max reconnect backoff (seconds)", "Upper bound for the exponential delay between retries after TikTok errors or rate limits", 300, 30, 900, 30);
        CreateGroup("Polling", "Refresh intervals and retry behaviour", TikTokSetting.LivePollSeconds, TikTokSetting.OfflinePollSeconds, TikTokSetting.FollowersRefreshMinutes, TikTokSetting.MaxBackoffSeconds);

        RegisterParameter<bool>(TikTokParameter.Live, "VRCOSC/TikTok/Live", ParameterMode.Write, "Live", "Whether the host is currently live");
        RegisterParameter<int>(TikTokParameter.Viewers, "VRCOSC/TikTok/Viewers", ParameterMode.Write, "Viewers", "Current viewer count (0 while offline)");
    }

    protected override void OnPostLoad()
    {
        var host = CreateVariable<string>(VarHost, "Host")!;
        var viewers = CreateVariable<int>(VarViewers, "Viewers")!;
        var likes = CreateVariable<int>(VarLikes, "Likes")!;
        var followers = CreateVariable<int>(VarFollowers, "Followers")!;
        CreateVariable<bool>(VarLive, "Live");

        CreateState(TikTokState.Live, "Live", "\U0001F534 @{0} LIVE\n\U0001F441 {1} ❤ {2} \U0001F465 {3}", new[] { host, viewers, likes, followers });
        CreateState(TikTokState.Offline, "Offline", "@{0} offline · \U0001F465 {1}", new[] { host, followers });

        CreateEvent(TikTokEvent.Follow, "New follower(s)", "\U0001F465 New follower! {0} followers", new[] { followers });
        CreateEvent(TikTokEvent.Like, "New like(s)", "❤ {0} likes", new[] { likes });
    }

    protected override Task<bool> OnModuleStart()
    {
        _host = TikTokPublicClient.NormaliseHost(GetSettingValue<string>(TikTokSetting.Host));
        if (string.IsNullOrEmpty(_host))
        {
            Log("Set the TikTok host (@name) in the module settings.");
            return Task.FromResult(false);
        }

        _roomId = null;
        _live = false;
        _likes = 0;
        _followers = 0;
        _haveLikes = false;
        _haveFollowers = false;
        _failures = 0;
        _lastProfileFetch = DateTime.MinValue;

        SetVariableValue(VarHost, _host);
        PublishOffline();
        SetVariableValue(VarFollowers, 0);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _worker = Task.Run(() => RunAsync(token), token);
        return Task.FromResult(true);
    }

    protected override async Task OnModuleStop()
    {
        var cts = _cts;
        var worker = _worker;
        _cts = null;
        _worker = null;
        if (cts is null) return;

        cts.Cancel();
        try
        {
            if (worker is not null) await worker.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cancellation or timeout on shutdown; nothing to recover.
        }
        cts.Dispose();
        PublishOffline();
    }

    /// <summary>Poll loop. Every exception is caught here so nothing escapes onto the thread pool.</summary>
    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                await PollOnceAsync(token).ConfigureAwait(false);
                _failures = 0;
                delay = TimeSpan.FromSeconds(_live ? GetSettingValue<int>(TikTokSetting.LivePollSeconds) : GetSettingValue<int>(TikTokSetting.OfflinePollSeconds));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _failures = Math.Min(_failures + 1, 10);
                delay = Backoff();
                Log($"TikTok poll failed ({ex.GetType().Name}: {ex.Message}); retrying in {delay.TotalSeconds:0}s");
            }

            try { await Task.Delay(delay, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private TimeSpan Backoff()
    {
        var max = TimeSpan.FromSeconds(GetSettingValue<int>(TikTokSetting.MaxBackoffSeconds));
        var seconds = MinBackoff.TotalSeconds * Math.Pow(2, _failures - 1);
        return seconds > max.TotalSeconds ? max : TimeSpan.FromSeconds(seconds);
    }

    private async Task PollOnceAsync(CancellationToken token)
    {
        if (_roomId is null)
        {
            _roomId = await _client.GetRoomIdAsync(_host, token).ConfigureAwait(false);
            if (_roomId is null)
            {
                await RefreshFollowersFromProfileAsync(token).ConfigureAwait(false);
                if (_live) { Log($"@{_host} is no longer live."); }
                PublishOffline();
                return;
            }
        }

        var info = await _client.GetRoomInfoAsync(_roomId, token).ConfigureAwait(false);
        if (info is null) throw new InvalidOperationException("room info unavailable");

        if (info.Value.Followers is { } followers) ApplyFollowers(followers, FollowerSource.RoomInfo);
        else await RefreshFollowersFromProfileAsync(token).ConfigureAwait(false);

        if (!info.Value.IsLive)
        {
            if (_live) Log($"@{_host} ended the stream.");
            _roomId = null; // the next poll re-resolves; a new stream gets a new room id
            PublishOffline();
            return;
        }

        if (!_live) Log($"@{_host} is live: {info.Value.Title}");
        PublishLive(info.Value);
    }

    private async Task RefreshFollowersFromProfileAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromMinutes(GetSettingValue<int>(TikTokSetting.FollowersRefreshMinutes));
        if (DateTime.UtcNow - _lastProfileFetch < interval) return;
        _lastProfileFetch = DateTime.UtcNow;

        try
        {
            var followers = await _client.GetProfileFollowersAsync(_host, token).ConfigureAwait(false);
            if (followers is { } count) ApplyFollowers(count, FollowerSource.ProfilePage);
            else Log("Follower count not found on the profile page; keeping the last known value.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Log($"Profile follower fetch failed ({ex.GetType().Name}: {ex.Message}); keeping the last known value.");
        }
    }

    private void ApplyFollowers(int followers, FollowerSource source)
    {
        if (_haveFollowers && followers > _followers) TriggerEvent(TikTokEvent.Follow);
        if (!_haveFollowers) Log($"Followers: {followers} ({source})");
        _followers = followers;
        _haveFollowers = true;
        SetVariableValue(VarFollowers, followers);
    }

    private void PublishLive(TikTokRoomInfo info)
    {
        if (_haveLikes && info.Likes > _likes) TriggerEvent(TikTokEvent.Like);
        _likes = info.Likes;
        _haveLikes = true;
        _live = true;

        SetVariableValue(VarLive, true);
        SetVariableValue(VarViewers, info.Viewers);
        SetVariableValue(VarLikes, info.Likes);
        SendParameter(TikTokParameter.Live, true);
        SendParameter(TikTokParameter.Viewers, info.Viewers);
        ChangeState(TikTokState.Live);
    }

    private void PublishOffline()
    {
        _live = false;
        _haveLikes = false;
        SetVariableValue(VarLive, false);
        SetVariableValue(VarViewers, 0);
        SetVariableValue(VarLikes, 0);
        SendParameter(TikTokParameter.Live, false);
        SendParameter(TikTokParameter.Viewers, 0);
        ChangeState(TikTokState.Offline);
    }

    private enum TikTokSetting { Host, LivePollSeconds, OfflinePollSeconds, FollowersRefreshMinutes, MaxBackoffSeconds }

    private enum TikTokParameter { Live, Viewers }

    private enum TikTokState { Live, Offline }

    private enum TikTokEvent { Follow, Like }
}
