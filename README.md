# Bluscream's VRCOSC Modules

Custom modules for VRCOSC including VRChat Settings, VRCX Bridge, and HTTP utilities.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Modules

### VRChat Settings

Comprehensive VRChat settings management with pulse nodes for registry and config file access.

**Features:**

- 746+ registry settings (audio, safety, avatar, network, etc.)
- Config file settings (cache, resolution, performance, etc.)
- Provider-based architecture (remote + embedded definitions)
- User ID template support (`{userId}_settingName`)
- Type-safe operations with validation
- Automatic backups before writes
- ChatBox integration (variables, states, events)
- Generic `<T>` nodes for flexible type handling

**Nodes (9):**

- `Get VRChat Registry Setting` - Read registry values
- `Get VRChat Config Setting` - Read config values
- `Set VRChat Registry Value<T>` - Write registry (generic, all types)
- `Set VRChat Config Value<T>` - Write config (generic, all types)
- `Get All VRChat Registry Settings` - List all registry → Dictionary
- `Get All VRChat Config Settings` - List all config → Dictionary
- `Get Configured VRChat User ID` - Check user ID status
- `Expand VRChat Key Template` - Manual template expansion
- `Object To JSON String<T>` - Serialize any object to JSON

**See**: [VRChatSettings/README.md](VRCOSC.Modules/VRChatSettings/README.md) for full documentation.

---

### VRCX Bridge

Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration.

**Features:**

- IPC communication with VRCX via Named Pipes
- Send OSC messages to VRChat from VRCX
- Forward OSC parameters from VRChat to VRCX
- Execute VRCX commands from VRCOSC nodes
- Auto-reconnection support
- Event batching and deduplication
- ChatBox variable management

**Nodes:**

- `VRCX Get Online Friends` - Get list of online friends
- `VRCX Send Invite` - Send world invite
- `VRCX Get User Info` - Get user information
- `VRCX Get Current Location` - Get current world/instance
- `VRCX Show Toast` - Show VRCX notification
- `VRCX Connection Status` - Check connection status

---

### HTTP

HTTP request automation with response handling.

**Features:**

- GET, POST, PUT, DELETE support
- Custom headers and body
- Response parsing
- Status code tracking
- Request counter
- ChatBox integration (states, events, variables)
- Success/failure events

**Nodes:**

- `HTTP GET Request`
- `HTTP POST Request`
- `HTTP Request` (custom method)

**ChatBox Integration:**

- Variables: Last URL, Status Code, Response, Request Count
- States: Idle, Requesting, Success, Failed
- Events: OnSuccess, OnFailed

---

## Building

### Quick Build (Debug)

```powershell
cd VRCOSC.Modules
dotnet build -c Debug
```

### Release Build

```powershell
cd VRCOSC.Modules
dotnet build -c Release
```

### Full Release Pipeline

```powershell
.\update.ps1
```

The `update.ps1` script will:

1. ✅ Bump version in `AssemblyInfo.cs` (auto-increments build number)
2. ✅ Build in Release mode
3. ✅ Commit and push changes
4. ✅ Create GitHub **pre-release (Beta)** with DLL attached (using version as tag)
5. ✅ Build in Debug mode (for local testing)

**Script Options:**

```powershell
.\update.ps1 -CommitMessage "Your message"  # Custom commit message
.\update.ps1 -SkipCommit                    # Don't commit/push
.\update.ps1 -SkipRelease                   # Don't create release
.\update.ps1 -NoPush                        # Commit but don't push
```

## Installation

### Automatic (PostBuild)

The DLL is automatically copied to:

```
%APPDATA%\VRCOSC\packages\local\Bluscream.Modules.dll
```

### Manual

Copy `bin/Release/net8.0-windows10.0.26100.0/Bluscream.Modules.dll` to `%APPDATA%\VRCOSC\packages\local\`

Restart VRCOSC to load the updated module.

## Requirements

- .NET 8.0 SDK
- VRCOSC 2025.1015.0 or later
- Windows 10.0.26100.0 or later
- VRChat installed (for VRChatSettings)
- VRCX installed (for VRCXBridge, optional)
- GitHub CLI (`gh`) for releases (optional)

## Architecture

### Provider Pattern

VRChatSettings uses a provider-based architecture:

- **Registry Provider**: Loads 746+ settings from CSV (remote + embedded)
- **Config Provider**: Loads settings from JSON schema (remote + embedded)
- **Remote-first**: Tries GitHub Gist, falls back to embedded resources

### Generic Nodes

Modules leverage VRCOSC's generic `<T>` pattern for flexible, type-safe nodes that automatically create variants for different types (int, float, bool, string, etc.).

### ChatBox Integration

All modules include:

- **Variables**: Expose data to ChatBox clips
- **States**: Show module status in ChatBox
- **Events**: Trigger ChatBox clips on actions

## License

GPL-3.0 License

## Credits

- Built on [VRCOSC SDK](https://github.com/VolcanicArts/VRCOSC) by VolcanicArts
- VRChat Registry definitions curated from community documentation
- Config schema based on VRChat's config.json structure
