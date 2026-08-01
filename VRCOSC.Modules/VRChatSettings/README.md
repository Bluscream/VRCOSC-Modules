# VRChat Settings Module

Read and write 746+ VRChat registry settings and config file values with provider architecture, JSON schema validation, and user ID templates.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- VRChat installed.
- Windows Registry access (`HKCU\Software\VRChat\vrchat`) or Proton/Wine registry (`system.reg` / `user.reg`).

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **AutoBackup** | `Toggle` | Automatically back up settings before writing | `true` |
| **BackupDirectory** | `TextBox` | Directory for settings backups | `empty` |
| **EnableRegistryAccess** | `Toggle` | Enable VRChat registry settings provider | `true` |
| **EnableConfigAccess** | `Toggle` | Enable VRChat config.json settings provider | `true` |
| **RemoteFirstProvider** | `Toggle` | Try fetching definitions from GitHub Gist before embedded fallbacks | `true` |
| **LogDebug** | `Toggle` | Log settings read/write operations to console | `false` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Registry Count** | `registrycount` | `int` | Total registry settings available (746+) |
| **Config Count** | `configcount` | `int` | Total config file settings available |
| **Last Setting Modified** | `lastsettingmodified` | `string` | Name of last modified setting |
| **Backups Count** | `backupscount` | `int` | Number of backups created |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `VRChat Settings Idle` | Module ready |
| **Reading** | `reading` | `Reading {0}...` | Reading setting value |
| **Writing** | `writing` | `Writing {0}...` | Writing setting value |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Setting Read** | `onsettingread` | `Read: {0} = {1}` | Triggered when setting is read |
| **On Setting Written** | `onsettingwritten` | `Wrote: {0} = {1}` | Triggered when setting is modified |
| **On Backup Created** | `onbackupcreated` | `Backup Created: {0}` | Triggered when backup file is created |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/VRChatSettings/Read` | `bool` | `Read` | Trigger setting read |
| `VRCOSC/VRChatSettings/Write` | `bool` | `Read` | Trigger setting write |
| `VRCOSC/VRChatSettings/Success` | `bool` | `Write` | True if last operation succeeded |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get VRChat Registry Value<T>** | Setting Name (string), User ID (string) | Value (T), Exists (bool) | Reads VRChat registry setting |
| **Get VRChat Config Value<T>** | Setting Name (string) | Value (T), Exists (bool) | Reads VRChat config.json setting |
| **Set VRChat Registry Value<T>** | Setting Name (string), Value (T), User ID (string) | Success (bool) | Writes VRChat registry setting |
| **Set VRChat Config Value<T>** | Setting Name (string), Value (T) | Success (bool) | Writes VRChat config.json setting |
| **Get All VRChat Registry Settings** | Flow trigger | Settings (Dict) | Returns dictionary of all 746+ registry settings |
| **Get All VRChat Config Settings** | Flow trigger | Settings (Dict) | Returns dictionary of all config.json settings |
| **Object To JSON String<T>** | Value (T), Formatted (bool) | JSON String (string) | Serializes object/collection to JSON |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **AutoBackup** | `Toggle` | `Automatically back up settings before writing` | `true` |
| **BackupDirectory** | `TextBox` | `Directory for settings backups` | `empty` |
| **EnableRegistryAccess** | `Toggle` | `Enable VRChat registry settings provider` | `true` |
| **EnableConfigAccess** | `Toggle` | `Enable VRChat config.json settings provider` | `true` |
| **RemoteFirstProvider** | `Toggle` | `Try fetching definitions from GitHub Gist before embedded fallbacks` | `true` |
| **LogDebug** | `Toggle` | `Log settings read/write operations to console` | `false` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Registry Count** | `registrycount` | `int` | `Total registry settings available (746+)` |
| **Config Count** | `configcount` | `int` | `Total config file settings available` |
| **Last Setting Modified** | `lastsettingmodified` | `string` | `Name of last modified setting` |
| **Backups Count** | `backupscount` | `int` | `Number of backups created` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `VRChat Settings Idle` | `Module ready` |
| **Reading** | `reading` | `Reading {0}...` | `Reading setting value` |
| **Writing** | `writing` | `Writing {0}...` | `Writing setting value` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Setting Read** | `onsettingread` | `Read: {0} = {1}` | `Triggered when setting is read` |
| **On Setting Written** | `onsettingwritten` | `Wrote: {0} = {1}` | `Triggered when setting is modified` |
| **On Backup Created** | `onbackupcreated` | `Backup Created: {0}` | `Triggered when backup file is created` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/VRChatSettings/Read** | `bool` | `Read` | `Trigger setting read` |
| **VRCOSC/VRChatSettings/Write** | `bool` | `Read` | `Trigger setting write` |
| **VRCOSC/VRChatSettings/Success** | `bool` | `Write` | `True if last operation succeeded` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get VRChat Registry Value<T>** | `Setting Name (string), User ID (string)` | `Value (T), Exists (bool)` | `Reads VRChat registry setting` |
| **Get VRChat Config Value<T>** | `Setting Name (string)` | `Value (T), Exists (bool)` | `Reads VRChat config.json setting` |
| **Set VRChat Registry Value<T>** | `Setting Name (string), Value (T), User ID (string)` | `Success (bool)` | `Writes VRChat registry setting` |
| **Set VRChat Config Value<T>** | `Setting Name (string), Value (T)` | `Success (bool)` | `Writes VRChat config.json setting` |
| **Get All VRChat Registry Settings** | `Flow trigger` | `Settings (Dict)` | `Returns dictionary of all 746+ registry settings` |
| **Get All VRChat Config Settings** | `Flow trigger` | `Settings (Dict)` | `Returns dictionary of all config.json settings` |
| **Object To JSON String<T>** | `Value (T), Formatted (bool)` | `JSON String (string)` | `Serializes object/collection to JSON` |
<!-- AUTOGEN:NODES:END -->
