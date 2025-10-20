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

Simple HTTP request utilities.

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
