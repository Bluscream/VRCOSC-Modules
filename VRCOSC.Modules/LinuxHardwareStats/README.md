# Linux Hardware Stats Module

Linux-native hardware monitoring module. Reads CPU, GPU, RAM, VRAM, network speeds, temperatures, active window title/FPS (via MangoHud / xdotool / kdotool), and VR compositor mode (SteamVR / Monado / WiVRn) directly from host via embedded vrcosc_hwstats.sh script.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- Linux host running VRCOSC (under Proton/Wine or native container).
- Automatically deploys `vrcosc_hwstats.sh` to `~/.local/bin/`.
- Optional: MangoHud configured with `~/.config/MangoHud/MangoHud.conf` (`autostart_log=1`) for real-time process FPS tracking.
- Optional: `xdotool` or `kdotool` installed for active window title detection.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **RefreshIntervalMs** | `Slider` | Script sampling interval in milliseconds | `1000` |
| **GpuIndex** | `TextBox` | 0-based index of GPU package to monitor | `0` |
| **CpuIndex** | `TextBox` | 0-based index of CPU package to monitor | `0` |
| **NetIface** | `TextBox` | Network interface to monitor (empty = combine all non-loopback) | `empty` |
| **EnableOsc** | `Toggle` | Publish hardware stats to avatar OSC parameters | `true` |
| **LogDebug** | `Toggle` | Log script output details to console | `false` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **CPU Usage** | `cpuusage` | `int` | CPU load percentage (0-100%) |
| **CPU Power** | `cpupower` | `float` | CPU package power draw in Watts |
| **CPU Temp** | `cputemp` | `int` | CPU package temperature in °C |
| **GPU Usage** | `gpuusage` | `int` | GPU core load percentage (0-100%) |
| **GPU Power** | `gpupower` | `float` | GPU power draw in Watts |
| **GPU Temp** | `gputemp` | `int` | GPU core temperature in °C |
| **RAM Usage** | `ramusage` | `int` | System RAM load percentage (0-100%) |
| **RAM Total** | `ramtotal` | `int` | Total system RAM in MB |
| **RAM Used** | `ramused` | `int` | Used system RAM in MB |
| **RAM Free** | `ramfree` | `int` | Free system RAM in MB |
| **VRAM Usage** | `vramusage` | `int` | GPU VRAM load percentage (0-100%) |
| **VRAM Total** | `vramtotal` | `int` | Total VRAM in MB |
| **VRAM Used** | `vramused` | `int` | Used VRAM in MB |
| **VRAM Free** | `vramfree` | `int` | Free VRAM in MB |
| **CPU Name** | `cpuname` | `string` | CPU model name string |
| **GPU Name** | `gpuname` | `string` | GPU model name string |
| **Net Rx KiB/s** | `netrxkibps` | `float` | Network download speed in KiB/s |
| **Net Tx KiB/s** | `nettxkibps` | `float` | Network upload speed in KiB/s |
| **Net Rx Total MB** | `netrxtotalmb` | `float` | Total downloaded MB |
| **Net Tx Total MB** | `nettxtotalmb` | `float` | Total uploaded MB |
| **System Temp** | `systemtemp` | `int` | Motherboard / ACPI system temperature in °C |
| **Max Temp** | `maxtemp` | `int` | Highest temperature across all hwmon sensors in °C |
| **Window Title** | `windowtitle` | `string` | Title of the currently active desktop window |
| **Process Name** | `processname` | `string` | Process executable name of active window |
| **Window FPS** | `windowfps` | `int` | Active window FPS (MangoHud CSV log or monitor refresh rate) |
| **VR Mode** | `vrmode` | `string` | Active VR compositor mode: Desktop, SteamVR, or OpenXR |
| **VRChat Running** | `vrchatrunning` | `bool` | True if VRChat.exe process is detected |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `CPU: {0}% | GPU: {3}% | RAM: {6}%` | Default hardware monitoring state |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | No ChatBox events provided. |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/Hardware/CPU/Usage` | `int` | `Write` | CPU load percentage |
| `VRCOSC/Hardware/CPU/Temp` | `int` | `Write` | CPU temperature in °C |
| `VRCOSC/Hardware/CPU/Power` | `float` | `Write` | CPU power draw in W |
| `VRCOSC/Hardware/GPU/Usage` | `int` | `Write` | GPU load percentage |
| `VRCOSC/Hardware/GPU/Temp` | `int` | `Write` | GPU temperature in °C |
| `VRCOSC/Hardware/GPU/Power` | `float` | `Write` | GPU power draw in W |
| `VRCOSC/Hardware/RAM/Usage` | `int` | `Write` | RAM usage percentage |
| `VRCOSC/Hardware/VRAM/Usage` | `int` | `Write` | VRAM usage percentage |
| `VRCOSC/Hardware/Network/RxKiBps` | `float` | `Write` | Network download speed (KiB/s) |
| `VRCOSC/Hardware/Network/TxKiBps` | `float` | `Write` | Network upload speed (KiB/s) |
| `VRCOSC/Hardware/Window/FPS` | `int` | `Write` | Active window rendering FPS |
| `VRCOSC/Hardware/VR/Mode` | `string` | `Write` | VR Compositor state (Desktop/SteamVR/OpenXR) |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get Linux Hardware Stats** | Flow trigger | CPU Usage (int), GPU Usage (int), RAM Usage (int), VRAM Usage (int) | Returns main hardware metrics |
| **Get Active Window Info** | Flow trigger | Window Title (string), Process Name (string), FPS (int) | Returns active desktop window details |
| **Get Linux VR Mode** | Flow trigger | VR Mode (string), VRChat Running (bool) | Returns current VR compositor mode |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **RefreshIntervalMs** | `Slider` | `Script sampling interval in milliseconds` | `1000` |
| **GpuIndex** | `TextBox` | `0-based index of GPU package to monitor` | `0` |
| **CpuIndex** | `TextBox` | `0-based index of CPU package to monitor` | `0` |
| **NetIface** | `TextBox` | `Network interface to monitor (empty = combine all non-loopback)` | `empty` |
| **EnableOsc** | `Toggle` | `Publish hardware stats to avatar OSC parameters` | `true` |
| **LogDebug** | `Toggle` | `Log script output details to console` | `false` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **CPU Usage** | `cpuusage` | `int` | `CPU load percentage (0-100%)` |
| **CPU Power** | `cpupower` | `float` | `CPU package power draw in Watts` |
| **CPU Temp** | `cputemp` | `int` | `CPU package temperature in °C` |
| **GPU Usage** | `gpuusage` | `int` | `GPU core load percentage (0-100%)` |
| **GPU Power** | `gpupower` | `float` | `GPU power draw in Watts` |
| **GPU Temp** | `gputemp` | `int` | `GPU core temperature in °C` |
| **RAM Usage** | `ramusage` | `int` | `System RAM load percentage (0-100%)` |
| **RAM Total** | `ramtotal` | `int` | `Total system RAM in MB` |
| **RAM Used** | `ramused` | `int` | `Used system RAM in MB` |
| **RAM Free** | `ramfree` | `int` | `Free system RAM in MB` |
| **VRAM Usage** | `vramusage` | `int` | `GPU VRAM load percentage (0-100%)` |
| **VRAM Total** | `vramtotal` | `int` | `Total VRAM in MB` |
| **VRAM Used** | `vramused` | `int` | `Used VRAM in MB` |
| **VRAM Free** | `vramfree` | `int` | `Free VRAM in MB` |
| **CPU Name** | `cpuname` | `string` | `CPU model name string` |
| **GPU Name** | `gpuname` | `string` | `GPU model name string` |
| **Net Rx KiB/s** | `netrxkibps` | `float` | `Network download speed in KiB/s` |
| **Net Tx KiB/s** | `nettxkibps` | `float` | `Network upload speed in KiB/s` |
| **Net Rx Total MB** | `netrxtotalmb` | `float` | `Total downloaded MB` |
| **Net Tx Total MB** | `nettxtotalmb` | `float` | `Total uploaded MB` |
| **System Temp** | `systemtemp` | `int` | `Motherboard / ACPI system temperature in °C` |
| **Max Temp** | `maxtemp` | `int` | `Highest temperature across all hwmon sensors in °C` |
| **Window Title** | `windowtitle` | `string` | `Title of the currently active desktop window` |
| **Process Name** | `processname` | `string` | `Process executable name of active window` |
| **Window FPS** | `windowfps` | `int` | `Active window FPS (MangoHud CSV log or monitor refresh rate)` |
| **VR Mode** | `vrmode` | `string` | `Active VR compositor mode: Desktop, SteamVR, or OpenXR` |
| **VRChat Running** | `vrchatrunning` | `bool` | `True if VRChat.exe process is detected` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `CPU: {0}% | GPU: {3}% | RAM: {6}%` | `Default hardware monitoring state` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | — |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Hardware/CPU/Usage** | `int` | `Write` | `CPU load percentage` |
| **VRCOSC/Hardware/CPU/Temp** | `int` | `Write` | `CPU temperature in °C` |
| **VRCOSC/Hardware/CPU/Power** | `float` | `Write` | `CPU power draw in W` |
| **VRCOSC/Hardware/GPU/Usage** | `int` | `Write` | `GPU load percentage` |
| **VRCOSC/Hardware/GPU/Temp** | `int` | `Write` | `GPU temperature in °C` |
| **VRCOSC/Hardware/GPU/Power** | `float` | `Write` | `GPU power draw in W` |
| **VRCOSC/Hardware/RAM/Usage** | `int` | `Write` | `RAM usage percentage` |
| **VRCOSC/Hardware/VRAM/Usage** | `int` | `Write` | `VRAM usage percentage` |
| **VRCOSC/Hardware/Network/RxKiBps** | `float` | `Write` | `Network download speed (KiB/s)` |
| **VRCOSC/Hardware/Network/TxKiBps** | `float` | `Write` | `Network upload speed (KiB/s)` |
| **VRCOSC/Hardware/Window/FPS** | `int` | `Write` | `Active window rendering FPS` |
| **VRCOSC/Hardware/VR/Mode** | `string` | `Write` | `VR Compositor state (Desktop/SteamVR/OpenXR)` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get Linux Hardware Stats** | `Flow trigger` | `CPU Usage (int), GPU Usage (int), RAM Usage (int), VRAM Usage (int)` | `Returns main hardware metrics` |
| **Get Active Window Info** | `Flow trigger` | `Window Title (string), Process Name (string), FPS (int)` | `Returns active desktop window details` |
| **Get Linux VR Mode** | `Flow trigger` | `VR Mode (string), VRChat Running (bool)` | `Returns current VR compositor mode` |
<!-- AUTOGEN:NODES:END -->
