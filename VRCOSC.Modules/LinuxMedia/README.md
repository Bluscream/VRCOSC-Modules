# Linux Media Module

Integrates with Linux MPRIS Media Players via D-Bus and vrcosc_mpris_query.sh script for player control and track info in ChatBox clips.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- Linux host with D-Bus session bus.
- MPRIS-compliant media player running (Spotify, VLC, Firefox, Rhythmbox, MPV).

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| _None_ | — | No configurable settings for this module. | — |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Title** | `title` | `string` | Currently playing track title |
| **Artist** | `artist` | `string` | Currently playing artist name |
| **Album** | `album` | `string` | Currently playing album title |
| **Player** | `player` | `string` | Active media player name |
| **Status** | `status` | `string` | Playback status (Playing, Paused, Stopped) |
| **Position** | `position` | `float` | Track position in seconds |
| **Duration** | `duration` | `float` | Track duration in seconds |
| **Progress Visual** | `progressvisual` | `ProgressClipVariable` | Visual progress bar variable for ChatBox clips |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Playing** | `playing` | `🎵 {0} - {1}` | Media playing |
| **Paused** | `paused` | `⏸️ {0} - {1}` | Media paused |
| **Stopped** | `stopped` | `⏹️ Media Stopped` | Media stopped |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Media Changed** | `onmediachanged` | `Now Playing: {0} - {1}` | Triggered when active track changes |
| **On Playback State Changed** | `onplaybackstatechanged` | `Media State: {0}` | Triggered when play/pause state changes |
| **On Progress Updated** | `onprogressupdated` | `Progress: {0}` | Triggered periodically as track progresses |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/Media/Playing` | `bool` | `Write` | True if media is actively playing |
| `VRCOSC/Media/Title` | `string` | `Write` | Current track title |
| `VRCOSC/Media/Artist` | `string` | `Write` | Current track artist |
| `VRCOSC/Media/Progress` | `float` | `Write` | Normalized track progress (0.0 to 1.0) |
| `VRCOSC/Media/Volume` | `float` | `Write` | Current player volume level |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Linux Media Play** | Flow trigger | Success (bool) | Starts or resumes playback |
| **Linux Media Pause** | Flow trigger | Success (bool) | Pauses playback |
| **Linux Media Next** | Flow trigger | Success (bool) | Skips to next track |
| **Linux Media Previous** | Flow trigger | Success (bool) | Skips to previous track |
| **Linux Media Stop** | Flow trigger | Success (bool) | Stops playback |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| _None_ | — | — | — |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Title** | `title` | `string` | `Currently playing track title` |
| **Artist** | `artist` | `string` | `Currently playing artist name` |
| **Album** | `album` | `string` | `Currently playing album title` |
| **Player** | `player` | `string` | `Active media player name` |
| **Status** | `status` | `string` | `Playback status (Playing, Paused, Stopped)` |
| **Position** | `position` | `float` | `Track position in seconds` |
| **Duration** | `duration` | `float` | `Track duration in seconds` |
| **Progress Visual** | `progressvisual` | `ProgressClipVariable` | `Visual progress bar variable for ChatBox clips` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Playing** | `playing` | `🎵 {0} - {1}` | `Media playing` |
| **Paused** | `paused` | `⏸️ {0} - {1}` | `Media paused` |
| **Stopped** | `stopped` | `⏹️ Media Stopped` | `Media stopped` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Media Changed** | `onmediachanged` | `Now Playing: {0} - {1}` | `Triggered when active track changes` |
| **On Playback State Changed** | `onplaybackstatechanged` | `Media State: {0}` | `Triggered when play/pause state changes` |
| **On Progress Updated** | `onprogressupdated` | `Progress: {0}` | `Triggered periodically as track progresses` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Media/Playing** | `bool` | `Write` | `True if media is actively playing` |
| **VRCOSC/Media/Title** | `string` | `Write` | `Current track title` |
| **VRCOSC/Media/Artist** | `string` | `Write` | `Current track artist` |
| **VRCOSC/Media/Progress** | `float` | `Write` | `Normalized track progress (0.0 to 1.0)` |
| **VRCOSC/Media/Volume** | `float` | `Write` | `Current player volume level` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Linux Media Play** | `Flow trigger` | `Success (bool)` | `Starts or resumes playback` |
| **Linux Media Pause** | `Flow trigger` | `Success (bool)` | `Pauses playback` |
| **Linux Media Next** | `Flow trigger` | `Success (bool)` | `Skips to next track` |
| **Linux Media Previous** | `Flow trigger` | `Success (bool)` | `Skips to previous track` |
| **Linux Media Stop** | `Flow trigger` | `Success (bool)` | `Stops playback` |
<!-- AUTOGEN:NODES:END -->
