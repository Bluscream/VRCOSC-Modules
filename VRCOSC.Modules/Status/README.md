# Status

A list of status texts shown one at a time in the ChatBox, with optional cycling, groups and an icon prefix

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Cycle statuses** | `Toggle` | `Take turns showing every status marked for cycling. Can also be toggled at runtime through the Cycle parameter.` | `false` |
| **Interval (seconds)** | `TextBox` | `Seconds each status stays before the next one` | `30` |
| **Random order** | `Toggle` | `Pick the next status by weighted random (statuses shown longest ago are most likely) instead of top to bottom` | `false` |
| **Only cycle this group** | `TextBox` | `When set, cycling only uses statuses in this group` | `empty` |
| **Icon in front** | `Toggle` | `Put an icon in front of the status text in the Status variable. The icon is also available on its own as the Icon variable.` | `true` |
| **Shuffle icons** | `Toggle` | `Pick a random icon on each switch instead of going in order` | `false` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Status (with icon)** | `status` | `string` | `ChatBox variable Status (with icon)` |
| **Status text** | `text` | `string` | `ChatBox variable Status text` |
| **Icon** | `icon` | `string` | `ChatBox variable Icon` |
| **Index** | `index` | `int` | `ChatBox variable Index` |
| **Count** | `count` | `int` | `ChatBox variable Count` |
| **Group** | `group` | `string` | `ChatBox variable Group` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `{0}` | `Default state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Status changed** | `changed` | `{0}` | `Triggered on Status changed` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Status/Next** | `bool` | `Read` | `Becoming true switches to the next status` |
| **VRCOSC/Status/Previous** | `bool` | `Read` | `Becoming true switches to the previous status` |
| **VRCOSC/Status/Cycle** | `bool` | `ReadWrite` | `Whether statuses are cycling; write to turn cycling on or off` |
| **VRCOSC/Status/Index** | `int` | `ReadWrite` | `Index of the active status in the list; write to select one` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
