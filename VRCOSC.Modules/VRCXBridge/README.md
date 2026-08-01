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
