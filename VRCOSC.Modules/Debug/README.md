# Debug Module

Debug tools for tracking and exporting OSC parameters with CSV exports, Harmony patches for Linux/Wine connection log spam, WinRT file picker fixes, and ChatBox validation protection.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- VRCOSC 2026.501.0 or later
- Harmony patches automatically apply on module load to suppress repeated OSC disconnection stack traces and fix Wine WinRT file picker dialog crashes.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **DumpDirectory** | `TextBox` | Custom directory for CSV parameter exports (default: dumps folder) | `empty` |
| **SortBy** | `Dropdown` | Column to sort CSV parameter dumps by | `ParameterPath` |
| **SortDirection** | `Dropdown` | Sort direction for CSV parameter dumps | `Ascending` |
| **LogParameterUpdates** | `Toggle` | Log all parameter updates to console | `false` |
| **AutoStartModules** | `Toggle` | Automatically start VRCOSC on load (equivalent to Play button) | `false` |
| **SuppressConnectAsyncLogSpam** | `Toggle` | Intercept and suppress repeating 'Please call ConnectAsync first' stack traces | `true` |
| **FixWinRTFilePickerException** | `Toggle` | Patch PickFileAsync with WPF OpenFileDialog fallback for Linux/Wine | `true` |
| **BypassChatBoxValidation** | `Toggle` | Prevent VRCOSC from wiping ChatBox timeline clips when dynamic variables unregister | `true` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Incoming Count** | `incomingcount` | `int` | Number of unique incoming OSC parameters tracked |
| **Outgoing Count** | `outgoingcount` | `int` | Number of unique outgoing OSC parameters tracked |
| **Total Count** | `totalcount` | `int` | Total unique OSC parameters tracked (incoming + outgoing) |
| **Last Dump Path** | `lastdumppath` | `string` | Absolute filepath of the last created CSV dump file |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `Debug\nTracking: {0} params` | Module active and tracking parameters in memory |
| **Dumping** | `dumping` | `Dumping {0} params...` | CSV parameter dump in progress |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Dump Complete** | `ondumpcomplete` | `Dumped to: {0}` | Triggered after a CSV parameter dump completes |
| **On Tracking Cleared** | `ontrackingcleared` | `Cleared all tracked parameters` | Triggered when parameter tracking cache is cleared |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/Debug/DumpNow` | `bool` | `Read` | Set to true to trigger a CSV parameter dump |
| `VRCOSC/Debug/Clear` | `bool` | `Read` | Set to true to clear parameter tracking cache |
| `VRCOSC/Debug/IncomingCount` | `int` | `Write` | Number of unique incoming parameters |
| `VRCOSC/Debug/OutgoingCount` | `int` | `Write` | Number of unique outgoing parameters |
| `VRCOSC/Debug/TotalCount` | `int` | `Write` | Total unique parameters tracked |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Dump All Parameters** | Flow trigger | File Path, Total Parameters, Error | Exports all tracked parameters to CSV |
| **Dump Incoming Parameters** | Flow trigger | File Path, Parameter Count, Error | Exports only incoming parameters to CSV |
| **Dump Outgoing Parameters** | Flow trigger | File Path, Parameter Count, Error | Exports only outgoing parameters to CSV |
| **Clear Parameter Tracking** | Flow trigger | None | Clears parameter tracking memory cache |
| **Get Parameter Counts** | Flow trigger | Incoming Count, Outgoing Count, Total Count | Returns current parameter counts |
| **Get Incoming Parameters** | Flow trigger | Parameters (Dict), Count | Returns dictionary of incoming parameters |
| **Get Outgoing Parameters** | Flow trigger | Parameters (Dict), Count | Returns dictionary of outgoing parameters |
| **Get All Parameters** | Flow trigger | Parameters (Dict), Incoming Count, Outgoing Count, Total Count | Returns dictionary of all parameters |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **DumpDirectory** | `TextBox` | `Custom directory for CSV parameter exports (default: dumps folder)` | `empty` |
| **SortBy** | `Dropdown` | `Column to sort CSV parameter dumps by` | `ParameterPath` |
| **SortDirection** | `Dropdown` | `Sort direction for CSV parameter dumps` | `Ascending` |
| **LogParameterUpdates** | `Toggle` | `Log all parameter updates to console` | `false` |
| **AutoStartModules** | `Toggle` | `Automatically start VRCOSC on load (equivalent to Play button)` | `false` |
| **SuppressConnectAsyncLogSpam** | `Toggle` | `Intercept and suppress repeating 'Please call ConnectAsync first' stack traces` | `true` |
| **FixWinRTFilePickerException** | `Toggle` | `Patch PickFileAsync with WPF OpenFileDialog fallback for Linux/Wine` | `true` |
| **BypassChatBoxValidation** | `Toggle` | `Prevent VRCOSC from wiping ChatBox timeline clips when dynamic variables unregister` | `true` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Incoming Count** | `incomingcount` | `int` | `Number of unique incoming OSC parameters tracked` |
| **Outgoing Count** | `outgoingcount` | `int` | `Number of unique outgoing OSC parameters tracked` |
| **Total Count** | `totalcount` | `int` | `Total unique OSC parameters tracked (incoming + outgoing)` |
| **Last Dump Path** | `lastdumppath` | `string` | `Absolute filepath of the last created CSV dump file` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `Debug\nTracking: {0} params` | `Module active and tracking parameters in memory` |
| **Dumping** | `dumping` | `Dumping {0} params...` | `CSV parameter dump in progress` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Dump Complete** | `ondumpcomplete` | `Dumped to: {0}` | `Triggered after a CSV parameter dump completes` |
| **On Tracking Cleared** | `ontrackingcleared` | `Cleared all tracked parameters` | `Triggered when parameter tracking cache is cleared` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Debug/DumpNow** | `bool` | `Read` | `Set to true to trigger a CSV parameter dump` |
| **VRCOSC/Debug/Clear** | `bool` | `Read` | `Set to true to clear parameter tracking cache` |
| **VRCOSC/Debug/IncomingCount** | `int` | `Write` | `Number of unique incoming parameters` |
| **VRCOSC/Debug/OutgoingCount** | `int` | `Write` | `Number of unique outgoing parameters` |
| **VRCOSC/Debug/TotalCount** | `int` | `Write` | `Total unique parameters tracked` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Dump All Parameters** | `Flow trigger` | `File Path, Total Parameters, Error` | `Exports all tracked parameters to CSV` |
| **Dump Incoming Parameters** | `Flow trigger` | `File Path, Parameter Count, Error` | `Exports only incoming parameters to CSV` |
| **Dump Outgoing Parameters** | `Flow trigger` | `File Path, Parameter Count, Error` | `Exports only outgoing parameters to CSV` |
| **Clear Parameter Tracking** | `Flow trigger` | `None` | `Clears parameter tracking memory cache` |
| **Get Parameter Counts** | `Flow trigger` | `Incoming Count, Outgoing Count, Total Count` | `Returns current parameter counts` |
| **Get Incoming Parameters** | `Flow trigger` | `Parameters (Dict), Count` | `Returns dictionary of incoming parameters` |
| **Get Outgoing Parameters** | `Flow trigger` | `Parameters (Dict), Count` | `Returns dictionary of outgoing parameters` |
| **Get All Parameters** | `Flow trigger` | `Parameters (Dict), Incoming Count, Outgoing Count, Total Count` | `Returns dictionary of all parameters` |
<!-- AUTOGEN:NODES:END -->

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Dump Directory** | `TextBox` | `Directory for parameter dumps (leave empty for 'dumps' folder in module directory)` | `empty` |
| **Sort By** | `Dropdown` | `Which column to sort the CSV by before saving` | `CsvSortBy.ParameterPath` |
| **Sort Direction** | `Dropdown` | `Sort order for CSV export` | `CsvSortDirection.Ascending` |
| **Use VRCOSC Tracking** | `Toggle` | `Use VRCOSC's built-in parameter tracking` | `true` |
| **Avatar Parameters Only** | `Toggle` | `Only track avatar parameters` | `true` |
| **Auto-Track Incoming** | `Toggle` | `Automatically track incoming parameters` | `true` |
| **Auto-Track Outgoing** | `Toggle` | `Automatically track outgoing parameters` | `true` |
| **Max Parameters** | `Slider` | `Maximum parameters to track (0 = unlimited)` | `0, 0, 10000, 100` |
| **Log Parameter Updates** | `Toggle` | `Log all parameter updates to console` | `false` |
| **Auto Start VRCOSC on Load** | `Toggle` | `Automatically starts VRCOSC when it loads (equivalent to clicking Play button). Bypasses VRChat detection.` | `false` |
| **Suppress ConnectAsync Log Spam** | `Toggle` | `Intercept and suppress repeating 'Please call ConnectAsync first' exception stack traces when OSC is disconnected.` | `true` |
| **Fix WinRT FilePicker Exception (Linux/Wine)** | `Toggle` | `Patch VRCOSC PickFileAsync with WPF OpenFileDialog fallback to fix 'WinRT.ActivationFactory threw an exception' / REGDB_E_CLASSNOTREG errors on Linux/Wine.` | `true` |
| **Bypass ChatBox Timeline Validation** | `Toggle` | `Prevent VRCOSC from wiping out your ChatBox timeline clips when dynamic variables change or are unregistered.` | `true` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Incoming Count** | `incomingcount` | `int` | `ChatBox variable Incoming Count` |
| **Outgoing Count** | `outgoingcount` | `int` | `ChatBox variable Outgoing Count` |
| **Total Count** | `totalcount` | `int` | `ChatBox variable Total Count` |
| **Last Dump Path** | `lastdumppath` | `string` | `ChatBox variable Last Dump Path` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `Debug\nTracking: {0} params` | `Idle state` |
| **Dumping** | `dumping` | `Dumping {0} params...` | `Dumping state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Dump Complete** | `ondumpcomplete` | `Dumped to: {0}` | `Triggered on Dump Complete` |
| **Tracking Cleared** | `ontrackingcleared` | `Cleared all tracked parameters` | `Triggered on Tracking Cleared` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Debug/DumpNow** | `bool` | `Read` | `Set to true to trigger a parameter dump` |
| **VRCOSC/Debug/Clear** | `bool` | `Read` | `Set to true to clear tracked parameters` |
| **VRCOSC/Debug/IncomingCount** | `int` | `Write` | `Number of unique incoming parameters` |
| **VRCOSC/Debug/OutgoingCount** | `int` | `Write` | `Number of unique outgoing parameters` |
| **VRCOSC/Debug/TotalCount** | `int` | `Write` | `Total unique parameters (incoming + outgoing)` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Dump All Parameters** | `Flow trigger` | `Output` | `Node node for Dump All Parameters` |
| **Clear Parameter Tracking** | `Flow trigger` | `Output` | `Node node for Clear Parameter Tracking` |
| **Get Parameter Counts** | `Flow trigger` | `Output` | `Node node for Get Parameter Counts` |
| **Get All Parameters** | `Flow trigger` | `Output` | `Node node for Get All Parameters` |
<!-- NODES_TABLE_END -->
