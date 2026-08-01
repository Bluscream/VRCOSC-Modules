# Desktop FPS Module

Monitors VRChat desktop / window FPS using high-precision process frame timing and performance counters.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- VRCOSC 2026.501.0 or later
- Windows host or Wine/Proton environment running VRChat.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| _None_ | — | No configurable settings for this module. | — |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **FPS** | `fps` | `int` | Current VRChat process rendering FPS |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| _None_ | — | — | No ChatBox states provided. |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | No ChatBox events provided. |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/Desktop/FPS` | `int` | `Write` | Current VRChat process frame rate |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get Desktop FPS** | Flow trigger | FPS (int) | Returns current desktop rendering FPS |

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
| **FPS** | `fps` | `int` | `Current VRChat process rendering FPS` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | — |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Desktop/FPS** | `int` | `Write` | `Current VRChat process frame rate` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get Desktop FPS** | `Flow trigger` | `FPS (int)` | `Returns current desktop rendering FPS` |
<!-- AUTOGEN:NODES:END -->

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
| **FPS** | `fps` | `int` | `ChatBox variable FPS` |
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
| **Info/FPS** | `int` | `Write` | `Current VRChat FPS` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get F P S** | `Flow trigger` | `Output` | `Node node for Get F P S` |
<!-- NODES_TABLE_END -->
