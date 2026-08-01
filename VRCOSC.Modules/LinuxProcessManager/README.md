# Linux Process Manager Module

Allows starting, stopping, and restarting Linux host processes directly from avatar OSC parameters and flow nodes.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- Linux host environment.
- Process executable must be accessible in user PATH or absolute path.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| _None_ | — | No configurable settings for this module. | — |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| _None_ | — | — | No ChatBox variables provided. |

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
| `VRCOSC/Process/Start` | `bool` | `Read` | Set to true to launch process |
| `VRCOSC/Process/Stop` | `bool` | `Read` | Set to true to terminate process |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Start Linux Process** | Process Path (string), Arguments (string) | PID (int), Success (bool) | Launches executable on Linux host |
| **Stop Linux Process** | Process Name or PID (string) | Success (bool) | Terminates matching process on Linux host |
| **Is Process Running** | Process Name (string) | Is Running (bool) | Checks if matching process exists |

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
| _None_ | — | — | — |
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
| **VRCOSC/Process/Start** | `bool` | `Read` | `Set to true to launch process` |
| **VRCOSC/Process/Stop** | `bool` | `Read` | `Set to true to terminate process` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Start Linux Process** | `Process Path (string), Arguments (string)` | `PID (int), Success (bool)` | `Launches executable on Linux host` |
| **Stop Linux Process** | `Process Name or PID (string)` | `Success (bool)` | `Terminates matching process on Linux host` |
| **Is Process Running** | `Process Name (string)` | `Is Running (bool)` | `Checks if matching process exists` |
<!-- AUTOGEN:NODES:END -->
