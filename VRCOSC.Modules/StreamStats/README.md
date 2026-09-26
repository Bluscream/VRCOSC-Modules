# Twitch Stats

Live status, game, title, viewers, followers and uptime of a Twitch channel for the ChatBox. Logs in through the Twitch device code flow.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Twitch enabled** | `Toggle` | `Watch the Twitch channel below (only runs when a channel is set)` | `true` |
| **Channel** | `TextBox` | `Twitch login name to watch` | `empty` |
| **Poll interval (seconds)** | `TextBox` | `How often Helix is asked for stream, channel and follower data` | `60` |
| **Client ID** | `TextBox` | `Twitch application client ID used for the device code login. The default is the official VRCOSC app; use your own if the login fails.` | `DefaultClientId` |
| **Forget saved login** | `Toggle` | `Drop the stored token on the next start and log in again through the device code flow` | `false` |
| **TikTok enabled** | `Toggle` | `Watch the TikTok host below (only runs when a host is set)` | `true` |
| **Host** | `TextBox` | `TikTok username of the streamer to watch (the @name, without the @)` | `empty` |
| **Poll interval while live (seconds)** | `Slider` | `How often viewers/likes are refreshed while the host is live` | `15, 5, 120, 5` |
| **Poll interval while offline (seconds)** | `Slider` | `How often to check whether the host went live` | `60, 15, 600, 15` |
| **Followers refresh (minutes)** | `Slider` | `How often the follower count is scraped from the public profile page (only used when the live room does not report it)` | `5, 1, 60` |
| **Max reconnect backoff (seconds)** | `Slider` | `Upper bound for the exponential delay between retries after TikTok errors or rate limits` | `300, 30, 900, 30` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Live (any platform)** | `VarStreamLive` | `bool` | `ChatBox variable Live (any platform)` |
| **Viewers (all platforms)** | `VarStreamViewers` | `int` | `ChatBox variable Viewers (all platforms)` |
| **Live platform(s)** | `VarStreamPlatform` | `string` | `ChatBox variable Live platform(s)` |
| **Twitch Live** | `VarTwitchLive` | `bool` | `ChatBox variable Twitch Live` |
| **Twitch Channel (display name)** | `VarTwitchChannel` | `string` | `ChatBox variable Twitch Channel (display name)` |
| **Twitch Game** | `VarTwitchGame` | `string` | `ChatBox variable Twitch Game` |
| **Twitch Title** | `VarTwitchTitle` | `string` | `ChatBox variable Twitch Title` |
| **Twitch Viewers** | `VarTwitchViewers` | `int` | `ChatBox variable Twitch Viewers` |
| **Twitch Followers** | `VarTwitchFollowers` | `int` | `ChatBox variable Twitch Followers` |
| **Twitch Uptime (h:mm)** | `VarTwitchUptime` | `string` | `ChatBox variable Twitch Uptime (h:mm)` |
| **TikTok Host** | `VarTikTokHost` | `string` | `ChatBox variable TikTok Host` |
| **TikTok Viewers** | `VarTikTokViewers` | `int` | `ChatBox variable TikTok Viewers` |
| **TikTok Likes** | `VarTikTokLikes` | `int` | `ChatBox variable TikTok Likes` |
| **TikTok Followers** | `VarTikTokFollowers` | `int` | `ChatBox variable TikTok Followers` |
| **TikTok Live** | `VarTikTokLive` | `bool` | `ChatBox variable TikTok Live` |
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
