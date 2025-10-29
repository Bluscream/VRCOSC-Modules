# VRChat Settings Module for VRCOSC

Comprehensive VRChat settings management module with pulse nodes for reading and writing both registry settings and config file values.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Features

### Core Capabilities

- ✅ **746+ Registry Settings**: Audio, safety, avatar, network, UI, and more
- ✅ **Config File Access**: Cache, resolution, performance settings
- ✅ **User ID Templates**: Automatic `{userId}` expansion for user-specific settings
- ✅ **Remote + Embedded Definitions**: Auto-loads from GitHub Gist with fallback
- ✅ **Type-Safe Operations**: Generic `<T>` nodes for all types
- ✅ **Validation & Safety**: Min/max limits, known settings filter
- ✅ **Automatic Backups**: Registry and config file backup before writes
- ✅ **ChatBox Integration**: Variables, states, and events

### Safety Features

- Only allows known settings by default (configurable)
- Validates values against known limits (configurable)
- Requires user ID for user-specific settings
- Automatic backup before writing (optional)
- Comprehensive error handling

---

## Module Settings

### User Configuration

- **VRChat User ID**: Your VRChat user ID (usr_xxx...) for user-specific settings. Find at vrchat.com/home/user/{your-id}

### Safety Settings

- **Allow Unknown Settings**: Work with undocumented settings (default: disabled)
- **Allow Outside Known Limits**: Set values beyond validated ranges (default: disabled)

### Definitions

- **Allow Remote Definitions**: Try loading from GitHub Gist first (default: enabled)

### Backup Settings

- **Auto Backup**: Automatically backup before writing (default: enabled)
- **Backup Directory**: Custom backup location (leave empty for default)

### Debug Settings

- **Log Operations**: Log all get/set operations to console (default: disabled)

---

## Nodes (9 Total)

### Get Nodes (2)

#### `Get VRChat Registry Setting`

Reads a value from VRChat registry settings.

**Inputs:**

- `Setting Key` (string) - Registry key name or `{userId}` template
- `User ID` (string, optional) - Override module setting for this operation

**Outputs:**

- `String Value` - Value as string
- `Int Value` - Value as integer (if numeric)
- `Float Value` - Value as float (if numeric)
- `Bool Value` - Value as boolean (if boolean)
- `Error` - Error message if failed

**Flow:** Next, On Error

#### `Get VRChat Config Setting`

Reads a value from VRChat config.json file.

**Inputs:**

- `Setting Key` (string) - Config key, supports nested keys with dots (e.g., "vrcx.customjs.loader.loadTimeout")

**Outputs:**

- Same as Registry Get node

**Flow:** Next, On Error

---

### Set Nodes (2 - Generic)

#### `Set VRChat Registry Value<T>`

Writes a value to VRChat registry settings. Generic node creates type-specific variants.

**Inputs:**

- `Setting Key` (string) - Registry key name or `{userId}` template
- `Value` (T) - Value to write (type auto-detected from connection)
- `User ID` (string, optional) - Override module setting for this operation

**Outputs:**

- `Error` - Error message if failed

**Flow:** Next, On Error

**Available Types:** int, float, bool, string, double, etc.

#### `Set VRChat Config Value<T>`

Writes a value to VRChat config.json file. Generic node creates type-specific variants.

**Inputs:**

- `Setting Key` (string) - Config key (supports nested with dots)
- `Value` (T) - Value to write

**Outputs:**

- `Error` - Error message if failed

**Flow:** Next, On Error

---

### List Nodes (2)

#### `Get All VRChat Registry Settings`

Lists all registry settings as a Dictionary (filtered by known settings if safety enabled).

**Outputs:**

- `Settings Dictionary` (Dictionary<string, object>) - All settings as dictionary
- `Settings Count` (int) - Number of settings
- `Error` - Error message if failed

**Flow:** Next, On Error

**Use with:** Dictionary nodes (Key To Value, Count, etc.)

#### `Get All VRChat Config Settings`

Lists all config file settings as a Dictionary.

**Outputs:**

- Same as Registry List node

**Flow:** Next, On Error

---

### Utility Nodes (3)

#### `Get Configured VRChat User ID`

Outputs the configured user ID from module settings.

**Outputs:**

- `User ID` (string) - Configured user ID
- `Is Configured` (bool) - Whether user ID is set
- `Is Valid` (bool) - Whether format is valid (usr_xxx)

#### `Expand VRChat Key Template`

Manually expands `{userId}` templates in strings.

**Inputs:**

- `Key Template` (string) - Key with `{userId}` placeholder

**Outputs:**

- `Expanded Key` (string) - Expanded key
- `Success` (bool) - Whether expansion succeeded
- `Error` (string) - Error message if failed

#### `Object To JSON String<T>`

Serializes any object to JSON string. Generic node for all types.

**Inputs:**

- `Input Object` (T) - Object to serialize
- `Indented` (bool) - Pretty print with indentation

**Outputs:**

- `JSON String` (string) - Serialized JSON
- `String Length` (int) - Length of JSON string

---

## Known Settings

### Registry Settings (746+ total)

**Audio (10 settings):**

- `AUDIO_MASTER` - Master Volume (0.0-1.0, float)
- `AUDIO_MASTER_ENABLED` - Master Enabled (bool)
- `AUDIO_GAME_VOICE` - Voice Volume (0.0-1.0, float)
- `AUDIO_GAME_VOICE_ENABLED` - Voice Enabled (bool)
- `AUDIO_GAME_AVATARS` - Avatar Audio (0.0-1.0, float)
- `AUDIO_GAME_AVATARS_ENABLED` - Avatar Audio Enabled (bool)
- `AUDIO_GAME_PROPS` - Props Audio (0.0-1.0, float)
- `AUDIO_GAME_PROPS_ENABLED` - Props Enabled (bool)
- `AUDIO_GAME_SFX` - SFX Volume (0.0-1.0, float)
- `AUDIO_GAME_SFX_ENABLED` - SFX Enabled (bool)
- `AUDIO_UI` - UI Volume (0.0-1.0, float)
- `AUDIO_UI_ENABLED` - UI Enabled (bool)

**Avatar (6 settings):**

- `avatarProxyAlwaysShowExplicit` - Always show explicit avatars (bool)
- `avatarProxyAlwaysShowFriends` - Always show friend avatars (bool)
- `avatarProxyShowAtRange` - Avatar show distance in meters (0-1000, float)
- `avatarProxyShowAtRangeToggle` - Enable range-based showing (bool)
- `avatarProxyShowMaxNumber` - Max avatars to show (0-200, int)
- `currentShowMaxNumberOfAvatarsEnabled` - Enable max avatar limit (bool)

**User-Specific Templates (20+ settings):**

- `{userId}_currentShowMaxNumberOfAvatarsEnabled` - Per-user max avatar limit
- `{userId}_avatarProxyShowMaxNumber` - Per-user avatar count
- `{userId}_DroneControllerSettings` - Drone camera settings
- `{userId}_UI.Emojis.CustomGroup0` - Custom emoji groups
- `{userId}_UI.RecentlyUsedEmojis` - Recent emojis
- `{userId}_OpenedQuickMenu` - Quick menu first open
- And more...

**Full list**: See [Definitions/Registry.csv](Definitions/Registry.csv) (746 settings)

### Config File Settings

**Cache:**

- `cache_size` - Cache Size in GB (1-1000, int)
- `cache_expiry_delay` - Expiry in days (1-365, int)
- `cache_directory` - Cache path (string)

**Visual:**

- `screenshot_res_width` - Screenshot width (640-7680, int)
- `screenshot_res_height` - Screenshot height (480-4320, int)
- `camera_res_width` - Camera width (640-7680, int)
- `camera_res_height` - Camera height (480-4320, int)
- `camera_spout_res_width` - Spout width (640-7680, int)
- `camera_spout_res_height` - Spout height (480-4320, int)
- `picture_output_folder` - Screenshot directory (string)

**Performance:**

- `dynamic_bone_max_affected_transform_count` - Max transforms (0-256, int)
- `dynamic_bone_max_collider_check_count` - Max colliders (0-256, int)

**Privacy:**

- `disableRichPresence` - Disable Discord integration (bool)

**VRCX:**

- `vrcx.customjs.loader.loadTimeout` - Plugin timeout in ms (1000-60000, int)

**Full schema**: See [Definitions/config.schema.json](Definitions/config.schema.json)

---

### VRCX Bridge

Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration.

**Features:**

- IPC communication with VRCX via Named Pipes
- Send OSC messages to VRChat from VRCX
- Forward OSC parameters from VRChat to VRCX
- Execute VRCX commands from VRCOSC nodes
- Auto-reconnection with configurable retry
- Event batching and deduplication
- ChatBox variable management
- Persistent variable storage

**Nodes:**

- `VRCX Get Online Friends` - Get list of online friends
- `VRCX Send Invite` - Send world invite
- `VRCX Get User Info` - Get user information
- `VRCX Get Current Location` - Get current world/instance
- `VRCX Show Toast` - Show VRCX notification
- `VRCX Connection Status` - Check connection status

**Module Settings:**

- Connection: Enabled, Auto Reconnect, Reconnect Delay
- Performance: Batch Interval, Deduplicate Events, Only Changed Values
- Debug: Log OSC Params, Log Commands, Log Raw IPC

---

### HTTP

HTTP request automation with comprehensive response handling.

**Features:**

- GET, POST, PUT, DELETE, custom methods
- Custom headers and request body
- Response parsing (body, status, headers)
- Request tracking and counting
- Timeout configuration
- ChatBox integration with states and events

**Nodes:**

- `HTTP GET Request` - Simple GET with URL
- `HTTP POST Request` - POST with URL and body
- `HTTP Request` - Custom method/headers

**ChatBox Integration:**

- **Variables**: Last URL, Status Code, Response, Request Count
- **States**: Idle, Requesting, Success (HTTP 2xx), Failed (HTTP 4xx/5xx)
- **Events**: OnSuccess, OnFailed
- **OSC Parameters**: Success, Failed, StatusCode, RequestsCount

---

## Usage Examples

### VRChat Settings Examples

**1. Set Master Volume:**

```
[Slider 0-1] ──► [Set VRChat Registry Value<float>]
                  ├─ Key: "AUDIO_MASTER"
                  ├─ Value: (from slider)
                  └─ User ID: (empty)
                     └─ Next ──► [Confirmation]
```

**2. User-Specific Setting with Template:**

```
[Button] ──► [Set VRChat Registry Value<bool>]
              ├─ Key: "{userId}_OpenedQuickMenu"
              ├─ Value: true
              └─ User ID: (uses module setting)
                 └─ Next ──► [Success]
```

**3. User-Specific with Dynamic User ID:**

```
[Get User ID] ──► userId ──► [Set Registry Value<int>]
                              ├─ Key: "{userId}_avatarProxyShowMaxNumber"
                              ├─ Value: 50
                              └─ User ID: (from input)
```

**4. List and Process Settings:**

```
[Trigger] ──► [Get All Registry Settings]
               └─ Dictionary ──► [Dictionary Key To Value]
                                  ├─ Key: "AUDIO_MASTER"
                                  └─ Value ──► [Display]
```

**5. Export Settings as JSON:**

```
[Get All Settings] ──► Dictionary ──► [Object To JSON<Dictionary>]
                                       ├─ Indented: true
                                       └─ JSON ──► [Save to File]
```

---

## ChatBox Integration

### VRChat Settings

**Variables:**

- `Last Key` - Last setting accessed
- `Last Value` - Last value read/written
- `Settings Loaded` - Total settings loaded on startup
- `Operations Count` - Successful operations counter

**States:**

- `Idle` - "VRChat Settings\nReady"
- `Reading` - "Reading: {Last Key}"
- `Writing` - "Writing: {Last Key}\n= {Last Value}"

**Events:**

- `On Setting Read` - "Read: {Last Key} = {Last Value}"
- `On Setting Write` - "Wrote: {Last Key} = {Last Value}"
- `On Error` - "Error: {Last Value}"

### HTTP Module

**Variables:**

- `Last URL`, `Last Status Code`, `Last Response`, `Requests Count`

**States:**

- `Idle`, `Requesting`, `Success`, `Failed`

**Events:**

- `On Success`, `On Failed`

---

## Technical Details

### Registry Storage

VRChat stores settings in Windows Registry:

```
HKEY_CURRENT_USER\SOFTWARE\VRChat\VRChat
```

Keys are hashed using Unity's PlayerPrefs algorithm:

```csharp
uint hash = 5381;
foreach (var c in key)
    hash = (hash * 33) ^ c;
return key + "_h" + hash;
```

**Value Types:**

- `REG_BINARY` - String data (ASCII with null terminator)
- `REG_DWORD` - 32-bit integers and booleans (0/1)
- `REG_QWORD` - 64-bit double-precision floats (0.0-1.0)

### Config File Location

```
%LocalAppData%Low\VRChat\VRChat\config.json
```

**Nested Keys:**
Use dot notation: `vrcx.customjs.loader.loadTimeout`

### User ID Templates

Template keys containing `{userId}` are automatically expanded:

```
Input:  "{userId}_OpenedQuickMenu"
Output: "usr_08082729-592d-4098-9a21-83c8dd37a844_OpenedQuickMenu"
```

**Two ways to provide User ID:**

1. **Module Setting** (global) - Configure once, use everywhere
2. **Node Input** (per-operation) - Override for specific operations

---

## Provider Architecture

### Remote-First Loading

1. **Try Remote** (with 10s timeout):

   - Registry: `https://gist.github.com/Bluscream/.../registry.csv`
   - Config: `https://gist.github.com/Bluscream/.../config.schema.json`

2. **Fallback to Embedded** (baked into DLL):
   - `Bluscream.Modules.Definitions.Registry.csv`
   - `Bluscream.Modules.Definitions.config.schema.json`

### Settings Categories

**Registry:**

- Audio, Avatar, Safety, Network, Visual, General

**Config:**

- Cache, Visual, Performance, Privacy, VRCX, General

---

## OSC Parameters

- `VRCOSC/VRChatSettings/Success` (bool) - Pulses true when operation succeeds
- `VRCOSC/VRChatSettings/Failed` (bool) - Pulses true when operation fails
- `VRCOSC/VRChatSettings/OperationsCount` (int) - Total successful operations

---

## Backup & Recovery

### Backup Files

Stored in: `%AppData%\VRCOSC\Backups\VRChatSettings\`

- Registry: `vrchat_registry_backup_YYYYMMDD_HHMMSS.reg`
- Config: `vrchat_config_backup_YYYYMMDD_HHMMSS.json`

### Restore Registry Backup

Double-click `.reg` file or:

```cmd
reg import "path\to\backup.reg"
```

### Restore Config Backup

Copy backup file to:

```
%LocalAppData%Low\VRChat\VRChat\config.json
```

---

## Advanced Usage

### Working with Dictionaries

List nodes output `Dictionary<string, object>` for use with VRCOSC's built-in collection nodes:

```
[Get All Registry Settings]
  └─ Dictionary ──► [Dictionary Count] - Count settings
                 ├─ [Dictionary Key To Value] - Extract specific value
                 ├─ [For Each] - Process each setting
                 └─ [Object To JSON] - Export as JSON
```

### Generic Type Selection

Generic `<T>` nodes automatically create variants:

```
Set VRChat Registry Value<int>      ← For integers
Set VRChat Registry Value<float>    ← For floats
Set VRChat Registry Value<bool>     ← For booleans
Set VRChat Registry Value<string>   ← For strings
```

VRCOSC shows all available types based on your graph connections!

### Error Handling

All nodes have dual flow outputs:

- **Next** - Success path
- **On Error** - Error handling path

Plus `Error` output (string) for error messages.

---

## Safety Considerations

### Default Safety (Recommended)

1. ✅ **Known Settings Only** - Only 746+ documented registry + schema-defined config settings
2. ✅ **Value Validation** - Min/max limits enforced
3. ✅ **Auto Backups** - Settings backed up before changes
4. ✅ **User ID Required** - Template keys require configured or provided user ID
5. ✅ **Type Checking** - Generic nodes ensure type safety

### Advanced Mode (Use with Caution)

Enable "Allow Unknown Settings" and "Allow Outside Known Limits" for:

- Experimental settings not yet documented
- Testing custom value ranges
- Advanced troubleshooting

⚠️ **Warning**: Incorrect values can cause VRChat instability or crashes.

---

## Requirements

- VRCOSC 2025.1015.0 or later
- .NET 8.0 Windows 10.0.26100.0+
- VRChat installed on the system
- Windows (for registry access)
- Internet connection (for remote definitions, optional)

---

## Troubleshooting

### User ID Issues

**Problem**: "User ID required for template key"

**Solutions:**

1. Configure User ID in module settings
2. Find your ID at: https://vrchat.com/home/user/{your-id} (while logged in)
3. Or provide via node's optional User ID input

### Definitions Not Loading

**Problem**: "Failed to load definitions"

**Solutions:**

1. Check internet connection (for remote loading)
2. Disable "Allow Remote Definitions" to use embedded only
3. Check module logs for specific errors

### Settings Not Found

**Problem**: "Setting 'xxx' not found"

**Solutions:**

1. Verify VRChat is installed
2. Check spelling and case sensitivity
3. Enable "Allow Unknown Settings" for undocumented settings

---

## Performance

- **Definition Loading**: ~1-3s (remote), <100ms (embedded)
- **Registry Read**: ~1-5ms per key
- **Registry Write**: ~5-10ms per key (with backup)
- **Config Read**: ~10-20ms (full file parse)
- **Config Write**: ~20-30ms (with backup)

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Credits

- Built on [VRCOSC SDK](https://github.com/VolcanicArts/VRCOSC) by VolcanicArts
- Registry definitions curated from VRChat community documentation
- Config schema based on VRChat's config.json structure
