# Bluscream's VRCOSC Modules

Custom modules for VRCOSC including VRCX Bridge and HTTP utilities.

## Modules

### VRCX Bridge

Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration.

**Features:**

- IPC communication with VRCX
- Send OSC messages to VRChat from VRCX
- Forward OSC parameters from VRChat to VRCX
- Execute VRCX commands from VRCOSC nodes
- Auto-reconnection support

**Nodes:**

- `VRCX Get Online Friends` - Get list of online friends
- `VRCX Send Invite` - Send world invite
- `VRCX Get User Info` - Get user information
- `VRCX Get Current Location` - Get current world/instance
- `VRCX Show Toast` - Show VRCX notification
- `VRCX Connection Status` - Check connection status

### HTTP Module

Send HTTP requests and receive responses for automation.

**Features:**

- HTTP GET, POST, PUT, DELETE requests
- Custom headers and request body support
- Configurable timeout settings
- Request logging and status monitoring
- OSC parameters for success/failure status

**Parameters:**

- `VRCOSC/HTTP/Success` - True for 1 second when request succeeds
- `VRCOSC/HTTP/Failed` - True for 1 second when request fails
- `VRCOSC/HTTP/StatusCode` - Last HTTP status code
- `VRCOSC/HTTP/RequestsCount` - Total number of successful requests

### VRChat Settings

Read and write VRChat registry settings and config file values through pulse nodes.

**Features:**

- **Registry Settings Access**: Read and write VRChat registry settings (audio volumes, etc.)
- **Config File Access**: Read and write VRChat config.json settings (cache, resolution, etc.)
- **Safety Features**:
  - Only allows known settings by default (configurable)
  - Validates values against known limits (configurable)
  - Automatic backup before writing (optional)
- **Type Support**: String, Int, Float, and Bool values
- **Comprehensive Nodes**: Multiple node types for different use cases

**Nodes:**

- `Get Registry Setting` - Read VRChat registry values
- `Set Registry Setting` - Write VRChat registry values
- `Get Config Setting` - Read VRChat config.json values
- `Set Config Setting` - Write VRChat config.json values
- `List All Registry Settings` - List all available registry settings
- `List All Config Settings` - List all available config settings

**Parameters:**

- `VRCOSC/VRChatSettings/Success` - True for 1 second when operation succeeds
- `VRCOSC/VRChatSettings/Failed` - True for 1 second when operation fails
- `VRCOSC/VRChatSettings/LastError` - Last error message (string)

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
.\update.ps1 -Publish
```

## Installation

The DLL is automatically copied to:

```
%APPDATA%\VRCOSC\packages\local\Bluscream.Modules.dll
```

Restart VRCOSC to load the updated module.

## Requirements

- .NET 8.0 SDK
- VRCOSC SDK (installed via NuGet)
- GitHub CLI (`gh`) for releases (optional)

## License

GPL-3.0 License
