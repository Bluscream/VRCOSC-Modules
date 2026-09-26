// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// TikTok half of Stream Stats: host, viewers, likes, followers and live flag polled from
// TikTok's public endpoints - no login, no websocket - so counters update on the poll
// interval rather than per event. See README.md for the trade-offs.

using VRCOSC.App.ChatBox.Clips.Variables;

namespace Bluscream.Modules.StreamStats;

public partial class StreamStatsModule
{
    // Exact MagicChatbox placeholder keys; the converter relies on these lookups.
    private const string VarTikTokHost = "tiktok_host";
    private const string VarTikTokViewers = "tiktok_viewers";
    private const string VarTikTokLikes = "tiktok_likes";
    private const string VarTikTokFollowers = "tiktok_followers";
    private const string VarTikTokLive = "tiktok_live";

    private static readonly TimeSpan TikTokMinBackoff = TimeSpan.FromSeconds(5);

    private readonly TikTokPublicClient _tiktokClient = new();
    private CancellationTokenSource? _tiktokCts;
    private Task? _tiktokWorker;

    private string _tiktokHost = string.Empty;
    private string? _tiktokRoomId;
    private bool _tiktokLive;
    private int _tiktokViewers;
    private int _tiktokLikes;
    private int _tiktokFollowers;
    private bool _haveTikTokLikes;
    private bool _haveTikTokFollowers;
    private int _tiktokFailures;
    private DateTime _lastProfileFetch = DateTime.MinValue;

    private void CreateTikTokSettings()
    {
        CreateToggle(TikTokSetting.TikTokEnabled, "TikTok enabled", "Watch the TikTok host below (only runs when a host is set)", true);
        CreateTextBox(TikTokSetting.Host, "Host", "TikTok username of the streamer to watch (the @name, without the @)", string.Empty);
        CreateSlider(TikTokSetting.LivePollSeconds, "Poll interval while live (seconds)", "How often viewers/likes are refreshed while the host is live", 15, 5, 120, 5);
        CreateSlider(TikTokSetting.OfflinePollSeconds, "Poll interval while offline (seconds)", "How often to check whether the host went live", 60, 15, 600, 15);
        CreateSlider(TikTokSetting.FollowersRefreshMinutes, "Followers refresh (minutes)", "How often the follower count is scraped from the public profile page (only used when the live room does not report it)", 5, 1, 60);
        CreateSlider(TikTokSetting.MaxBackoffSeconds, "Max reconnect backoff (seconds)", "Upper bound for the exponential delay between retries after TikTok errors or rate limits", 300, 30, 900, 30);
    }

    private (ClipVariableReference Followers, ClipVariableReference Likes) CreateTikTokVariables()
    {
        CreateVariable<string>(VarTikTokHost, "TikTok Host");
        CreateVariable<int>(VarTikTokViewers, "TikTok Viewers");
        var likes = CreateVariable<int>(VarTikTokLikes, "TikTok Likes")!;
        var followers = CreateVariable<int>(VarTikTokFollowers, "TikTok Followers")!;
        CreateVariable<bool>(VarTikTokLive, "TikTok Live");
        return (followers, likes);
    }

    /// <summary>Starts the TikTok poll loop when enabled and a host is set; returns whether it runs.</summary>
    private bool StartTikTok()
    {
        _tiktokRoomId = null;
        _tiktokLive = false;
        _tiktokViewers = 0;
        _tiktokLikes = 0;
        _tiktokFollowers = 0;
        _haveTikTokLikes = false;
        _haveTikTokFollowers = false;
        _tiktokFailures = 0;
        _lastProfileFetch = DateTime.MinValue;
        PublishTikTokOffline();
        SetVariableValue(VarTikTokFollowers, 0);

        if (!GetSettingValue<bool>(TikTokSetting.TikTokEnabled)) return false;

        _tiktokHost = TikTokPublicClient.NormaliseHost(GetSettingValue<string>(TikTokSetting.Host));
        if (string.IsNullOrEmpty(_tiktokHost))
        {
            Log("TikTok: no host (@name) set; the TikTok half stays idle.");
            return false;
        }

        SetVariableValue(VarTikTokHost, _tiktokHost);

        _tiktokCts = new CancellationTokenSource();
        var token = _tiktokCts.Token;
        _tiktokWorker = Task.Run(() => RunTikTokAsync(token), token);
        return true;
    }

    private async Task StopTikTokAsync()
    {
        var cts = _tiktokCts;
        var worker = _tiktokWorker;
        _tiktokCts = null;
        _tiktokWorker = null;
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
        PublishTikTokOffline();
    }

    /// <summary>Poll loop. Every exception is caught here so nothing escapes onto the thread pool.</summary>
    private async Task RunTikTokAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                await PollTikTokOnceAsync(token).ConfigureAwait(false);
                _tiktokFailures = 0;
                delay = TimeSpan.FromSeconds(_tiktokLive ? GetSettingValue<int>(TikTokSetting.LivePollSeconds) : GetSettingValue<int>(TikTokSetting.OfflinePollSeconds));
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _tiktokFailures = Math.Min(_tiktokFailures + 1, 10);
                delay = TikTokBackoff();
                Log($"TikTok: poll failed ({ex.GetType().Name}: {ex.Message}); retrying in {delay.TotalSeconds:0}s");
            }

            try { await Task.Delay(delay, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    private TimeSpan TikTokBackoff()
    {
        var max = TimeSpan.FromSeconds(GetSettingValue<int>(TikTokSetting.MaxBackoffSeconds));
        var seconds = TikTokMinBackoff.TotalSeconds * Math.Pow(2, _tiktokFailures - 1);
        return seconds > max.TotalSeconds ? max : TimeSpan.FromSeconds(seconds);
    }

    private async Task PollTikTokOnceAsync(CancellationToken token)
    {
        if (_tiktokRoomId is null)
        {
            _tiktokRoomId = await _tiktokClient.GetRoomIdAsync(_tiktokHost, token).ConfigureAwait(false);
            if (_tiktokRoomId is null)
            {
                await RefreshTikTokFollowersFromProfileAsync(token).ConfigureAwait(false);
                if (_tiktokLive) Log($"TikTok: @{_tiktokHost} is no longer live.");
                PublishTikTokOffline();
                return;
            }
        }

        var info = await _tiktokClient.GetRoomInfoAsync(_tiktokRoomId, token).ConfigureAwait(false);
        if (info is null) throw new InvalidOperationException("room info unavailable");

        if (info.Value.Followers is { } followers) ApplyTikTokFollowers(followers, FollowerSource.RoomInfo);
        else await RefreshTikTokFollowersFromProfileAsync(token).ConfigureAwait(false);

        if (!info.Value.IsLive)
        {
            if (_tiktokLive) Log($"TikTok: @{_tiktokHost} ended the stream.");
            _tiktokRoomId = null; // the next poll re-resolves; a new stream gets a new room id
            PublishTikTokOffline();
            return;
        }

        if (!_tiktokLive) Log($"TikTok: @{_tiktokHost} is live: {info.Value.Title}");
        PublishTikTokLive(info.Value);
    }

    private async Task RefreshTikTokFollowersFromProfileAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromMinutes(GetSettingValue<int>(TikTokSetting.FollowersRefreshMinutes));
        if (DateTime.UtcNow - _lastProfileFetch < interval) return;
        _lastProfileFetch = DateTime.UtcNow;

        try
        {
            var followers = await _tiktokClient.GetProfileFollowersAsync(_tiktokHost, token).ConfigureAwait(false);
            if (followers is { } count) ApplyTikTokFollowers(count, FollowerSource.ProfilePage);
            else Log("TikTok: follower count not found on the profile page; keeping the last known value.");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            Log($"TikTok: profile follower fetch failed ({ex.GetType().Name}: {ex.Message}); keeping the last known value.");
        }
    }

    private void ApplyTikTokFollowers(int followers, FollowerSource source)
    {
        if (_haveTikTokFollowers && followers > _tiktokFollowers) TriggerEvent(StreamEvent.Follow);
        if (!_haveTikTokFollowers) Log($"TikTok: followers {followers} ({source})");
        _tiktokFollowers = followers;
        _haveTikTokFollowers = true;
        SetVariableValue(VarTikTokFollowers, followers);
    }

    private void PublishTikTokLive(TikTokRoomInfo info)
    {
        if (_haveTikTokLikes && info.Likes > _tiktokLikes) TriggerEvent(StreamEvent.Like);
        _tiktokLikes = info.Likes;
        _haveTikTokLikes = true;
        _tiktokLive = true;
        _tiktokViewers = info.Viewers;

        SetVariableValue(VarTikTokLive, true);
        SetVariableValue(VarTikTokViewers, info.Viewers);
        SetVariableValue(VarTikTokLikes, info.Likes);
        SendParameter(StreamParameter.TikTokLive, true);
        SendParameter(StreamParameter.TikTokViewers, info.Viewers);
        PublishCombined();
    }

    private void PublishTikTokOffline()
    {
        _tiktokLive = false;
        _tiktokViewers = 0;
        _haveTikTokLikes = false;
        SetVariableValue(VarTikTokLive, false);
        SetVariableValue(VarTikTokViewers, 0);
        SetVariableValue(VarTikTokLikes, 0);
        SendParameter(StreamParameter.TikTokLive, false);
        SendParameter(StreamParameter.TikTokViewers, 0);
        PublishCombined();
    }

    private enum TikTokSetting { TikTokEnabled, Host, LivePollSeconds, OfflinePollSeconds, FollowersRefreshMinutes, MaxBackoffSeconds }
}
