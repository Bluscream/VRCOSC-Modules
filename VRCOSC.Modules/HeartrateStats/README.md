# Heartrate Stats

Heart rate with session **min/max** and a **trend arrow** as ChatBox variables (MagicChatbox parity). VRCOSC modules cannot read each other's variables, so this module cannot sit on top of the official Pulsoid/HypeRate modules; it speaks the same protocols itself and sends the same `VRCOSC/Heartrate/*` avatar parameters, so existing prefabs keep working. Run it **instead of** the official heartrate module, not next to it.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Providers

| Provider | What you need |
|---|---|
| **Pulsoid** | An access token from https://pulsoid.net/ui/keys with the *Data: Heart Rate: Read* scope. |
| **HypeRate** | Your session ID from the HypeRate app plus an application API key (HypeRate issues these on request at https://www.hyperate.io/api; the official module ships its own key, which is not public). |
| **Osc** | Nothing. Point any other tool at VRChat's OSC input with the parameter name from *OSC Input Parameter* (int or float bpm). The module reads it from VRChat's parameter stream, so with an avatar parameter of that name it also works for values set on the avatar. |

Websocket providers reconnect automatically with exponential backoff (2 s doubling to 60 s). A provider counts as *disconnected* after 30 s without a reading, regardless of socket state.

## Statistics

- `heartrate_min` / `heartrate_max` reset on module start, via the **Reset Min/Max** toggle (flip it either way) and via the `VRCOSC/Heartrate/ResetStats` parameter.
- `heartrate_trend` is `↑`, `→` or `↓`: the mean of the newer half of the trend window compared with the older half; a difference of at least the threshold (bpm) gives an arrow.
- MagicChatbox placeholders map to `{bluscream.vrcosc.modules.heartratestatsmodule_<key>}` with the lookup keys listed below.

Smoothing of the Value/Average parameters (an option of the official module) is not implemented; values jump to the latest reading.

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Provider** | `Dropdown` | `Where the heart rate comes from` | `HeartrateProvider.Pulsoid` |
| **Pulsoid Access Token** | `TextBox` | `Token from https://pulsoid.net/ui/keys (needs the Data:Heart Rate:Read scope)` | `empty` |
| **HypeRate Session ID** | `TextBox` | `The ID shown in the HypeRate app (the characters at the end of your app.hyperate.io link)` | `empty` |
| **HypeRate API Key** | `TextBox` | `Application key issued by HypeRate (request one at https://www.hyperate.io/api)` | `empty` |
| **OSC Input Parameter** | `TextBox` | `Parameter name another tool writes the heart rate to (int or float bpm), e.g. HR or VRCOSC/HeartrateStats/Input` | `"VRCOSC/HeartrateStats/Input"` |
| **Trend Window (s)** | `Slider` | `How far back the trend arrow looks` | `30, 5, 300, 5` |
| **Trend Threshold (bpm)** | `Slider` | `Minimum change across the window before the arrow leaves flat` | `3, 1, 30` |
| **Average Period (s)** | `TextBox` | `Period used for the Average parameter and variable` | `10` |
| **Reset Min/Max** | `Toggle` | `Flip this toggle (either way) to reset the session minimum and maximum` | `false` |
| **Normalised Lowerbound** | `TextBox` | `The bpm mapped to 0 on the Normalised parameter` | `0` |
| **Normalised Upperbound** | `TextBox` | `The bpm mapped to 1 on the Normalised parameter` | `240` |
| **Beat Mode** | `Toggle` | `Whether the Beat parameter toggles its value (off) or becomes true for one update (on) on every beat` | `false` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Heartrate** | `heartrate` | `int` | `ChatBox variable Heartrate` |
| **Session Min** | `heartrate_min` | `int` | `ChatBox variable Session Min` |
| **Session Max** | `heartrate_max` | `int` | `ChatBox variable Session Max` |
| **Trend Arrow** | `heartrate_trend` | `string` | `ChatBox variable Trend Arrow` |
| **Average** | `heartrate_average` | `int` | `ChatBox variable Average` |
| **Connected** | `heartrate_connected` | `bool` | `ChatBox variable Connected` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Connected** | `connected` | `❤ {0} {3} ({1}-{2})` | `Connected state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Connected** | `connected` | `❤ Heartrate connected` | `Triggered on Connected` |
| **Disconnected** | `disconnected` | `❤ Heartrate disconnected` | `Triggered on Disconnected` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Heartrate/Connected** | `bool` | `Write` | `Whether this module is connected and receiving values` |
| **VRCOSC/Heartrate/Value** | `int` | `Write` | `The value of your heartrate` |
| **VRCOSC/Heartrate/Normalised** | `float` | `Write` | `The heartrate value normalised from the set bounds to 0-1` |
| **VRCOSC/Heartrate/Average** | `int` | `Write` | `The average of your heartrate` |
| **VRCOSC/Heartrate/Beat** | `bool` | `ReadWrite` | `Toggles value OR becomes true for 1 update (depending on the setting) when your heart beats` |
| **VRCOSC/Heartrate/Enabled** | `bool` | `Write` | `Whether this module is connected and receiving values` |
| **VRCOSC/Heartrate/Units** | `float` | `Write` | `The units digit 0-9 mapped to a float` |
| **VRCOSC/Heartrate/Tens** | `float` | `Write` | `The tens digit 0-9 mapped to a float` |
| **VRCOSC/Heartrate/Hundreds** | `float` | `Write` | `The hundreds digit 0-9 mapped to a float` |
| **VRCOSC/Heartrate/Min** | `int` | `Write` | `Lowest heartrate this session` |
| **VRCOSC/Heartrate/Max** | `int` | `Write` | `Highest heartrate this session` |
| **VRCOSC/Heartrate/Trend** | `int` | `Write` | `-1 falling, 0 flat, 1 rising` |
| **VRCOSC/Heartrate/ResetStats** | `bool` | `Read` | `Becoming true resets the session minimum and maximum` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
