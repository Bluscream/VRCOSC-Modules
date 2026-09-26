// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox-parity stream stats for the ChatBox, covering Twitch (Helix, device code
// login) and TikTok LIVE (public endpoints, no login) in one module. Each platform keeps
// its own poll loop in a partial file; this file owns the shared surface (settings groups,
// variables, states, events) and the combined stream_* variables.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.StreamStats;

[ModuleTitle("Stream Stats")]
[ModuleDescription("Live status, viewers, followers and more for a Twitch channel (device code login) and a TikTok LIVE host (public pages, no login) for the ChatBox, plus combined stream_* variables")]
[ModuleType(ModuleType.Integrations)]
public partial class StreamStatsModule : Module
{
    private const string PlatformTwitch = "Twitch";
    private const string PlatformTikTok = "TikTok";

    // Combined keys; the per-platform keys live in the platform partials.
    private const string VarStreamLive = "stream_live";
    private const string VarStreamViewers = "stream_viewers";
    private const string VarStreamPlatform = "stream_platform";

    private readonly object _combinedLock = new();

    protected override void OnPreLoad()
    {
        CreateTwitchSettings();
        CreateTikTokSettings();

        CreateGroup(PlatformTwitch, "Twitch channel and Helix polling; watch the log for the login URL and code", TwitchSetting.TwitchEnabled, TwitchSetting.Channel, TwitchSetting.PollInterval, TwitchSetting.ClientId, TwitchSetting.ManualToken, TwitchSetting.ForgetToken);
        CreateGroup(PlatformTikTok, "TikTok LIVE host and public-endpoint polling", TikTokSetting.TikTokEnabled, TikTokSetting.Host, TikTokSetting.LivePollSeconds, TikTokSetting.OfflinePollSeconds, TikTokSetting.FollowersRefreshMinutes, TikTokSetting.MaxBackoffSeconds);

        RegisterParameter<bool>(StreamParameter.TwitchLive, "VRCOSC/Twitch/Live", ParameterMode.Write, "Twitch Live", "True while the Twitch channel is streaming");
        RegisterParameter<bool>(StreamParameter.TikTokLive, "VRCOSC/TikTok/Live", ParameterMode.Write, "TikTok Live", "Whether the TikTok host is currently live");
        RegisterParameter<int>(StreamParameter.TikTokViewers, "VRCOSC/TikTok/Viewers", ParameterMode.Write, "TikTok Viewers", "Current TikTok viewer count (0 while offline)");
    }

    protected override void OnPostLoad()
    {
        CreateVariable<bool>(VarStreamLive, "Live (any platform)");
        var streamViewers = CreateVariable<int>(VarStreamViewers, "Viewers (all platforms)")!;
        var streamPlatform = CreateVariable<string>(VarStreamPlatform, "Live platform(s)")!;

        var twitch = CreateTwitchVariables();
        var tiktok = CreateTikTokVariables();

        CreateState(StreamState.Live, "Live", "Live on {0} | {1} viewers", new[] { streamPlatform, streamViewers });
        CreateState(StreamState.Offline, "Offline", "Offline");
        CreateState(StreamState.Unauthenticated, "Unauthenticated", string.Empty);

        CreateEvent(StreamEvent.WentLive, "Twitch went live", "{0} went live: {1}", new[] { twitch.Channel, twitch.Game });
        CreateEvent(StreamEvent.WentOffline, "Twitch went offline", "{0} went offline", new[] { twitch.Channel });
        CreateEvent(StreamEvent.Follow, "TikTok new follower(s)", "\U0001F465 New follower! {0} followers", new[] { tiktok.Followers });
        CreateEvent(StreamEvent.Like, "TikTok new like(s)", "❤ {0} likes", new[] { tiktok.Likes });
    }

    protected override Task<bool> OnModuleStart()
    {
        var twitch = StartTwitch();
        var tiktok = StartTikTok();

        if (!twitch && !tiktok)
        {
            Log("Nothing to watch: enable Twitch and set a channel, or enable TikTok and set a host.");
            return Task.FromResult(false);
        }

        PublishCombined();
        return Task.FromResult(true);
    }

    protected override async Task OnModuleStop()
    {
        StopTwitch();
        await StopTikTokAsync().ConfigureAwait(false);
        PublishCombined();
    }

    /// <summary>Recomputes the combined variables and the module state from both platforms; safe from any thread.</summary>
    private void PublishCombined()
    {
        lock (_combinedLock)
        {
            var twitchLive = _twitchLive;
            var tiktokLive = _tiktokLive;
            var live = twitchLive || tiktokLive;

            var platform = (twitchLive, tiktokLive) switch
            {
                (true, true) => $"{PlatformTwitch}+{PlatformTikTok}",
                (true, false) => PlatformTwitch,
                (false, true) => PlatformTikTok,
                _ => string.Empty
            };

            SetVariableValue(VarStreamLive, live);
            SetVariableValue(VarStreamViewers, (twitchLive ? _twitchViewers : 0) + (tiktokLive ? _tiktokViewers : 0));
            SetVariableValue(VarStreamPlatform, platform);

            if (live) ChangeState(StreamState.Live);
            else if (_twitchActive && !_twitchAuthenticated) ChangeState(StreamState.Unauthenticated);
            else ChangeState(StreamState.Offline);
        }
    }

    private enum StreamParameter { TwitchLive, TikTokLive, TikTokViewers }

    private enum StreamState { Live, Offline, Unauthenticated }

    private enum StreamEvent { WentLive, WentOffline, Follow, Like }
}
