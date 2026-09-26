# OpenXR Modules

OpenXR counterparts of VRCOSC's official SteamVR modules (Stats, Haptic Control, Gesture
Extensions) for setups where VRCOSC's built-in OpenVR manager cannot reach the runtime, such
as WiVRn or Monado on Linux with VRCOSC running under Proton. They publish the **same OSC
parameter names** as the official modules, so prefabs and ChatBox layouts built for SteamVR
Stats / Haptic Control / Index Gesture Extensions keep working unchanged.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## How it works

All three modules share one `OpenXRRuntime`: a single OpenXR instance and a **headless
session** (`XR_MND_headless`) on a dedicated thread that pumps events, runs the frame loop,
syncs an action set and locates hand joints, then publishes an immutable snapshot every
frame. The session is requested as an `XR_EXTX_overlay` session so controller input stays
readable while VRChat has focus; the *Overlay session* setting turns that off.

| Data | Source |
|---|---|
| Refresh rate ("FPS") | `XR_FB_display_refresh_rate`, else the predicted display period |
| Head tracked ("User Present") | `VIEW` space located against `LOCAL` |
| Finger curls | `XR_EXT_hand_tracking` joint angles when active; otherwise trigger (index) and grip (other fingers) |
| Button / stick / pad touch | Action set bound to Touch, Index, Vive and simple-controller profiles |
| Haptics | `xrApplyHapticFeedback` on the same action set |
| Battery, charging, "Dashboard visible" | `vrcosc_xr_query.sh` on the Linux host, via **libmonado** (OpenXR has no battery or foreign-app focus API) |

The runtime is unavailable until the headset is connected (WiVRn only registers itself as
the active runtime then), so initialisation is retried every 5 s instead of failing once at
module start. Every OpenXR result code is logged on first failure.

## Setup & Requirements

- An OpenXR runtime that supports `XR_MND_headless`: WiVRn, Monado. SteamVR does **not**
  offer headless sessions; on SteamVR use the official modules instead.
- `openxr_loader.dll` is embedded and extracted automatically at load.
- Battery and focus need the WiVRn flatpak (`io.github.wivrn.wivrn`) or a native Monado
  with `libmonado` and `python3` on the host. The probe script is deployed to
  `~/.local/bin/vrcosc_xr_query.sh` on first start and writes `~/.vrcosc_openxr.json`.

## Troubleshooting

Read the module log (`logs/*.module-debug.log`): the runtime logs the extension list it saw,
which extensions it enabled, every session state change, the interaction profiles the
runtime accepted and the current profile per hand. `xrCreateInstance failed:
ErrorRuntimeUnavailable` means no headset is connected yet; `xrCreateSession failed` after a
successful instance means the runtime (or Proton's `wineopenxr` bridge) refused a session
without a graphics binding.

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Threshold** | `Slider` | `How far down a finger should be to be considered down\n0 being fully up. 1 being fully down` | `0.5f, 0f, 1f, 0.01f` |
| **Overlay session** | `Toggle` | `Ask the runtime for an overlay session so controller input stays readable while VRChat is focused. Turn off if the runtime refuses to start the session.` | `true` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **FPS** | `fps` | `float` | `ChatBox variable FPS` |
| **Dashboard Visible** | `dashboardvisible` | `bool` | `ChatBox variable Dashboard Visible` |
| **Runtime Name** | `runtimename` | `string` | `ChatBox variable Runtime Name` |
| **System Name** | `systemname` | `string` | `ChatBox variable System Name` |
| **Session State** | `sessionstate` | `string` | `ChatBox variable Session State` |
| **HMD Charging** | `hmd_charging` | `bool` | `ChatBox variable HMD Charging` |
| **HMD Battery (%)** | `hmd_battery` | `int` | `ChatBox variable HMD Battery (%)` |
| **Left Hand Charging** | `lhand_charging` | `bool` | `ChatBox variable Left Hand Charging` |
| **Left Hand Battery (%)** | `lhand_battery` | `int` | `ChatBox variable Left Hand Battery (%)` |
| **Right Hand Charging** | `rhand_charging` | `bool` | `ChatBox variable Right Hand Charging` |
| **Right Hand Battery (%)** | `rhand_battery` | `int` | `ChatBox variable Right Hand Battery (%)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `HMD: {0}\nLHand: {1}\nRHand: {2}` | `Default state` |
| **No Runtime** | `noruntime` | `OpenXR runtime not available` | `No Runtime state` |
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
| **VRCOSC/VR/Gestures/Left** | `int` | `Write` | `Custom left hand gesture value` |
| **VRCOSC/VR/Gestures/Right** | `int` | `Write` | `Custom right hand gesture value` |
| **VRCOSC/VR/Haptics/Duration** | `float` | `Read` | `The duration of the haptic trigger in seconds` |
| **VRCOSC/VR/Haptics/Frequency** | `float` | `Read` | `The frequency of the haptic trigger (0-1, mapped to 0-100 Hz; 0 lets the runtime choose)` |
| **VRCOSC/VR/Haptics/Amplitude** | `float` | `Read` | `The amplitude of the haptic trigger (0-1)` |
| **VRCOSC/VR/Haptics/TriggerLeft** | `bool` | `Read` | `Becoming true causes a haptic trigger in the left controller using the above parameters` |
| **VRCOSC/VR/Haptics/TriggerRight** | `bool` | `Read` | `Becoming true causes a haptic trigger in the right controller using the above parameters` |
| **VRCOSC/VR/Haptics/TriggerLeft/*/*/*** | `bool` | `Read` | `Becoming true causes a haptic trigger in the left controller using the wildcards\nFor example:\n Writing 'VRCOSC/VR/Haptics/TriggerLeft/2/0.5/0.75' will trigger haptics in the left controller with a 2 second duration, 0.5 frequency, and 0.75 amplitude` |
| **VRCOSC/VR/Haptics/TriggerRight/*/*/*** | `bool` | `Read` | `Becoming true causes a haptic trigger in the right controller using the wildcards\nFor example:\n Writing 'VRCOSC/VR/Haptics/TriggerRight/2/0.5/0.75' will trigger haptics in the right controller with a 2 second duration, 0.5 frequency, and 0.75 amplitude` |
| **VRCOSC/VR/FPS/Value** | `int` | `Write` | `Display refresh rate of the headset (OpenXR has no per-app FPS)` |
| **VRCOSC/VR/FPS/Normalised** | `float` | `Write` | `Refresh rate normalised from 0-240 to 0-1` |
| **VRCOSC/VR/UserPresent** | `bool` | `Write` | `Whether the headset is being tracked (worn)` |
| **VRCOSC/VR/DashboardVisible** | `bool` | `Write` | `Whether the main application is visible but not focused (system UI / dashboard open)` |
| **VRCOSC/VR/HMD/Connected** | `bool` | `Write` | `Whether an OpenXR session with the headset is running` |
| **VRCOSC/VR/HMD/Battery** | `float` | `Write` | `Headset battery normalised (0-1)` |
| **VRCOSC/VR/HMD/Charging** | `bool` | `Write` | `Whether the headset is charging` |
| **VRCOSC/VR/LHand/Connected** | `bool` | `Write` | `Whether the left controller or hand is tracked` |
| **VRCOSC/VR/LHand/Battery** | `float` | `Write` | `Left controller battery normalised (0-1)` |
| **VRCOSC/VR/LHand/Charging** | `bool` | `Write` | `Whether the left controller is charging` |
| **VRCOSC/VR/LHand/Input/A/Touch** | `bool` | `Write` | `Whether the left primary button (A/X) is touched` |
| **VRCOSC/VR/LHand/Input/B/Touch** | `bool` | `Write` | `Whether the left secondary button (B/Y) is touched` |
| **VRCOSC/VR/LHand/Input/Pad/Touch** | `bool` | `Write` | `Whether the left trackpad or thumbrest is touched` |
| **VRCOSC/VR/LHand/Input/Stick/Touch** | `bool` | `Write` | `Whether the left thumbstick is touched` |
| **VRCOSC/VR/LHand/Input/Finger/Index** | `float` | `Write` | `Left index finger curl (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Middle** | `float` | `Write` | `Left middle finger curl (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Ring** | `float` | `Write` | `Left ring finger curl (0-1)` |
| **VRCOSC/VR/LHand/Input/Finger/Pinky** | `float` | `Write` | `Left pinky finger curl (0-1)` |
| **VRCOSC/VR/RHand/Connected** | `bool` | `Write` | `Whether the right controller or hand is tracked` |
| **VRCOSC/VR/RHand/Battery** | `float` | `Write` | `Right controller battery normalised (0-1)` |
| **VRCOSC/VR/RHand/Charging** | `bool` | `Write` | `Whether the right controller is charging` |
| **VRCOSC/VR/RHand/Input/A/Touch** | `bool` | `Write` | `Whether the right primary button (A) is touched` |
| **VRCOSC/VR/RHand/Input/B/Touch** | `bool` | `Write` | `Whether the right secondary button (B) is touched` |
| **VRCOSC/VR/RHand/Input/Pad/Touch** | `bool` | `Write` | `Whether the right trackpad or thumbrest is touched` |
| **VRCOSC/VR/RHand/Input/Stick/Touch** | `bool` | `Write` | `Whether the right thumbstick is touched` |
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
