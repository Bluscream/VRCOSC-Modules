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
| **VRCOSC/ProcessManager/Start/*** | `bool` | `Read` | `Becoming true will start the process named in the '*' that you set on your avatar\nFor example, on your avatar you put: VRCOSC/ProcessManager/Start/obs-studio` |
| **VRCOSC/ProcessManager/Stop/*** | `bool` | `Read` | `Becoming true will stop the process named in the '*' that you set on your avatar\nFor example, on your avatar you put: VRCOSC/ProcessManager/Stop/obs-studio` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
