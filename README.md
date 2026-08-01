# Bluscream's VRCOSC Modules

Custom modules for VRCOSC including Home Assistant integration, Linux hardware stats, VRChat settings, VRCX bridge, HTTP server, notifications, and more.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## Submodules Index

| Module Name | Folder / Docs | Settings | Variables | States | Events | Description |
|---|---|---|---|---|---|---|
| **Debug Module** | [VRCOSC.Modules/Debug/README.md](VRCOSC.Modules/Debug/README.md) | 8 | 4 | 2 | 2 | Debug tools for tracking and exporting OSC parameters with CSV exports, Harmony patches for Linux/Wine connection log spam, WinRT file picker fixes, and ChatBox validation protection. |
| **Desktop FPS Module** | [VRCOSC.Modules/DesktopFPS/README.md](VRCOSC.Modules/DesktopFPS/README.md) | 0 | 1 | 0 | 0 | Monitors VRChat desktop / window FPS using high-precision process frame timing and performance counters. |
| **HTTP Module** | [VRCOSC.Modules/HTTP/README.md](VRCOSC.Modules/HTTP/README.md) | 3 | 4 | 4 | 2 | Send HTTP requests (GET, POST, PUT, DELETE) and receive responses for web automation and API integration. |
| **HTTP / MCP Server Module** | [VRCOSC.Modules/HTTPServer/README.md](VRCOSC.Modules/HTTPServer/README.md) | 7 | 5 | 5 | 5 | Embedded REST API & Model Context Protocol (MCP) server allowing external web applications, local scripts, or AI Agents to query and control VRCOSC. |
| **Home Assistant Module** | [VRCOSC.Modules/HomeAssistant/README.md](VRCOSC.Modules/HomeAssistant/README.md) | 10 | 6 | 4 | 3 | Integrate Home Assistant entity states, Jinja templates, avatar parameters, custom HomeAssistantEntityClipVariable, and flow nodes via REST & WebSocket APIs. |
| **IRC Bridge Module** | [VRCOSC.Modules/IRCBridge/README.md](VRCOSC.Modules/IRCBridge/README.md) | 10 | 9 | 6 | 9 | Connect to IRC networks and Twitch IRC for chat integration, channel tracking, and pulse nodes. |
| **Linux Hardware Stats Module** | [VRCOSC.Modules/LinuxHardwareStats/README.md](VRCOSC.Modules/LinuxHardwareStats/README.md) | 6 | 27 | 1 | 0 | Linux-native hardware monitoring module. Reads CPU, GPU, RAM, VRAM, network speeds, temperatures, active window title/FPS (via MangoHud / xdotool / kdotool), and VR compositor mode (SteamVR / Monado / WiVRn) directly from host via embedded vrcosc_hwstats.sh script. |
| **Linux Media Module** | [VRCOSC.Modules/LinuxMedia/README.md](VRCOSC.Modules/LinuxMedia/README.md) | 0 | 8 | 3 | 3 | Integrates with Linux MPRIS Media Players via D-Bus and vrcosc_mpris_query.sh script for player control and track info in ChatBox clips. |
| **Linux Process Manager Module** | [VRCOSC.Modules/LinuxProcessManager/README.md](VRCOSC.Modules/LinuxProcessManager/README.md) | 0 | 0 | 0 | 0 | Allows starting, stopping, and restarting Linux host processes directly from avatar OSC parameters and flow nodes. |
| **Notifications Module** | [VRCOSC.Modules/Notifications/README.md](VRCOSC.Modules/Notifications/README.md) | 11 | 4 | 2 | 2 | Send notifications to Windows Desktop toasts, XSOverlay (UDP 42010), OVRToolkit (WebSocket 15000), and Webhooks. |
| **OpenXR Modules** | [VRCOSC.Modules/OpenXR/README.md](VRCOSC.Modules/OpenXR/README.md) | 1 | 7 | 3 | 0 | Cross-platform OpenXR integration providing runtime statistics (FPS, frame timing, VRAM), hand tracking gestures (XR_EXT_hand_tracking), and haptic controller feedback via native openxr_loader.dll. |
| **VRCX Bridge Module** | [VRCOSC.Modules/VRCXBridge/README.md](VRCOSC.Modules/VRCXBridge/README.md) | 4 | 8 | 2 | 1 | Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration via Windows Named Pipes (\\.\pipe\vrcx-ipc). |
| **VRChat Settings Module** | [VRCOSC.Modules/VRChatSettings/README.md](VRCOSC.Modules/VRChatSettings/README.md) | 6 | 4 | 3 | 3 | Read and write 746+ VRChat registry settings and config file values with provider architecture, JSON schema validation, and user ID templates. |

---

## Codebase Map & Documentation

The [`docs/`](docs/) directory contains generated reference maps of all symbols, classes, methods, properties, and events across the entire repository:

- 📐 [Classes Map](docs/classes.md) — Map of all classes, structs, interfaces, and enums.
- ⚙️ [Methods Map](docs/methods.md) — Map of all methods and constructors.
- 🔧 [Properties Map](docs/properties.md) — Map of all properties.
- 📌 [Fields Map](docs/fields.md) — Map of all fields and node pins.
- 💬 [ChatBox Events Map](docs/chatbox-events.md) — Map of all ChatBox events.
- ⚡ [Code Events Map](docs/events.md) — Map of all C# events, delegates, and callbacks.

---

## Building & Deploying

### Linux Container Pipeline (`update.sh`)

```bash
cd tools && ./update.sh
```

The `update.sh` script automates the full workflow:
- Stops running VRCOSC instance
- Auto-bumps build version in `AssemblyInfo.cs`
- Builds Release DLL in Arch container (`distrobox-enter -n arch -- dotnet build ...`)
- Deploys DLL + dependencies (`Silk.NET.*`) to active target roaming directory
- Deploys native `openxr_loader.dll` from SteamVR to VRCOSC app dir
- Regenerates code map docs (`python3 tools/gen-docs.py`)
- Commits, tags, and creates GitHub Release (`gh release create`)

### Target Channel Switches:
- `./update.sh` — Target **Stable** VRCOSC (`2026.501.0`)
- `./update.sh --beta` — Target **Beta** VRCOSC (`2026.702.0`, published as Pre-Release)
- `./update.sh --dev` — Target **Dev** VRCOSC (local build deploy only)
- Add `-r / --skip-release` to skip GitHub release upload.

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
