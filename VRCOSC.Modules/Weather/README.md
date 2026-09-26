# Open-Meteo Weather

Temperature, feels-like, wind, humidity, condition and a weather emoji from Open-Meteo. No API key needed.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Location** | `TextBox` | `Configure Location` | `"City name (e.g. \"Berlin\" or \"Berlin, DE\"` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Emoji** | `emoji` | `string` | `ChatBox variable Emoji` |
| **Condition** | `condition` | `string` | `ChatBox variable Condition` |
| **Temp C** | `tempc` | `float` | `ChatBox variable Temp C` |
| **Temp F** | `tempf` | `float` | `ChatBox variable Temp F` |
| **Feels Like C** | `feelslikec` | `float` | `ChatBox variable Feels Like C` |
| **Feels Like F** | `feelslikef` | `float` | `ChatBox variable Feels Like F` |
| **Humidity (%)** | `humidity` | `int` | `ChatBox variable Humidity (%)` |
| **Wind (speed + direction)** | `wind` | `string` | `ChatBox variable Wind (speed + direction)` |
| **Wind km/h** | `windkph` | `float` | `ChatBox variable Wind km/h` |
| **Wind mph** | `windmph` | `float` | `ChatBox variable Wind mph` |
| **Wind Direction** | `winddirection` | `string` | `ChatBox variable Wind Direction` |
| **Location Name** | `locationname` | `string` | `ChatBox variable Location Name` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `{0} {1}\n{2}C (feels {3}C)\n{4}` | `Default state` |
| **Unavailable** | `unavailable` | `Weather unavailable` | `Unavailable state` |
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
| **VRCOSC/Weather/Code** | `int` | `Write` | `WMO weather code (0 clear ... 99 thunderstorm with hail)` |
| **VRCOSC/Weather/TempC** | `float` | `Write` | `Air temperature in Celsius` |
| **VRCOSC/Weather/FeelsLikeC** | `float` | `Write` | `Apparent temperature in Celsius` |
| **VRCOSC/Weather/WindKph** | `float` | `Write` | `Wind speed at 10 m in km/h` |
| **VRCOSC/Weather/IsDay** | `bool` | `Write` | `Whether it is daytime at the location` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
