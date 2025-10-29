# VRChat Settings Module for VRCOSC

A VRCOSC module that allows reading and writing VRChat Registry Settings and config file values through pulse nodes.

## Features

- **Registry Settings Access**: Read and write VRChat registry settings (audio volumes, etc.)
- **Config File Access**: Read and write VRChat config.json settings (cache, resolution, etc.)
- **Safety Features**:
  - Only allows known settings by default (configurable)
  - Validates values against known limits (configurable)
  - Automatic backup before writing (optional)
- **Type Support**: String, Int, Float, and Bool values
- **Comprehensive Nodes**: Multiple node types for different use cases

## Module Settings

### Safety Settings

- **Allow Unknown Settings**: Enable to read/write settings not in the known list (default: disabled)
- **Allow Outside Known Limits**: Enable to set values outside known safe limits (default: disabled)

### Backup Settings

- **Auto Backup**: Automatically backup settings before writing (default: enabled)
- **Backup Directory**: Custom directory for backups (leave empty for default)

### Definition Settings

- **Allow Remote Definitions**: Try to load definitions from GitHub Gist (fallback to embedded) (default: disabled)

### Debug Settings

- **Log Operations**: Log all get/set operations to console (default: disabled)

## Available Nodes

### Registry Nodes

#### Get Registry Setting

Reads a value from VRChat registry settings.

- **Inputs**: Setting Key (string)
- **Outputs**: String Value, Int Value, Float Value, Bool Value, Error
- **Flow**: Next, On Error

#### Set Registry Setting

Writes a value to VRChat registry settings.

- **Inputs**: Setting Key (string), Value Type (string), Value (string)
- **Outputs**: Error
- **Flow**: Next, On Error

#### Typed Set Nodes

- **Set Registry Int**: Set an integer value
- **Set Registry Float**: Set a float value
- **Set Registry Bool**: Set a boolean value

#### List All Registry Settings

Lists all registry settings (filtered by known settings if safety enabled).

- **Outputs**: Settings List (JSON), Settings Count, Error
- **Flow**: Next, On Error

### Config File Nodes

#### Get Config Setting

Reads a value from VRChat config.json file.

- **Inputs**: Setting Key (string, supports nested keys with dots)
- **Outputs**: String Value, Int Value, Float Value, Bool Value, Error
- **Flow**: Next, On Error

#### Set Config Setting

Writes a value to VRChat config.json file.

- **Inputs**: Setting Key (string), Value Type (string), Value (string)
- **Outputs**: Error
- **Flow**: Next, On Error

#### Typed Set Nodes

- **Set Config Int**: Set an integer value
- **Set Config Float**: Set a float value
- **Set Config Bool**: Set a boolean value

#### List All Config Settings

Lists all config file settings (filtered by known settings if safety enabled).

- **Outputs**: Settings List (JSON), Settings Count, Error
- **Flow**: Next, On Error

## Known Settings

### Registry Settings (Audio)

- `AUDIO_MASTER` - Master Volume (0.0-1.0)
- `AUDIO_MASTER_ENABLED` - Master Volume Enabled (bool)
- `AUDIO_GAME_VOICE` - Voice Volume (0.0-1.0)
- `AUDIO_GAME_VOICE_ENABLED` - Voice Enabled (bool)
- `AUDIO_GAME_AVATARS` - Avatar Audio Volume (0.0-1.0)
- `AUDIO_GAME_AVATARS_ENABLED` - Avatar Audio Enabled (bool)
- `AUDIO_GAME_SFX` - SFX Volume (0.0-1.0)
- `AUDIO_GAME_SFX_ENABLED` - SFX Enabled (bool)
- `AUDIO_UI` - UI Volume (0.0-1.0)
- `AUDIO_UI_ENABLED` - UI Audio Enabled (bool)

### Config File Settings

- `cache_size` - Cache Size (1-1000)
- `cache_expiry_delay` - Cache Expiry Delay in days (1-365)
- `cache_directory` - Cache Directory path (string)
- `screenshot_res_width` - Screenshot Width (640-7680)
- `screenshot_res_height` - Screenshot Height (480-4320)
- `camera_res_width` - Camera Width (640-7680)
- `camera_res_height` - Camera Height (480-4320)
- `camera_spout_res_width` - Camera Spout Width (640-7680)
- `camera_spout_res_height` - Camera Spout Height (480-4320)
- `dynamic_bone_max_affected_transform_count` - Max Dynamic Bone Transforms (0-256)
- `dynamic_bone_max_collider_check_count` - Max Dynamic Bone Colliders (0-256)

## Usage Examples

### Example 1: Get Master Volume

```
[Get Registry Setting]
  Setting Key: "AUDIO_MASTER"
  -> Float Value -> [Display Value]
```

### Example 2: Set Voice Volume to 75%

```
[Trigger] -> [Set Registry Float]
  Setting Key: "AUDIO_GAME_VOICE"
  Value: 0.75
  -> Next -> [Show Success]
```

### Example 3: Change Screenshot Resolution

```
[Trigger] -> [Set Config Int]
  Setting Key: "screenshot_res_width"
  Value: 3840
  -> Next -> [Set Config Int]
    Setting Key: "screenshot_res_height"
    Value: 2160
```

### Example 4: List All Settings

```
[Trigger] -> [List All Registry Settings]
  -> Settings List (JSON) -> [Display]
  -> Settings Count -> [Display Count]
```

## Safety Considerations

1. **Known Settings Only**: By default, the module only allows interaction with known, safe settings. This prevents accidental corruption of VRChat configuration.

2. **Value Validation**: When working with known settings, values are validated against defined min/max limits to prevent invalid configurations.

3. **Automatic Backups**: The module can automatically backup both registry and config file before making changes.

4. **Override Options**: Advanced users can enable "Allow Unknown Settings" and "Allow Outside Known Limits" for full control, but this should be used with caution.

## Backup Location

Backups are stored in:

- Default: `%AppData%\VRCOSC\Backups\VRChatSettings\`
- Custom: As specified in module settings

Backup files are timestamped:

- Registry: `vrchat_registry_backup_YYYYMMDD_HHMMSS.reg`
- Config: `vrchat_config_backup_YYYYMMDD_HHMMSS.json`

## Parameters

The module exposes the following OSC parameters:

- `VRCOSC/VRChatSettings/Success` - True for 1 second when operation succeeds
- `VRCOSC/VRChatSettings/Failed` - True for 1 second when operation fails
- `VRCOSC/VRChatSettings/OperationsCount` - Total number of successful operations

## Variables

The module provides the following variables:

- `Last Key` - Last setting key accessed
- `Last Value` - Last value read/written
- `Settings Loaded` - Number of settings definitions loaded
- `Operations Count` - Total number of operations performed

## States

The module has the following states:

- `Idle` - Module ready
- `Reading` - Currently reading a setting
- `Writing` - Currently writing a setting

## Events

The module triggers the following events:

- `On Setting Read` - Triggered when a setting is read
- `On Setting Write` - Triggered when a setting is written

## Technical Details

### Registry Storage

VRChat stores settings in the Windows Registry under:

```
HKEY_CURRENT_USER\SOFTWARE\VRChat\VRChat
```

Keys are hashed using Unity's PlayerPrefs algorithm (key_h{hash}).

### Config File Location

VRChat config file is located at:

```
%LocalAppData%Low\VRChat\VRChat\config.json
```

### Nested Config Keys

Config file settings support nested keys using dot notation:

```
vrcx.customjs.loader.loadTimeout
```

## Requirements

- VRCOSC 2025.1015.0 or later
- VRChat installed on the system
- Windows (for registry access)

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
