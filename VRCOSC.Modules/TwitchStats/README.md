# Twitch Stats

Live status, game, title, viewers, followers and uptime of a Twitch channel for the ChatBox. Logs in through the Twitch device code flow.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Channel** | `TextBox` | `Twitch login name to watch. Leave empty to use the account you logged in with.` | `empty` |
| **Poll interval (seconds)** | `TextBox` | `How often Helix is asked for stream, channel and follower data` | `60` |
| **Client ID** | `TextBox` | `Twitch application client ID used for the device code login. The default is the official VRCOSC app; use your own if the login fails.` | `DefaultClientId` |
| **Forget saved login** | `Toggle` | `Drop the stored token on the next start and log in again through the device code flow` | `false` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
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
| **Live** | `live` | `{0} is live: {1}\n{2} | {3} viewers | {4}` | `Live state` |
| **Offline** | `offline` | `{0} is offline\n{1} followers` | `Offline state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Went live** | `wentlive` | `{0} went live: {1}` | `Triggered on Went live` |
| **Went offline** | `wentoffline` | `{0} went offline` | `Triggered on Went offline` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Twitch/Live** | `bool` | `Write` | `True while the channel is streaming` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
