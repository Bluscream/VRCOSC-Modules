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
