# Debug Module for VRCOSC

Debug tools for tracking and exporting OSC parameters.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Features

### Core Capabilities

- ✅ **Real-Time Tracking**: Automatically tracks all incoming and outgoing OSC parameters
- ✅ **CSV Export**: Dump parameters to CSV with customizable format
- ✅ **Avatar-Only Filter**: Option to track only avatar parameters (ignore system messages)
- ✅ **Flexible Dumps**: Export all, incoming-only, or outgoing-only parameters
- ✅ **Memory Management**: Configurable max parameter limit to prevent memory issues
- ✅ **ChatBox Integration**: Variables, states, and events for status monitoring
- ✅ **OSC Control**: Trigger dumps and clear tracking via OSC parameters
- ✅ **Detailed Tracking**: Optionally include first seen, last update timestamps, and update counts

---

## Module Settings

### Dump Settings

- **Dump Directory**: Custom directory for CSV exports (default: `%AppData%\VRCOSC\Dumps\Debug`)
- **Include Timestamps**: Add First Seen, Last Update, and Update Count columns to CSV
- **Avatar Parameters Only**: Only track avatar parameters, ignore system OSC messages (e.g., chatbox, dolly)

### Tracking Settings

- **Auto-Track Incoming**: Automatically start tracking incoming parameters when module starts (default: enabled)
- **Auto-Track Outgoing**: Automatically start tracking outgoing parameters when module starts (default: enabled)
- **Max Parameters**: Maximum number of parameters to track (0 = unlimited, default: 0)

### Debug Settings

- **Log Parameter Updates**: Log every parameter update to console (default: disabled)

---

## Nodes

### Dump Nodes

#### `Dump All Parameters`

Dumps all tracked parameters (incoming + outgoing) to CSV file.

**Inputs:** Flow trigger

**Outputs:**
- `File Path` (string) - Path to created CSV file
- `Total Parameters` (int) - Total parameters dumped
- `Error` (string) - Error message if failed

**Flow:** Next, On Error

#### `Dump Incoming Parameters`

Dumps only incoming parameters to CSV file.

**Inputs:** Flow trigger

**Outputs:**
- `File Path` (string)
- `Parameter Count` (int)
- `Error` (string)

**Flow:** Next, On Error

#### `Dump Outgoing Parameters`

Dumps only outgoing parameters to CSV file.

**Inputs:** Flow trigger

**Outputs:**
- `File Path` (string)
- `Parameter Count` (int)
- `Error` (string)

**Flow:** Next, On Error

### Management Nodes

#### `Clear Parameter Tracking`

Clears all tracked parameters from memory.

**Inputs:** Flow trigger

**Flow:** Next

#### `Get Parameter Counts`

Gets the current count of tracked parameters.

**Inputs:** Flow trigger

**Outputs:**
- `Incoming Count` (int)
- `Outgoing Count` (int)
- `Total Count` (int)

**Flow:** Next

### Query Nodes

#### `Get Incoming Parameters`

Gets all incoming parameters as a Dictionary.

**Inputs:** Flow trigger

**Outputs:**
- `Parameters` (Dictionary<string, object>) - All incoming parameters
- `Count` (int) - Number of parameters

**Flow:** Next

**Use with:** Dictionary nodes (Key To Value, For Each, etc.)

#### `Get Outgoing Parameters`

Gets all outgoing parameters as a Dictionary.

**Inputs:** Flow trigger

**Outputs:**
- `Parameters` (Dictionary<string, object>)
- `Count` (int)

**Flow:** Next

#### `Get All Parameters`

Gets all parameters (incoming + outgoing) as a Dictionary with "IN:" and "OUT:" prefixes.

**Inputs:** Flow trigger

**Outputs:**
- `Parameters` (Dictionary<string, object>)
- `Incoming Count` (int)
- `Outgoing Count` (int)
- `Total Count` (int)

**Flow:** Next

---

## OSC Parameters

- `VRCOSC/Debug/DumpNow` (bool, Read) - Set to true to trigger parameter dump
- `VRCOSC/Debug/Clear` (bool, Read) - Set to true to clear tracked parameters
- `VRCOSC/Debug/IncomingCount` (int, Write) - Number of unique incoming parameters
- `VRCOSC/Debug/OutgoingCount` (int, Write) - Number of unique outgoing parameters
- `VRCOSC/Debug/TotalCount` (int, Write) - Total unique parameters

---

## ChatBox Integration

### Variables

- `Incoming Count` - Number of incoming parameters tracked
- `Outgoing Count` - Number of outgoing parameters tracked
- `Total Count` - Total parameters tracked
- `Last Dump Path` - Path to last created CSV file

### States

- `Idle` - "Debug\nTracking: {Total Count} params"
- `Dumping` - "Dumping {Total Count} params..."

### Events

- `On Dump Complete` - "Dumped to: {Last Dump Path}"
- `On Tracking Cleared` - "Cleared all tracked parameters"

---

## CSV Output Format

### Basic Format (Timestamps Disabled)

```csv
Direction;Parameter Path;Type;Value
Incoming;MyParameter;Int32;42
Outgoing;AnotherParam;Boolean;true
```

### Extended Format (Timestamps Enabled)

```csv
Direction;Parameter Path;Type;Value;First Seen;Last Update;Update Count
Incoming;MyParameter;Int32;42;2025-10-30 14:23:45;2025-10-30 14:25:12;15
Outgoing;AnotarParam;Boolean;true;2025-10-30 14:23:46;2025-10-30 14:24:01;3
```

### File Naming

Files are named: `params_DDMMYYYY-HH-mm-ss.csv`

Example: `params_30102025-14-23-45.csv`

---

## Usage Examples

### Example 1: Auto-Export on Button Press

```
[Button Press] ──► [Dump All Parameters]
                    ├─ File Path ──► [Display Text]
                    └─ Total Parameters ──► [Log]
```

### Example 2: Periodic Dumps

```
[Timer: 5min] ──► [Dump All Parameters]
                   └─ Next ──► [Log "Exported"]
```

### Example 3: Monitor Parameter Count

```
[Timer: 1s] ──► [Get Parameter Counts]
                 ├─ Total Count ──► [Display]
                 └─ Next ──► [Continue]
```

### Example 4: Process Parameters

```
[Trigger] ──► [Get All Parameters]
               └─ Parameters ──► [For Each]
                                  └─ Process each param
```

### Example 5: OSC-Triggered Dump

Send OSC message to `/avatar/parameters/VRCOSC/Debug/DumpNow` with value `1` to trigger a dump from external applications.

---

## Performance Notes

- **Memory Usage**: Each tracked parameter stores path, type, value, and timestamps (~100-200 bytes per parameter)
- **Typical Load**: 50-200 parameters tracked for average avatar
- **Max Recommended**: 1000-5000 parameters before performance impact
- **Dump Speed**: ~1-5ms per 100 parameters for CSV export

---

## Tips

1. **Enable "Avatar Parameters Only"** unless you specifically need system messages
2. **Set "Max Parameters"** to 1000-2000 for safety if tracking everything
3. **Disable "Log Parameter Updates"** in production (very verbose!)
4. **Use "Include Timestamps"** for debugging parameter update patterns
5. **Clear tracking periodically** if running for extended periods

---

## Troubleshooting

### Too Many Parameters Tracked

**Problem**: Module tracking thousands of parameters, causing slowdown

**Solutions**:
1. Enable "Avatar Parameters Only"
2. Set "Max Parameters" to a reasonable limit (1000-2000)
3. Use "Clear Parameter Tracking" node periodically

### CSV Files Too Large

**Problem**: CSV exports are huge

**Solutions**:
1. Use "Dump Incoming Parameters" or "Dump Outgoing Parameters" instead of "Dump All"
2. Disable "Include Timestamps" to reduce file size
3. Clear tracking before dumping to only capture recent data

### Parameters Not Being Tracked

**Problem**: Expected parameters not showing in dumps

**Solutions**:
1. Check that module is enabled and started
2. Verify "Auto-Track Incoming/Outgoing" are enabled
3. Check that "Max Parameters" limit hasn't been reached
4. Disable "Avatar Parameters Only" if tracking system messages

---

## Requirements

- VRCOSC 2025.1015.0 or later
- .NET 8.0 Windows 10.0.26100.0+
- Write permissions for dump directory

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Credits

- Built on [VRCOSC SDK](https://github.com/VolcanicArts/VRCOSC) by VolcanicArts
- OSC parameter tracking inspired by VRCOSC's built-in Avatar Parameter Tab
