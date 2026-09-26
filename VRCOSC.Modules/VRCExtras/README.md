# VRChat Extras

Current world, instance type, region and player count as ChatBox variables, straight from VRCOSC's VRChat log reader

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Instance master icon** | `TextBox` | `Text shown in the Master Icon variable while you are the instance master. VRCOSC cannot tell who the master is, so this stays empty; the setting exists so the variable can be mapped now and filled in later.` | `"\U0001F451"` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **World Name** | `world` | `string` | `ChatBox variable World Name` |
| **World ID** | `worldid` | `string` | `ChatBox variable World ID` |
| **Instance Type** | `instancetype` | `string` | `ChatBox variable Instance Type` |
| **Instance Region** | `region` | `string` | `ChatBox variable Instance Region` |
| **Player Count** | `playercount` | `int` | `ChatBox variable Player Count` |
| **Instance ID** | `instanceid` | `string` | `ChatBox variable Instance ID` |
| **Instance Owner ID** | `instanceowner` | `string` | `ChatBox variable Instance Owner ID` |
| **Age Gated** | `agegated` | `bool` | `ChatBox variable Age Gated` |
| **Has Queue** | `hasqueue` | `bool` | `ChatBox variable Has Queue` |
| **Master Icon** | `mastericon` | `string` | `ChatBox variable Master Icon` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **In Instance** | `ininstance` | `{0}\n{1} · {2} · {3} players` | `In Instance state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **World Changed** | `worldchanged` | `Now in {0}` | `Triggered on World Changed` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/VRChat/Instance/Type** | `int` | `Write` | `0 public, 1 friends+, 2 friends, 3 invite+, 4 invite, 5 group, 6 group+, 7 group public` |
| **VRCOSC/VRChat/Instance/Region** | `int` | `Write` | `0 unknown, 1 US West, 2 US East, 3 Europe, 4 Japan` |
| **VRCOSC/VRChat/Instance/PlayerCount** | `int` | `Write` | `Players currently in the instance` |
| **VRCOSC/VRChat/Instance/Joined** | `bool` | `Write` | `Whether you are in an instance` |
| **VRCOSC/VRChat/Instance/AgeGated** | `bool` | `Write` | `Whether the instance is age gated` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
