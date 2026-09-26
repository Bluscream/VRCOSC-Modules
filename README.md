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
- Builds Release DLL with a working .NET SDK (host, or the build-box distrobox container)
- Deploys DLL + dependencies (`Silk.NET.*`) to active target roaming directory
- Deploys native `openxr_loader.dll` from SteamVR to VRCOSC app dir
- Regenerates code map docs (`python3 tools/gen-docs.py`)
- Regenerates module READMEs (`python3 tools/gen-readmes.py`)
- Commits, tags, and creates GitHub Release (`gh release create`)

### Target Channel Switches:
- `./update.sh` — Target **Stable** VRCOSC (`2026.501.0`)
- `./update.sh --beta` — Target **Beta** VRCOSC (`2026.702.0`, published as Pre-Release)
- `./update.sh --dev` — Target **Dev** VRCOSC (local build deploy only)
- Add `-r / --skip-release` to skip GitHub release upload.

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Submodules Index

<!-- AUTOGEN:SUBMODULES:START -->
| Module Name | Folder / Docs | Settings | Variables | States | Events | Description |
|---|---|---|---|---|---|---|
| **Debug Module** | `[VRCOSC.Modules/Debug/README.md](VRCOSC.Modules/Debug/README.md)` | `8` | `4` | 2 | 2 | Debug tools for tracking and exporting OSC parameters with CSV exports, Harmony patches for Linux/Wine connection log spam, WinRT file picker fixes, and ChatBox validation protection. |
| **Desktop FPS Module** | `[VRCOSC.Modules/DesktopFPS/README.md](VRCOSC.Modules/DesktopFPS/README.md)` | `0` | `1` | 0 | 0 | Monitors VRChat desktop / window FPS using high-precision process frame timing and performance counters. |
| **HTTP Module** | `[VRCOSC.Modules/HTTP/README.md](VRCOSC.Modules/HTTP/README.md)` | `3` | `4` | 4 | 2 | Send HTTP requests (GET, POST, PUT, DELETE) and receive responses for web automation and API integration. |
| **HTTP / MCP Server Module** | `[VRCOSC.Modules/HTTPServer/README.md](VRCOSC.Modules/HTTPServer/README.md)` | `7` | `5` | 5 | 5 | Embedded REST API & Model Context Protocol (MCP) server allowing external web applications, local scripts, or AI Agents to query and control VRCOSC. |
| **Home Assistant Module** | `[VRCOSC.Modules/HomeAssistant/README.md](VRCOSC.Modules/HomeAssistant/README.md)` | `10` | `6` | 4 | 3 | Integrate Home Assistant entity states, Jinja templates, avatar parameters, custom HomeAssistantEntityClipVariable, and flow nodes via REST & WebSocket APIs. |
| **IRC Bridge Module** | `[VRCOSC.Modules/IRCBridge/README.md](VRCOSC.Modules/IRCBridge/README.md)` | `10` | `9` | 6 | 9 | Connect to IRC networks and Twitch IRC for chat integration, channel tracking, and pulse nodes. |
| **Linux Hardware Stats Module** | `[VRCOSC.Modules/LinuxHardwareStats/README.md](VRCOSC.Modules/LinuxHardwareStats/README.md)` | `6` | `27` | 1 | 0 | Linux-native hardware monitoring module. Reads CPU, GPU, RAM, VRAM, network speeds, temperatures, active window title/FPS (via MangoHud / xdotool / kdotool), and VR compositor mode (SteamVR / Monado / WiVRn) directly from host via embedded vrcosc_hwstats.sh script. |
| **Linux Media Module** | `[VRCOSC.Modules/LinuxMedia/README.md](VRCOSC.Modules/LinuxMedia/README.md)` | `0` | `8` | 3 | 3 | Integrates with Linux MPRIS Media Players via D-Bus and vrcosc_mpris_query.sh script for player control and track info in ChatBox clips. |
| **Linux Process Manager Module** | `[VRCOSC.Modules/LinuxProcessManager/README.md](VRCOSC.Modules/LinuxProcessManager/README.md)` | `0` | `0` | 0 | 0 | Allows starting, stopping, and restarting Linux host processes directly from avatar OSC parameters and flow nodes. |
| **Notifications Module** | `[VRCOSC.Modules/Notifications/README.md](VRCOSC.Modules/Notifications/README.md)` | `11` | `4` | 2 | 2 | Send notifications to Windows Desktop toasts, XSOverlay (UDP 42010), OVRToolkit (WebSocket 15000), and Webhooks. |
| **OpenXR Modules** | `[VRCOSC.Modules/OpenXR/README.md](VRCOSC.Modules/OpenXR/README.md)` | `1` | `7` | 3 | 0 | Cross-platform OpenXR integration providing runtime statistics (FPS, frame timing, VRAM), hand tracking gestures (XR_EXT_hand_tracking), and haptic controller feedback via native openxr_loader.dll. |
| **VRCX Bridge Module** | `[VRCOSC.Modules/VRCXBridge/README.md](VRCOSC.Modules/VRCXBridge/README.md)` | `4` | `8` | 2 | 1 | Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration via Windows Named Pipes (\\.\pipe\vrcx-ipc). |
| **VRChat Settings Module** | `[VRCOSC.Modules/VRChatSettings/README.md](VRCOSC.Modules/VRChatSettings/README.md)` | `6` | `4` | 3 | 3 | Read and write 746+ VRChat registry settings and config file values with provider architecture, JSON schema validation, and user ID templates. |
<!-- AUTOGEN:SUBMODULES:END -->

## Submodules Index

<!-- SUBMODULES_TABLE_START -->
| Module Name | Folder / Docs | Settings | Variables | States | Events | Description |
|---|---|---|---|---|---|---|
| **Debug** | `[VRCOSC.Modules/Debug/README.md](VRCOSC.Modules/Debug/README.md)` | `16` | `4` | 2 | 2 | Debug tools for tracking and exporting OSC parameters |
| **Desktop FPS** | `[VRCOSC.Modules/DesktopFPS/README.md](VRCOSC.Modules/DesktopFPS/README.md)` | `0` | `1` | 0 | 0 | Monitors VRChat FPS using Windows Performance Counters |
| **Discord Voice** | `[VRCOSC.Modules/DiscordVoice/README.md](VRCOSC.Modules/DiscordVoice/README.md)` | `4` | `15` | 1 | 18 | Mute, deafen and voice-channel state for the running Discord client over its local RPC socket. Fork of Yeusepe's DiscordOSC. |
| **HTTP** | `[VRCOSC.Modules/HTTP/README.md](VRCOSC.Modules/HTTP/README.md)` | `2` | `4` | 4 | 2 | Send HTTP requests and receive responses for automation |
| **HTTP/MCP Server** | `[VRCOSC.Modules/HTTPServer/README.md](VRCOSC.Modules/HTTPServer/README.md)` | `9` | `5` | 5 | 5 | HTTP/MCP server to control VRCOSC via HTTP Requests or from a AI Agent via MCP (optional) |
| **Heartrate Stats** | `[VRCOSC.Modules/HeartrateStats/README.md](VRCOSC.Modules/HeartrateStats/README.md)` | `12` | `6` | 1 | 2 | Heart rate from Pulsoid, HypeRate or any OSC parameter, with session min/max and a trend arrow as ChatBox variables. Sends the same avatar parameters as the official heartrate modules. |
| **HomeAssistant** | `[VRCOSC.Modules/HomeAssistant/README.md](VRCOSC.Modules/HomeAssistant/README.md)` | `10` | `4` | 4 | 3 | Integrate Home Assistant entity states, Jinja templates, avatar parameters, and flow nodes |
| **IRC Bridge** | `[VRCOSC.Modules/IRCBridge/README.md](VRCOSC.Modules/IRCBridge/README.md)` | `16` | `9` | 6 | 9 | Connect to IRC servers and receive events for channel activity |
| **Linux Audio FX** | `[VRCOSC.Modules/LinuxAudioFx/README.md](VRCOSC.Modules/LinuxAudioFx/README.md)` | `4` | `4` | 2 | 1 | Soundboard (Soundux, Kenku, ...) and voice changer (EasyEffects, PipeWire filters) state on Linux hosts — MagicChatbox soundpad_* / voicemod_* counterparts |
| **Linux Hardware Stats** | `[VRCOSC.Modules/LinuxHardwareStats/README.md](VRCOSC.Modules/LinuxHardwareStats/README.md)` | `6` | `39` | 1 | 0 | Sends hardware stats as avatar parameters and allows for displaying them in the ChatBox on Linux hosts |
| **Linux Media** | `[VRCOSC.Modules/LinuxMedia/README.md](VRCOSC.Modules/LinuxMedia/README.md)` | `1` | `11` | 3 | 3 | Integration with Linux MPRIS Media Players (via D-Bus) |
| **Linux Process Manager** | `[VRCOSC.Modules/LinuxProcessManager/README.md](VRCOSC.Modules/LinuxProcessManager/README.md)` | `0` | `0` | 0 | 0 | Allows for starting and stopping Linux host processes from avatar parameters |
| **MagicChatbox Parity** | `[VRCOSC.Modules/MCBParity/README.md](VRCOSC.Modules/MCBParity/README.md)` | `7` | `10` | 3 | 0 | Weather feels-like/wind/emoji, VRChat instance master and capacity, and VR reprojection/dropped-frame estimates as ChatBox variables (MagicChatbox placeholder parity) |
| **Notifications** | `[VRCOSC.Modules/Notifications/README.md](VRCOSC.Modules/Notifications/README.md)` | `11` | `4` | 0 | 2 | Send notifications to Desktop, XSOverlay, and OVRToolkit |
| **OpenXR Gesture Extensions** | `[VRCOSC.Modules/OpenXR/README.md](VRCOSC.Modules/OpenXR/README.md)` | `2` | `12` | 2 | 0 | Detect a range of custom gestures from OpenXR hand tracking or controllers |
| **Status** | `[VRCOSC.Modules/Status/README.md](VRCOSC.Modules/Status/README.md)` | `6` | `6` | 1 | 1 | A list of status texts shown one at a time in the ChatBox, with optional cycling, groups and an icon prefix |
| **TikTok Live** | `[VRCOSC.Modules/TikTokLive/README.md](VRCOSC.Modules/TikTokLive/README.md)` | `5` | `5` | 2 | 2 | Viewer, like and follower counts plus a live flag for a TikTok host, polled from TikTok's public pages (no login needed) |
| **Twitch Stats** | `[VRCOSC.Modules/TwitchStats/README.md](VRCOSC.Modules/TwitchStats/README.md)` | `4` | `7` | 2 | 2 | Live status, game, title, viewers, followers and uptime of a Twitch channel for the ChatBox. Logs in through the Twitch device code flow. |
| **VRChat Extras** | `[VRCOSC.Modules/VRCExtras/README.md](VRCOSC.Modules/VRCExtras/README.md)` | `1` | `10` | 1 | 1 | Current world, instance type, region and player count as ChatBox variables, straight from VRCOSC's VRChat log reader |
| **VRCX Bridge** | `[VRCOSC.Modules/VRCXBridge/README.md](VRCOSC.Modules/VRCXBridge/README.md)` | `10` | `0` | 0 | 0 | Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration |
| **VRChat Settings** | `[VRCOSC.Modules/VRChatSettings/README.md](VRCOSC.Modules/VRChatSettings/README.md)` | `7` | `4` | 3 | 3 | Read and write VRChat registry settings and config file values |
| **Open-Meteo Weather** | `[VRCOSC.Modules/Weather/README.md](VRCOSC.Modules/Weather/README.md)` | `1` | `12` | 2 | 0 | Temperature, feels-like, wind, humidity, condition and a weather emoji from Open-Meteo. No API key needed. |
<!-- SUBMODULES_TABLE_END -->
