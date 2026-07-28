# Linux Hardware Stats

A Linux-native replacement for VRCOSC's official **Hardware Stats** module.
Instead of Windows performance APIs, it runs a lightweight bash script on the host
that reads directly from `/sys`, `procfs`, and standard Linux tools, then writes
the results to a file that the C# module parses every tick.

---

## Requirements

| Tool | Purpose | Install |
|---|---|---|
| `bash` | Script runtime | pre-installed |
| `sensors` / `hwmon` | CPU / GPU / system temps | `lm-sensors` package |
| `xdotool` | Active window title & PID | `xdotool` package |
| `xrandr` | Display refresh rate | `xorg-xrandr` package |
| `ss` | WiVRn session detection | `iproute2` (pre-installed) |
| `pgrep` | VR compositor detection | `procps` (pre-installed) |
| `nvidia-smi` | NVIDIA GPU stats | `nvidia-utils` package |

Optional for real game FPS (instead of display refresh rate):

| Tool | Config needed |
|---|---|
| **MangoHud** | Set `output_folder = ~/.cache/MangoHud` in `MangoHud.conf` |

---

## Settings

| Setting | Description |
|---|---|
| **Selected CPU** | 0-based index of the CPU package (most systems: `0`). *Requires restart.* |
| **Selected GPU** | 0-based index of the GPU (useful for iGPU + dGPU setups). *Requires restart.* |
| **Network Interface** | Interface to monitor (e.g. `enp6s0`). Leave empty to combine all non-loopback. *Requires restart.* |
| **Redacted Window Title Pattern** | Regex — if the active window title matches, it shows **Redacted Text** instead. |
| **Redacted Process Name Pattern** | Regex — same for active process name. |
| **Redacted Text** | Replacement text when a redaction pattern matches (default: `[REDACTED]`). |

> Changing CPU, GPU, or Network Interface settings requires a module restart to apply (values are baked into the deployed script at startup).

---

## OSC Parameters

### CPU

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/CPU/Usage` | `float` | CPU usage (0–1) |
| `VRCOSC/Hardware/CPU/Power` | `int` | CPU power draw (W) |
| `VRCOSC/Hardware/CPU/Temp` | `int` | CPU temperature (°C) |

### GPU

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/GPU/Usage` | `float` | GPU usage (0–1) |
| `VRCOSC/Hardware/GPU/Power` | `int` | GPU power draw (W) |
| `VRCOSC/Hardware/GPU/Temp` | `int` | GPU junction temperature (°C) |

### RAM

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/RAM/Usage` | `float` | RAM usage (0–1) |
| `VRCOSC/Hardware/RAM/Total` | `int` | Total RAM (GB) |
| `VRCOSC/Hardware/RAM/Used` | `int` | Used RAM (GB) |
| `VRCOSC/Hardware/RAM/Free` | `int` | Free/available RAM (GB) |

### VRAM

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/VRAM/Usage` | `float` | VRAM usage (0–1) |
| `VRCOSC/Hardware/VRAM/Total` | `int` | Total VRAM (GB) |
| `VRCOSC/Hardware/VRAM/Used` | `int` | Used VRAM (GB) |
| `VRCOSC/Hardware/VRAM/Free` | `int` | Free VRAM (GB) |

### Network

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/Network/Download` | `int` | Download speed (KB/s) |
| `VRCOSC/Hardware/Network/Upload` | `int` | Upload speed (KB/s) |

### Temperature

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/System/Temp` | `int` | ACPI / motherboard temp (°C) |
| `VRCOSC/Hardware/Max/Temp` | `int` | Highest reading across all hwmon sensors (°C) |

### Active Window

| Path | Type | Value |
|---|---|---|
| `VRCOSC/ClientInfo/Info/FPS` | `int` | Active window FPS — MangoHud log if fresh, otherwise display refresh rate of the window's monitor |
| `VRCOSC/VR/FPS/Value` | `int` | Same FPS value — populates the standard VR FPS path |
| `VRCOSC/VR/FPS/Normalised` | `float` | FPS normalised 0–240 → 0–1 (matches `SteamVRStatisticsModule`'s scale) |

> **Why these paths?** On Linux:
> - `ClientInfo` always writes `0` to `VRCOSC/ClientInfo/Info/FPS` (VRChat doesn't send FPS over OSC on Linux).
> - `SteamVRStatisticsModule` always writes `0` to `VRCOSC/VR/FPS/*` on Monado/WiVRn (those paths use the native OpenVR API which is SteamVR-only).
>
> This module overwrites all three with a real value (MangoHud FPS or display refresh rate).
>
> ⚠️ If you run `ClientInfo` or `SteamVRStatisticsModule` simultaneously, they will race-write `0` to these paths every tick. Disable those modules on Linux, or accept occasional `0` flicker.

### Game / VR State

| Path | Type | Value |
|---|---|---|
| `VRCOSC/Hardware/Game/Running` | `bool` | `true` when `VRChat.exe` is running via Wine/Proton |
| `VRCOSC/Hardware/Game/SteamVR` | `bool` | `true` when `vrserver` (SteamVR) is running |
| `VRCOSC/Hardware/Game/OpenXR` | `bool` | `true` when Monado or WiVRn has an active session |
| `VRCOSC/Hardware/Game/Desktop` | `bool` | `true` when no VR compositor is active |

> WiVRn is only counted as active when its `comp_ipc` socket has an established client
> connection — i.e. a headset is streaming, not just when the daemon is idling.

---

## ChatBox Variables

All parameters are also available as ChatBox variables, plus additional string/float variants:

**CPU:** Name, Manufacturer, Model, Usage (%), Power (W), Temp (°C)  
**GPU:** Name, Manufacturer, Model, Usage (%), Power (W), Temp (°C)  
**RAM/VRAM:** Usage (%), Total (GB), Used (GB), Free (GB)  
**Network:** Download (KB/s), Upload (KB/s), Rx Total (MB), Tx Total (MB)  
**Temp:** System Temp (°C), Max Temp (°C)  
**Window:** Active Window Title, Active Process Name, Active Window FPS  
**VR:** VR Mode (`Desktop` / `SteamVR` / `OpenXR`)

---

## ⚠️ Parameter Path Conflict Warning

The following paths are **identical** to those used by the official VRCOSC
**Hardware Stats** module (Windows):

```
VRCOSC/Hardware/CPU/Usage    VRCOSC/Hardware/GPU/Usage
VRCOSC/Hardware/CPU/Power    VRCOSC/Hardware/GPU/Power
VRCOSC/Hardware/CPU/Temp     VRCOSC/Hardware/GPU/Temp
VRCOSC/Hardware/RAM/Usage    VRCOSC/Hardware/VRAM/Usage
VRCOSC/Hardware/RAM/Total    VRCOSC/Hardware/VRAM/Total
VRCOSC/Hardware/RAM/Used     VRCOSC/Hardware/VRAM/Used
VRCOSC/Hardware/RAM/Free     VRCOSC/Hardware/VRAM/Free
```

This is **intentional** — Linux Hardware Stats is designed as a drop-in replacement
for the official module on Linux hosts. Avatar parameters set up for the official
module will work without modification.

> **Do not run both modules simultaneously.** VRCOSC does not detect duplicate OSC
> paths across modules. Both will silently write to the same avatar parameter every
> tick and the last-writer-wins, producing unpredictable flickering values.

The following paths are **unique to this module** and have no conflicts:

```
VRCOSC/Hardware/Network/*    VRCOSC/Hardware/System/Temp
VRCOSC/Hardware/Max/Temp     VRCOSC/Hardware/Window/FPS
VRCOSC/Hardware/Game/*
```

---

## Active Window FPS — Priority Chain

1. **MangoHud CSV log** — looks for `~/.cache/MangoHud/<process>*.csv` (or `~/MangoHud/`, `/tmp/MangoHud/`) updated within the last 30 seconds. Reads the last line's FPS column.
   - Requires MangoHud configured with `output_folder` in `MangoHud.conf`.
2. **Window's monitor refresh rate** — gets the active window's bounding box via `xdotool`, computes its center, matches against each xrandr monitor rectangle, returns that monitor's current refresh rate.
3. **Primary display fallback** — first `*`-marked rate from `xrandr` if window geometry is unavailable.

---

## VR Mode Detection

| Mode | Trigger |
|---|---|
| `SteamVR` | `vrserver` process is running |
| `OpenXR` | `monado-service` / `monado` process running **or** `wivrn-server` with an established client on `comp_ipc` |
| `Desktop` | None of the above |
