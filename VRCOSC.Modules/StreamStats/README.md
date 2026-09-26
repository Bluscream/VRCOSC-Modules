# Twitch Stats

Live status, game, title, viewers, followers and uptime of a Twitch channel for the ChatBox. Logs in through the Twitch device code flow.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Host** | `TextBox` | `TikTok username of the streamer to watch (the @name, without the @)` | `empty` |
| **Poll interval while live (seconds)** | `Slider` | `How often viewers/likes are refreshed while the host is live` | `15, 5, 120, 5` |
| **Poll interval while offline (seconds)** | `Slider` | `How often to check whether the host went live` | `60, 15, 600, 15` |
| **Followers refresh (minutes)** | `Slider` | `How often the follower count is scraped from the public profile page (only used when the live room does not report it)` | `5, 1, 60` |
| **Max reconnect backoff (seconds)** | `Slider` | `Upper bound for the exponential delay between retries after TikTok errors or rate limits` | `300, 30, 900, 30` |
| **Channel** | `TextBox` | `Twitch login name to watch. Leave empty to use the account you logged in with.` | `empty` |
| **Poll interval (seconds)** | `TextBox` | `How often Helix is asked for stream, channel and follower data` | `60` |
| **Client ID** | `TextBox` | `Twitch application client ID used for the device code login. The default is the official VRCOSC app; use your own if the login fails.` | `DefaultClientId` |
| **Forget saved login** | `Toggle` | `Drop the stored token on the next start and log in again through the device code flow` | `false` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Host** | `VarHost` | `string` | `ChatBox variable Host` |
| **Viewers** | `VarViewers` | `int` | `ChatBox variable Viewers` |
| **Likes** | `VarLikes` | `int` | `ChatBox variable Likes` |
| **Followers** | `VarFollowers` | `int` | `ChatBox variable Followers` |
| **Live** | `VarLive` | `bool` | `ChatBox variable Live` |
| **Live** | `VarLive` | `bool` | `ChatBox variable Live` |
| **Channel (display name)** | `VarChannel` | `string` | `ChatBox variable Channel (display name)` |
| **Game** | `VarGame` | `string` | `ChatBox variable Game` |
| **Title** | `VarTitle` | `string` | `ChatBox variable Title` |
| **Viewers** | `VarViewers` | `int` | `ChatBox variable Viewers` |
| **Followers** | `VarFollowers` | `int` | `ChatBox variable Followers` |
| **Uptime (h:mm)** | `VarUptime` | `string` | `ChatBox variable Uptime (h:mm)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Live** | `live` | `\U0001F534 @{0} LIVE\n\U0001F441 {1} ❤ {2} \U0001F465 {3}` | `Live state` |
| **Offline** | `offline` | `@{0} offline · \U0001F465 {1}` | `Offline state` |
| **Live** | `live` | `{0} is live: {1}\n{2} | {3} viewers | {4}` | `Live state` |
| **Offline** | `offline` | `{0} is offline\n{1} followers` | `Offline state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **New follower(s)** | `follow` | `\U0001F465 New follower! {0} followers` | `Triggered on New follower(s)` |
| **New like(s)** | `like` | `❤ {0} likes` | `Triggered on New like(s)` |
| **Went live** | `wentlive` | `{0} went live: {1}` | `Triggered on Went live` |
| **Went offline** | `wentoffline` | `{0} went offline` | `Triggered on Went offline` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/TikTok/Live** | `bool` | `Write` | `Whether the host is currently live` |
| **VRCOSC/TikTok/Viewers** | `int` | `Write` | `Current viewer count (0 while offline)` |
| **VRCOSC/Twitch/Live** | `bool` | `Write` | `True while the channel is streaming` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
