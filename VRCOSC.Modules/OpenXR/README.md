# OpenXR Modules

Cross-platform OpenXR integration providing runtime statistics (FPS, frame timing, VRAM), hand tracking gestures (XR_EXT_hand_tracking), and haptic controller feedback via native openxr_loader.dll.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- OpenXR runtime active (SteamVR, Monado, WiVRn, Oculus).
- Native `openxr_loader.dll` deployed next to VRCOSC executable (automatically handled by `update.sh`).

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **EnableHaptics** | `Toggle` | Enable haptic feedback on OpenXR controllers | `true` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Runtime Name** | `runtimename` | `string` | Active OpenXR runtime name (e.g. SteamVR, Monado) |
| **Frame Rate** | `framerate` | `float` | OpenXR compositor frame rate |
| **Frame Time Ms** | `frametimems` | `float` | OpenXR compositor frame time in ms |
| **System Name** | `systemname` | `string` | VR Headset system name |
| **Headpose Valid** | `headposevalid` | `bool` | True if HMD pose tracking is valid |
| **Left Hand Valid** | `lefthandvalid` | `bool` | True if left hand controller/tracking is valid |
| **Right Hand Valid** | `righthandvalid` | `bool` | True if right hand controller/tracking is valid |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disabled** | `disabled` | `OpenXR Disabled` | OpenXR inactive |
| **Searching** | `searching` | `OpenXR Searching...` | Connecting to OpenXR instance |
| **Active** | `active` | `OpenXR Active ({0} FPS)` | OpenXR session active |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | No ChatBox events provided. |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/OpenXR/FrameRate` | `float` | `Write` | OpenXR compositor FPS |
| `VRCOSC/OpenXR/FrameTimeMs` | `float` | `Write` | OpenXR compositor frame time (ms) |
| `VRCOSC/OpenXR/Gestures/Pinch/Left` | `float` | `Write` | Left hand pinch gesture strength (0.0 - 1.0) |
| `VRCOSC/OpenXR/Gestures/Pinch/Right` | `float` | `Write` | Right hand pinch gesture strength (0.0 - 1.0) |
| `VRCOSC/OpenXR/Haptics/Left` | `float` | `Read` | Trigger left controller vibration (amplitude) |
| `VRCOSC/OpenXR/Haptics/Right` | `float` | `Read` | Trigger right controller vibration (amplitude) |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **OpenXR Haptic Pulse** | Hand (Left/Right), DurationMs (int), Amplitude (float), Frequency (float) | Success (bool) | Triggers haptic vibration on controller |
| **Get OpenXR Runtime Info** | Flow trigger | Runtime Name (string), System Name (string), FPS (float) | Returns OpenXR session metadata |
| **Get Hand Pose** | Hand (Left/Right) | Is Valid (bool), Position (Vector3), Rotation (Quaternion) | Returns hand/controller tracking pose |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **EnableHaptics** | `Toggle` | `Enable haptic feedback on OpenXR controllers` | `true` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Runtime Name** | `runtimename` | `string` | `Active OpenXR runtime name (e.g. SteamVR, Monado)` |
| **Frame Rate** | `framerate` | `float` | `OpenXR compositor frame rate` |
| **Frame Time Ms** | `frametimems` | `float` | `OpenXR compositor frame time in ms` |
| **System Name** | `systemname` | `string` | `VR Headset system name` |
| **Headpose Valid** | `headposevalid` | `bool` | `True if HMD pose tracking is valid` |
| **Left Hand Valid** | `lefthandvalid` | `bool` | `True if left hand controller/tracking is valid` |
| **Right Hand Valid** | `righthandvalid` | `bool` | `True if right hand controller/tracking is valid` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disabled** | `disabled` | `OpenXR Disabled` | `OpenXR inactive` |
| **Searching** | `searching` | `OpenXR Searching...` | `Connecting to OpenXR instance` |
| **Active** | `active` | `OpenXR Active ({0} FPS)` | `OpenXR session active` |
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
| **VRCOSC/OpenXR/FrameRate** | `float` | `Write` | `OpenXR compositor FPS` |
| **VRCOSC/OpenXR/FrameTimeMs** | `float` | `Write` | `OpenXR compositor frame time (ms)` |
| **VRCOSC/OpenXR/Gestures/Pinch/Left** | `float` | `Write` | `Left hand pinch gesture strength (0.0 - 1.0)` |
| **VRCOSC/OpenXR/Gestures/Pinch/Right** | `float` | `Write` | `Right hand pinch gesture strength (0.0 - 1.0)` |
| **VRCOSC/OpenXR/Haptics/Left** | `float` | `Read` | `Trigger left controller vibration (amplitude)` |
| **VRCOSC/OpenXR/Haptics/Right** | `float` | `Read` | `Trigger right controller vibration (amplitude)` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **OpenXR Haptic Pulse** | `Hand (Left/Right), DurationMs (int), Amplitude (float), Frequency (float)` | `Success (bool)` | `Triggers haptic vibration on controller` |
| **Get OpenXR Runtime Info** | `Flow trigger` | `Runtime Name (string), System Name (string), FPS (float)` | `Returns OpenXR session metadata` |
| **Get Hand Pose** | `Hand (Left/Right)` | `Is Valid (bool), Position (Vector3), Rotation (Quaternion)` | `Returns hand/controller tracking pose` |
<!-- AUTOGEN:NODES:END -->
