# TikTok Live

MagicChatbox-parity TikTok LIVE stats for the ChatBox: host, viewers, likes, followers and a
live flag. Polls TikTok's **public, unauthenticated** endpoints; no login, cookies or API key.

## How it works (and why polling)

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

## Limitations

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

## ChatBox

- Variables: `tiktok_host` (string), `tiktok_viewers` (int), `tiktok_likes` (int),
  `tiktok_followers` (int), `tiktok_live` (bool)
- States: `live`, `offline`
- Events: `follow`, `like`

## Parameters

- `VRCOSC/TikTok/Live` (bool, write)
- `VRCOSC/TikTok/Viewers` (int, write)

## Settings

- **Host**: the @name (with or without the @, a profile URL also works)
- **Poll interval while live** (5-120 s, default 15)
- **Poll interval while offline** (15-600 s, default 60)
- **Followers refresh** (1-60 min, default 5) - only used when the room does not report followers
- **Max reconnect backoff** (30-900 s, default 300)

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| _None_ | — | — | — |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | — |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
