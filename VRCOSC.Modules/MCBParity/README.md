# MagicChatbox Parity

Weather feels-like/wind/emoji as ChatBox variables (MagicChatbox placeholder parity)

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **weatherapi.com API key** | `TextBox` | `Free key from https://www.weatherapi.com. Leave empty to use Open-Meteo instead (no key needed, but no weatherapi condition texts).` | `empty` |
| **Weather refresh (minutes)** | `Slider` | `How often the weather is re-fetched.` | `10, 1, 60, 1` |
| **Temperature unit** | `Dropdown` | `Unit for the temperature and feels-like variables.` | `TemperatureUnit.Celsius` |
| **Wind speed unit** | `Dropdown` | `Unit for the wind variable.` | `WindUnit.Kmh` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
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
| **Default** | `default` | `{0} {1} (feels {2})\n{3}` | `Default state` |
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
