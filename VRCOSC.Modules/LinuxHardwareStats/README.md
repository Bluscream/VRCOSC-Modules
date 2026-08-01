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

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Selected CPU** | `TextBox` | `Configure Selected CPU` | `"Index (0-based` |
| **Selected GPU** | `TextBox` | `Configure Selected GPU` | `"Index (0-based` |
| **Network Interface** | `TextBox` | `Configure Network Interface` | `"Interface to monitor (e.g. enp6s0, eth0` |
| **Redacted Window Title Pattern** | `TextBox` | `Regex pattern — if the active window title matches, it is replaced with the Redacted Text. Leave empty to disable.` | `""` |
| **Redacted Process Name Pattern** | `TextBox` | `Regex pattern — if the active process name matches, it is replaced with the Redacted Text. Leave empty to disable.` | `""` |
| **Redacted Text** | `TextBox` | `Text shown when a window title or process name matches a redaction pattern.` | `"[REDACTED]"` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **CPU Name** | `cpuname` | `string` | `ChatBox variable CPU Name` |
| **CPU Manufacturer** | `cpumanufacturer` | `string` | `ChatBox variable CPU Manufacturer` |
| **CPU Model** | `cpumodel` | `string` | `ChatBox variable CPU Model` |
| **CPU Usage (%)** | `cpuusage` | `int` | `ChatBox variable CPU Usage (%)` |
| **CPU Power (W)** | `cpupower` | `int` | `ChatBox variable CPU Power (W)` |
| **CPU Temp (C)** | `cputemp` | `int` | `ChatBox variable CPU Temp (C)` |
| **GPU Name** | `gpuname` | `string` | `ChatBox variable GPU Name` |
| **GPU Manufacturer** | `gpumanufacturer` | `string` | `ChatBox variable GPU Manufacturer` |
| **GPU Model** | `gpumodel` | `string` | `ChatBox variable GPU Model` |
| **GPU Usage (%)** | `gpuusage` | `int` | `ChatBox variable GPU Usage (%)` |
| **GPU Power (W)** | `gpupower` | `int` | `ChatBox variable GPU Power (W)` |
| **GPU Temp (C)** | `gputemp` | `int` | `ChatBox variable GPU Temp (C)` |
| **RAM Usage (%)** | `ramusage` | `float` | `ChatBox variable RAM Usage (%)` |
| **RAM Total (GB)** | `ramtotal` | `float` | `ChatBox variable RAM Total (GB)` |
| **RAM Used (GB)** | `ramused` | `float` | `ChatBox variable RAM Used (GB)` |
| **RAM Free (GB)** | `ramfree` | `float` | `ChatBox variable RAM Free (GB)` |
| **VRAM Usage (%)** | `vramusage` | `float` | `ChatBox variable VRAM Usage (%)` |
| **VRAM Total (GB)** | `vramtotal` | `float` | `ChatBox variable VRAM Total (GB)` |
| **VRAM Used (GB)** | `vramused` | `float` | `ChatBox variable VRAM Used (GB)` |
| **VRAM Free (GB)** | `vramfree` | `float` | `ChatBox variable VRAM Free (GB)` |
| **Network Download** | `networkdownload` | `string` | `ChatBox variable Network Download` |
| **Network Upload** | `networkupload` | `string` | `ChatBox variable Network Upload` |
| **Network Received Total** | `networkrxtotal` | `string` | `ChatBox variable Network Received Total` |
| **Network Sent Total** | `networktxtotal` | `string` | `ChatBox variable Network Sent Total` |
| **System Temp (C)** | `systemtemp` | `int` | `ChatBox variable System Temp (C)` |
| **Max Temp (C)** | `maxtemp` | `int` | `ChatBox variable Max Temp (C)` |
| **Active Window Title** | `windowtitle` | `string` | `ChatBox variable Active Window Title` |
| **Active Process Name** | `processname` | `string` | `ChatBox variable Active Process Name` |
| **Active Window FPS** | `windowfps` | `int` | `ChatBox variable Active Window FPS` |
| **VR Mode** | `vrmode` | `string` | `ChatBox variable VR Mode` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Default** | `default` | `CPU: {0}% | GPU: {1}%\nRAM: {2}GB/{3}GB\n↓{4} ↑{5}` | `Default state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| _None_ | — | — | — |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Hardware/CPU/Usage** | `float` | `Write` | `The CPU usage (0-1)` |
| **VRCOSC/Hardware/CPU/Power** | `int` | `Write` | `The CPU power draw (W)` |
| **VRCOSC/Hardware/CPU/Temp** | `int` | `Write` | `The CPU temperature (C)` |
| **VRCOSC/Hardware/GPU/Usage** | `float` | `Write` | `The GPU usage (0-1)` |
| **VRCOSC/Hardware/GPU/Power** | `int` | `Write` | `The GPU power draw (W)` |
| **VRCOSC/Hardware/GPU/Temp** | `int` | `Write` | `The GPU temperature (C)` |
| **VRCOSC/Hardware/RAM/Usage** | `float` | `Write` | `The RAM usage (0-1)` |
| **VRCOSC/Hardware/RAM/Total** | `int` | `Write` | `The total RAM amount (GB)` |
| **VRCOSC/Hardware/RAM/Used** | `int` | `Write` | `The used RAM amount (GB)` |
| **VRCOSC/Hardware/RAM/Free** | `int` | `Write` | `The free RAM amount (GB)` |
| **VRCOSC/Hardware/VRAM/Usage** | `float` | `Write` | `The VRAM usage (0-1)` |
| **VRCOSC/Hardware/VRAM/Total** | `int` | `Write` | `The total VRAM amount (GB)` |
| **VRCOSC/Hardware/VRAM/Used** | `int` | `Write` | `The used VRAM amount (GB)` |
| **VRCOSC/Hardware/VRAM/Free** | `int` | `Write` | `The free VRAM amount (GB)` |
| **VRCOSC/Hardware/Network/Download** | `int` | `Write` | `The network download speed (KB/s)` |
| **VRCOSC/Hardware/Network/Upload** | `int` | `Write` | `The network upload speed (KB/s)` |
| **VRCOSC/Hardware/System/Temp** | `int` | `Write` | `The system (ACPI/motherboard) temperature (C)` |
| **VRCOSC/Hardware/Max/Temp** | `int` | `Write` | `The highest temperature across all sensors (C)` |
| **VRCOSC/ClientInfo/Info/FPS** | `int` | `Write` | `The active window FPS (MangoHud or display refresh rate) — populates the standard ClientInfo FPS path which is always 0 on Linux` |
| **VRCOSC/Hardware/Window/FPS/Normalised** | `float` | `Write` | `Window FPS normalised 0-240 → 0-1, matching the VR FPS scale` |
| **VRCOSC/Hardware/Game/Running** | `bool` | `Write` | `True when the VRChat process is running on the host` |
| **VRCOSC/Hardware/Game/SteamVR** | `bool` | `Write` | `True when SteamVR is the active VR compositor` |
| **VRCOSC/Hardware/Game/OpenXR** | `bool` | `Write` | `True when an OpenXR compositor (Monado, WiVRn) is active` |
| **VRCOSC/Hardware/Game/Desktop** | `bool` | `Write` | `True when no VR compositor is running` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Linux C P U Info Source** | `Flow trigger` | `Output` | `Node node for Linux C P U Info Source` |
| **Linux G P U Info Source** | `Flow trigger` | `Output` | `Node node for Linux G P U Info Source` |
| **Linux R A M Info Source** | `Flow trigger` | `Output` | `Node node for Linux R A M Info Source` |
| **Linux V R A M Info Source** | `Flow trigger` | `Output` | `Node node for Linux V R A M Info Source` |
| **Linux Network Info Source** | `Flow trigger` | `Output` | `Node node for Linux Network Info Source` |
<!-- NODES_TABLE_END -->
