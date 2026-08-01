# VRCX Bridge Module

Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration via Windows Named Pipes (\\.\pipe\vrcx-ipc).

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- VRCX running on the system with IPC enabled.
- Windows Named Pipe access (`\\.\pipe\vrcx-ipc`).

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **PipeName** | `TextBox` | Named Pipe name for VRCX IPC | `vrcx-ipc` |
| **AutoReconnect** | `Toggle` | Automatically reconnect if VRCX closes | `true` |
| **LogDebug** | `Toggle` | Log VRCX IPC messages to console | `false` |
| **SyncAvatarParameters** | `Toggle` | Sync avatar parameters to VRCX | `true` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | True if connected to VRCX Named Pipe |
| **Current World Name** | `currentworldname` | `string` | Name of current VRChat world |
| **Current World ID** | `currentworldid` | `string` | World ID of current VRChat world |
| **Online Friends Count** | `onlinefriendscount` | `int` | Number of online VRChat friends in VRCX |
| **Last Friend Name** | `lastfriendname` | `string` | Name of last friend event |
| **Last Friend ID** | `lastfriendid` | `string` | User ID of last friend event |
| **Last Toast Text** | `lasttoasttext` | `string` | Text of last toast sent to VRCX |
| **IPC Message Count** | `ipcmessagecount` | `int` | Total VRCX IPC messages processed |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `VRCX Disconnected` | Disconnected from VRCX IPC |
| **Connected** | `connected` | `VRCX Connected ({0} Friends)` | Connected to VRCX IPC |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On IPC Connected** | `onipcconnected` | `VRCX IPC Connected` | Triggered on successful pipe connection |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/VRCX/Connected` | `bool` | `Write` | True if connected to VRCX |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **VRCX Get Online Friends** | Flow trigger | Friends (List), Count (int) | Returns list of online VRChat friends from VRCX |
| **VRCX Send Invite** | User ID (string), World ID (string), Instance ID (string) | Success (bool) | Sends world invite to user via VRCX |
| **VRCX Get User Info** | User ID (string) | User Name (string), Bio (string), Status (string) | Fetches user info from VRCX API cache |
| **VRCX Get Current Location** | Flow trigger | World ID (string), World Name (string), Instance ID (string) | Returns current world location |
| **VRCX Show Toast** | Title (string), Message (string) | Success (bool) | Displays toast notification inside VRCX |
| **VRCX Connection Status** | Flow trigger | Is Connected (bool) | Checks VRCX pipe connection |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **PipeName** | `TextBox` | `Named Pipe name for VRCX IPC` | `vrcx-ipc` |
| **AutoReconnect** | `Toggle` | `Automatically reconnect if VRCX closes` | `true` |
| **LogDebug** | `Toggle` | `Log VRCX IPC messages to console` | `false` |
| **SyncAvatarParameters** | `Toggle` | `Sync avatar parameters to VRCX` | `true` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | `True if connected to VRCX Named Pipe` |
| **Current World Name** | `currentworldname` | `string` | `Name of current VRChat world` |
| **Current World ID** | `currentworldid` | `string` | `World ID of current VRChat world` |
| **Online Friends Count** | `onlinefriendscount` | `int` | `Number of online VRChat friends in VRCX` |
| **Last Friend Name** | `lastfriendname` | `string` | `Name of last friend event` |
| **Last Friend ID** | `lastfriendid` | `string` | `User ID of last friend event` |
| **Last Toast Text** | `lasttoasttext` | `string` | `Text of last toast sent to VRCX` |
| **IPC Message Count** | `ipcmessagecount` | `int` | `Total VRCX IPC messages processed` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `VRCX Disconnected` | `Disconnected from VRCX IPC` |
| **Connected** | `connected` | `VRCX Connected ({0} Friends)` | `Connected to VRCX IPC` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On IPC Connected** | `onipcconnected` | `VRCX IPC Connected` | `Triggered on successful pipe connection` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/VRCX/Connected** | `bool` | `Write` | `True if connected to VRCX` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **VRCX Get Online Friends** | `Flow trigger` | `Friends (List), Count (int)` | `Returns list of online VRChat friends from VRCX` |
| **VRCX Send Invite** | `User ID (string), World ID (string), Instance ID (string)` | `Success (bool)` | `Sends world invite to user via VRCX` |
| **VRCX Get User Info** | `User ID (string)` | `User Name (string), Bio (string), Status (string)` | `Fetches user info from VRCX API cache` |
| **VRCX Get Current Location** | `Flow trigger` | `World ID (string), World Name (string), Instance ID (string)` | `Returns current world location` |
| **VRCX Show Toast** | `Title (string), Message (string)` | `Success (bool)` | `Displays toast notification inside VRCX` |
| **VRCX Connection Status** | `Flow trigger` | `Is Connected (bool)` | `Checks VRCX pipe connection` |
<!-- AUTOGEN:NODES:END -->

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Enabled** | `Toggle` | `Enable VRCX bridge` | `true` |
| **Auto Reconnect** | `Toggle` | `Automatically reconnect if connection lost` | `true` |
| **Reconnect Delay (ms)** | `TextBox` | `Delay before reconnect attempt` | `5000` |
| **Batch Interval (ms)** | `TextBox` | `Collect events and send in bulk every X ms` | `2000` |
| **Deduplicate Events** | `Toggle` | `Only send latest value per parameter (discard intermediate values)` | `true` |
| **Only Changed Values** | `Toggle` | `Only send parameters when their value actually changes` | `true` |
| **IPC Message Type** | `TextBox` | `Type wrapper for OSC bulk events (Event7List=silent, VrcxMessage=verbose)` | `"Event7List"` |
| **Log OSC Parameters** | `Toggle` | `Log OSC parameter changes to console` | `false` |
| **Log VRCX Commands** | `Toggle` | `Log commands to/from VRCX` | `false` |
| **Log Raw IPC** | `Toggle` | `Log raw IPC message traffic (very verbose)` | `false` |
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
| **VRCOSC/VRCXBridge/Connected** | `bool` | `Write` | `True when connected to VRCX` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **V R C X Get Online Friends** | `Flow trigger` | `Output` | `Node node for V R C X Get Online Friends` |
| **V R C X Send Invite** | `Flow trigger` | `Output` | `Node node for V R C X Send Invite` |
| **V R C X Get User Info** | `Flow trigger` | `Output` | `Node node for V R C X Get User Info` |
| **V R C X Get Current Location** | `Flow trigger` | `Output` | `Node node for V R C X Get Current Location` |
| **V R C X Show Toast** | `Flow trigger` | `Output` | `Node node for V R C X Show Toast` |
| **V R C X Connection Status** | `Flow trigger` | `Output` | `Node node for V R C X Connection Status` |
<!-- NODES_TABLE_END -->
