# Stream Stats

MagicChatbox-parity stream stats for the ChatBox in one module: a **Twitch** channel (Helix,
device code login) and a **TikTok LIVE** host (public pages, no login). Each platform has its
own settings group and Enabled toggle and only runs when its channel/host is set; both can run
at the same time.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## ChatBox keys

Module id: `streamstatsmodule` (placeholders look like `{bluscream.vrcosc.modules.streamstatsmodule_<key>}`).

- Combined: `stream_live` (bool, any platform live), `stream_viewers` (int, sum of the live
  platforms), `stream_platform` (string: `Twitch`, `TikTok`, `Twitch+TikTok` or empty)
- Twitch: `twitch_live` (bool), `twitch_channel`, `twitch_game`, `twitch_title` (string),
  `twitch_viewers`, `twitch_followers` (int), `twitch_uptime` (string, `h:mm`)
- TikTok: `tiktok_host` (string), `tiktok_viewers`, `tiktok_likes`, `tiktok_followers` (int),
  `tiktok_live` (bool)
- States: `live` (either platform live), `offline`, `unauthenticated` (Twitch enabled but not
  logged in while TikTok is not live)
- Events: `wentlive` / `wentoffline` (Twitch), `follow` / `like` (TikTok counter increased)

## Twitch login

Modules cannot share the official Twitch module's token, so this module logs in on its own
through the **device code grant** flow: on the first start it logs a line like
`Twitch login: open https://www.twitch.tv/activate and enter the code XXXX-XXXX`. Enter the code in a
browser while logged into Twitch; the access and refresh tokens are then stored in the module's
persistent data (`twitch_access_token` / `twitch_refresh_token`) and refreshed automatically.

- **Client ID** defaults to the official VRCOSC Twitch app. Any Twitch app with the device code
  grant enabled works; use your own if the login fails.
- **Access token (manual)**: pasting a user access token skips the device code flow. The
  follower count needs the `moderator:read:followers` scope; without it the count stays 0 and
  everything else still works.
- **Forget saved login**: drops the stored tokens on the next start. Turn it off again
  afterwards or every start will ask for a new login.
- A `401` from Helix (revoked token) drops the token and starts a new login; `429` waits until
  the rate-limit window resets.

## TikTok: how it works (and why polling)

| Data | Source |
|---|---|
| Room id | `https://www.tiktok.com/@<host>/live` page (`"roomId":"..."`) |
| Live status, viewers, likes, exact follower count | `https://webcast.tiktok.com/webcast/room/info/?aid=1988&room_id=<id>` |
| Follower count fallback (rounded by TikTok, e.g. `95900000`) | `https://www.tiktok.com/@<host>` profile page (`"followerCount":N`) |

Options evaluated on 2026-09-26:

- **TikTokLiveSharp (NuGet)**: latest version 0.1.4, published 2022-08, `netstandard2.0`,
  depends on Newtonsoft.Json + protobuf-net. It predates TikTok's signed websocket URLs, so
  its websocket connection no longer works against current TikTok, and it would drag two
  extra assemblies into the Costura bundle for nothing. Rejected.
- **Own webcast websocket**: the websocket URL must be signed by a third-party sign server
  (Euler Stream) with an API key, and the protobuf message schema changes without notice.
  Not something a chatbox module should depend on. Rejected.
- **Polling the public room-info endpoint** (this module): stable JSON, exact counters, no
  auth. The only cost is granularity, see below.

### TikTok limitations

- Counters update on the poll interval (default 15 s while live, 60 s while offline), not
  per event. Likes/followers jump in steps.
- The **Follow** and **Like** ChatBox events fire when the polled counter *increases*;
  they are not per-user events and carry no username.
- There is **no Gift event**: gifts are only available over the signed websocket stream.
- TikTok rate-limits or serves a challenge page from time to time. Every failure is logged,
  the module keeps the last known values and retries with exponential backoff (5 s doubling
  up to the configured maximum). Nothing crashes the module or VRCOSC.
- TikTok can change its page markup or endpoint at any time; if the room id or follower
  count stop being found, the module logs it and reports offline.
- **Host** accepts the @name with or without the @; a profile URL also works.

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **TikTok enabled** | `Toggle` | `Watch the TikTok host below (only runs when a host is set)` | `true` |
| **Host** | `TextBox` | `TikTok username of the streamer to watch (the @name, without the @)` | `empty` |
| **Poll interval while live (seconds)** | `Slider` | `How often viewers/likes are refreshed while the host is live` | `15, 5, 120, 5` |
| **Poll interval while offline (seconds)** | `Slider` | `How often to check whether the host went live` | `60, 15, 600, 15` |
| **Followers refresh (minutes)** | `Slider` | `How often the follower count is scraped from the public profile page (only used when the live room does not report it)` | `5, 1, 60` |
| **Max reconnect backoff (seconds)** | `Slider` | `Upper bound for the exponential delay between retries after TikTok errors or rate limits` | `300, 30, 900, 30` |
| **Twitch enabled** | `Toggle` | `Watch the Twitch channel below (only runs when a channel is set)` | `true` |
| **Channel** | `TextBox` | `Twitch login name to watch` | `empty` |
| **Poll interval (seconds)** | `TextBox` | `How often Helix is asked for stream, channel and follower data` | `60` |
| **Client ID** | `TextBox` | `Twitch application client ID used for the device code login. The default is the official VRCOSC app; use your own if the login fails.` | `DefaultClientId` |
| **Forget saved login** | `Toggle` | `Drop the stored token on the next start and log in again through the device code flow` | `false` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **TikTok Host** | `VarTikTokHost` | `string` | `ChatBox variable TikTok Host` |
| **TikTok Viewers** | `VarTikTokViewers` | `int` | `ChatBox variable TikTok Viewers` |
| **TikTok Likes** | `VarTikTokLikes` | `int` | `ChatBox variable TikTok Likes` |
| **TikTok Followers** | `VarTikTokFollowers` | `int` | `ChatBox variable TikTok Followers` |
| **TikTok Live** | `VarTikTokLive` | `bool` | `ChatBox variable TikTok Live` |
| **Twitch Live** | `VarTwitchLive` | `bool` | `ChatBox variable Twitch Live` |
| **Twitch Channel (display name)** | `VarTwitchChannel` | `string` | `ChatBox variable Twitch Channel (display name)` |
| **Twitch Game** | `VarTwitchGame` | `string` | `ChatBox variable Twitch Game` |
| **Twitch Title** | `VarTwitchTitle` | `string` | `ChatBox variable Twitch Title` |
| **Twitch Viewers** | `VarTwitchViewers` | `int` | `ChatBox variable Twitch Viewers` |
| **Twitch Followers** | `VarTwitchFollowers` | `int` | `ChatBox variable Twitch Followers` |
| **Twitch Uptime (h:mm)** | `VarTwitchUptime` | `string` | `ChatBox variable Twitch Uptime (h:mm)` |
| **Live (any platform)** | `VarStreamLive` | `bool` | `ChatBox variable Live (any platform)` |
| **Viewers (all platforms)** | `VarStreamViewers` | `int` | `ChatBox variable Viewers (all platforms)` |
| **Live platform(s)** | `VarStreamPlatform` | `string` | `ChatBox variable Live platform(s)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Live** | `live` | `Live on {0} | {1} viewers` | `Live state` |
| **Offline** | `offline` | `Offline` | `Offline state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Twitch went live** | `wentlive` | `{0} went live: {1}` | `Triggered on Twitch went live` |
| **Twitch went offline** | `wentoffline` | `{0} went offline` | `Triggered on Twitch went offline` |
| **TikTok new follower(s)** | `follow` | `\U0001F465 New follower! {0} followers` | `Triggered on TikTok new follower(s)` |
| **TikTok new like(s)** | `like` | `❤ {0} likes` | `Triggered on TikTok new like(s)` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Twitch/Live** | `bool` | `Write` | `True while the Twitch channel is streaming` |
| **VRCOSC/TikTok/Live** | `bool` | `Write` | `Whether the TikTok host is currently live` |
| **VRCOSC/TikTok/Viewers** | `int` | `Write` | `Current TikTok viewer count (0 while offline)` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
