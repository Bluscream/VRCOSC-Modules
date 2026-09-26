# MagicChatbox Parity

Weather feels-like/wind/emoji and VR reprojection/dropped-frame estimates as ChatBox variables (MagicChatbox placeholder parity)

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **VR frame estimates** | `Toggle` | `Keep the shared OpenXR session open to estimate reprojection and dropped frames from the runtime's display-period schedule. Off leaves both variables at 0.` | `true` |
| **weatherapi.com API key** | `TextBox` | `Free key from https://www.weatherapi.com. Leave empty to use Open-Meteo instead (no key needed, but no weatherapi condition texts).` | `empty` |
| **Weather refresh (minutes)** | `Slider` | `How often the weather is re-fetched.` | `10, 1, 60, 1` |
| **Temperature unit** | `Dropdown` | `Unit for the temperature and feels-like variables.` | `TemperatureUnit.Celsius` |
| **Wind speed unit** | `Dropdown` | `Unit for the wind variable.` | `WindUnit.Kmh` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **VR Reprojection (% of frames, last second)** | `vr_reprojection` | `int` | `ChatBox variable VR Reprojection (% of frames, last second)` |
| **VR Dropped Frames (per minute)** | `vr_dropped_frames` | `int` | `ChatBox variable VR Dropped Frames (per minute)` |
| **Weather Temperature (with unit)** | `weather_temp` | `string` | `ChatBox variable Weather Temperature (with unit)` |
| **Weather Feels Like (with unit)** | `weather_feels_like` | `string` | `ChatBox variable Weather Feels Like (with unit)` |
| **Weather Wind (speed + direction)** | `weather_wind` | `string` | `ChatBox variable Weather Wind (speed + direction)` |
| **Weather Emoji** | `weather_emoji` | `string` | `ChatBox variable Weather Emoji` |
| **Weather Condition** | `weather_condition` | `string` | `ChatBox variable Weather Condition` |
| **Weather Humidity (%)** | `weather_humidity` | `int` | `ChatBox variable Weather Humidity (%)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `{0} {1} (feels {2})\n{3}\nReproj {4}% · Dropped {5}/min` | `Default state` |
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
