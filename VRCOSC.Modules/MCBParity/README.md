# MagicChatbox Parity

VR reprojection and dropped-frame estimates as ChatBox variables (MagicChatbox placeholder parity)

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **VR frame estimates** | `Toggle` | `Keep the shared OpenXR session open to estimate reprojection and dropped frames from the runtime's display-period schedule. Off leaves both variables at 0.` | `true` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **VR Reprojection (% of frames, last second)** | `vr_reprojection` | `int` | `ChatBox variable VR Reprojection (% of frames, last second)` |
| **VR Dropped Frames (per minute)** | `vr_dropped_frames` | `int` | `ChatBox variable VR Dropped Frames (per minute)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `Reproj {0}% · Dropped {1}/min` | `Default state` |
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
