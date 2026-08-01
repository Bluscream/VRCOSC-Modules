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

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Threshold** | `Slider` | `How far down a finger must be to count as 'down' (0=fully up, 1=fully down)` | `0.5f, 0f, 1f, 0.01f` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **FPS** | `fps` | `float` | `ChatBox variable FPS` |
| **HMD Battery (%)** | `hmd_battery` | `int` | `ChatBox variable HMD Battery (%)` |
| **HMD Charging** | `hmd_charging` | `bool` | `ChatBox variable HMD Charging` |
| **Left Hand Battery (%)** | `lhand_battery` | `int` | `ChatBox variable Left Hand Battery (%)` |
| **Left Hand Charging** | `lhand_charging` | `bool` | `ChatBox variable Left Hand Charging` |
| **Right Hand Battery (%)** | `rhand_battery` | `int` | `ChatBox variable Right Hand Battery (%)` |
| **Right Hand Charging** | `rhand_charging` | `bool` | `ChatBox variable Right Hand Charging` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `HMD: {0}%\nLHand: {1}%\nRHand: {2}%` | `Default state` |
| **No Runtime** | `noruntime` | `OpenXR runtime not found` | `No Runtime state` |
| **Error** | `error` | `OpenXR error — check logs` | `Error state` |
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
| **VRCOSC/VR/Haptics/Duration** | `float` | `Read` | `Duration of haptic in seconds` |
| **VRCOSC/VR/Haptics/Frequency** | `float` | `Read` | `Frequency of haptic (0-1 → 0-300 Hz)` |
| **VRCOSC/VR/Haptics/Amplitude** | `float` | `Read` | `Amplitude of haptic (0-1)` |
| **VRCOSC/VR/Gestures/Left** | `int` | `Write` | `Custom left hand gesture value` |
| **VRCOSC/VR/Gestures/Right** | `int` | `Write` | `Custom right hand gesture value` |
| **VRCOSC/VR/FPS/Normalised** | `float` | `Write` | `FPS normalised 0-240 → 0-1` |
| **VRCOSC/VR/UserPresent** | `bool` | `Write` | `Headset is worn / session focused` |
| **VRCOSC/VR/DashboardVisible** | `bool` | `Write` | `Session visible but not focused` |
| **VRCOSC/VR/HMD/Battery** | `float` | `Write` | `HMD battery percentage (0-1)` |
| **VRCOSC/VR/LHand/Battery** | `float` | `Write` | `Left controller battery (0-1)` |
| **VRCOSC/VR/RHand/Battery** | `float` | `Write` | `Right controller battery (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Index** | `float` | `Write` | `Left index finger curl (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Middle** | `float` | `Write` | `Left middle finger curl (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Ring** | `float` | `Write` | `Left ring finger curl (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Pinky** | `float` | `Write` | `Left pinky finger curl (0-1)` |
| **VRCOSC/VR/RHand/Input/Finger/Index** | `float` | `Write` | `Right index finger curl (0-1)` |
| **VRCOSC/VR/RHand/Input/Finger/Middle** | `float` | `Write` | `Right middle finger curl (0-1)` |
| **VRCOSC/VR/RHand/Input/Finger/Ring** | `float` | `Write` | `Right ring finger curl (0-1)` |
| **VRCOSC/VR/RHand/Input/Finger/Pinky** | `float` | `Write` | `Right pinky finger curl (0-1)` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
