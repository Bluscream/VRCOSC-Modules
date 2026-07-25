# HomeAssistant Module for VRCOSC

Integrate **Home Assistant** directly into **VRChat** through VRCOSC! Control smart home devices (lights, switches, covers, media players, locks, fans, scripts, scenes, and automations) directly using VRChat Avatar OSC parameters, render real-time Jinja templates in your ChatBox, and build custom automation flows with VRCOSC Flow Nodes.

---

## 📑 Table of Contents
1. [Features](#-features)
2. [Prerequisites & Setup](#-prerequisites--setup)
3. [Module Settings](#-module-settings)
4. [Unity Avatar Parameter Setup (Controlling HA from VRChat)](#-unity-avatar-parameter-setup-controlling-ha-from-vrchat)
    - [Parameter Naming Conventions](#1-parameter-naming-conventions)
    - [Supported Domains & Types](#2-supported-domains--types)
    - [Unity Setup Step-by-Step](#3-unity-setup-step-by-step)
5. [ChatBox Integration & Jinja Templates](#-chatbox-integration--jinja-templates)
6. [VRCOSC Flow Nodes](#-vrcosc-flow-nodes)
7. [Module Events, Variables & System Parameters](#-module-events-variables--system-parameters)

---

## ✨ Features

- **Bi-Directional Synchronization**: State changes in Home Assistant instantly update avatar parameters over WebSocket. Changes made to avatar parameters in VRChat trigger Home Assistant services.
- **Support for All Standard HA Domains**: `light`, `switch`, `cover`, `climate`, `media_player`, `lock`, `fan`, `scene`, `script`, `automation`, `button`, `input_boolean`, `input_number`, `select`, `input_select`, `number`, `sensor`, `binary_sensor`.
- **Custom Jinja Template ChatBox Variables**: Live-evaluated Jinja templates push formatted text into VRCOSC ChatBox clips (e.g. room temperatures, media state, or battery levels).
- **Flow Nodes**: Includes visual logic nodes (`Call Service`, `Get Entity State`, `Render Template`) for custom node graphs.
- **Configurable Entity Filtering**: Limit synchronization to specific entity IDs or whole domains to keep OSC traffic minimal.

---

## ⚙️ Prerequisites & Setup

1. **Long-Lived Access Token**:
   - In Home Assistant, click on your Profile (bottom left).
   - Scroll down to **Long-Lived Access Tokens** and click **Create Token**.
   - Copy the token.

2. **VRCOSC Module Settings**:
   - **Server URL**: Your Home Assistant base URL (e.g. `http://homeassistant.local:8123` or `http://192.168.1.100:8123`).
   - **Access Token**: Paste your Long-Lived Access Token.
   - **OSC Prefix**: Default is `HomeAssistant/`. (Must match what you put on your avatar parameters).
   - **Enable Realtime WebSocket**: `True` (recommended for instant updates).

---

## ⚙️ Module Settings

| Setting | Type | Default | Description |
|---|---|---|---|
| **Server URL** | Text | `http://homeassistant.local:8123` | Base HTTP/WS address of your HA instance |
| **Access Token** | Text | `""` | Long-Lived Access Token generated in HA profile |
| **OSC Prefix** | Text | `HomeAssistant/` | Prefix added to avatar parameters (e.g. `HomeAssistant/`) |
| **Enable Realtime WebSocket** | Toggle | `true` | Real-time bi-directional streaming via HA WebSocket API |
| **Log Debug** | Toggle | `false` | Enables verbose diagnostic console logging |
| **Log OSC Parameters** | Toggle | `false` | Logs all incoming & outgoing OSC messages |
| **Entity Filter** | Text | `""` | Comma-separated entity IDs or domains to sync (e.g. `light.living_room, switch`) |
| **Register All Entity Variables**| Toggle | `false` | Exposes every entity state as a ChatBox variable (`HAState.{entity_id}`) |
| **Custom ChatBox Template Variables** | Key-Value List | `[]` | Mapped custom Jinja templates to ChatBox variables |

---

## 🎮 Unity Avatar Parameter Setup (Controlling HA from VRChat)

You can send OSC parameters from VRChat to trigger Home Assistant services, and receive HA updates to drive avatar animations/toggles!

### 1. Parameter Naming Conventions

VRCOSC maps VRChat OSC parameters to Home Assistant entities based on the **OSC Prefix** (default: `HomeAssistant/`).

- Replace dots (`.`) in Entity IDs with underscores (`_`).
- Format: `{OSC_Prefix}{domain}_{object_id}` or `{OSC_Prefix}{domain}/{object_id}`.

#### Examples:
- Entity ID: `light.desk_lamp` $\rightarrow$ Parameter: `HomeAssistant/light_desk_lamp`
- Entity ID: `switch.pc_power` $\rightarrow$ Parameter: `HomeAssistant/switch_pc_power`
- Entity ID: `cover.garage_door` $\rightarrow$ Parameter: `HomeAssistant/cover_garage_door`
- Sub-attributes (e.g. Brightness): `HomeAssistant/light_desk_lamp/brightness`

---

### 2. Supported Domains & Types

#### **Toggle / On-Off Controls (`bool`)**
Assign a `bool` parameter on your avatar expressions menu to turn entities on/off or activate actions:
- **`light` / `switch` / `fan` / `input_boolean`**: `true` = `turn_on`, `false` = `turn_off`.
- **`lock`**: `true` = `lock`, `false` = `unlock`.
- **`cover`**: `true` = `open_cover`, `false` = `close_cover`.
- **`scene` / `script` / `automation` / `button`**: Sending `true` triggers/executes the action.

#### **Analog Controls (`float` or `int`)**
Assign a `float` radial puppet or slider parameter:
- **`light` (`float` 0.0 to 1.0)**: Setting `0.0` turns the light off; values `> 0.0` scale brightness from `1` to `255`.
- **`light/brightness` (`float` 0.0 to 1.0 or `int` 0 to 255)**: Direct brightness parameter control.
- **`cover` (`float` 0.0 to 1.0)**: Controls position from 0% (closed) to 100% (open).
- **`media_player` (`float` 0.0 to 1.0)**: Sets volume level from 0% to 100%.

---

### 3. Unity Setup Step-by-Step

1. Open your Avatar Project in Unity.
2. Select your avatar's **Expression Parameters** asset.
3. Add a new parameter:
   - **Name**: `HomeAssistant/light_desk_lamp` (matching your HA entity `light.desk_lamp`)
   - **Type**: `Bool`
   - **Saved**: Optional (enables saving state across world changes)
   - **Synced**: `True`
4. Open your **VRC Expresssions Menu** asset.
5. Add a Control:
   - **Name**: Desk Lamp
   - **Type**: `Toggle`
   - **Parameter**: `HomeAssistant/light_desk_lamp`
6. Upload your avatar! When you toggle this button in VRChat, VRCOSC sends the command to Home Assistant. Likewise, when you turn the lamp on/off in Home Assistant, the menu toggle updates live in VRCOSC/VRChat.

---

## 💬 ChatBox Integration & Jinja Templates

You can display Home Assistant states and custom formatted messages in your VRChat ChatBox.

### Using Built-in Dynamic Variables
Enable **Register All Entity Variables** in settings, or use dynamic variables in your ChatBox clips:
- `{HAState.light.desk_lamp}`
- `{HAState.sensor.bedroom_temperature}`

### Custom Jinja Template Variables
In the VRCOSC Module Settings under **Custom ChatBox Template Variables**, click **Add Item**:
- **Key (Variable Name)**: `LivingRoomTemp`
- **Value (Jinja Template)**: `{{ states('sensor.living_room_temperature') }}°C`

You can then reference `{HATemplate.LivingRoomTemp}` in any ChatBox clip!

#### Advanced Jinja Template Examples:
- **Now Playing**:
  `🎵 {{ state_attr('media_player.spotify', 'media_title') }} - {{ state_attr('media_player.spotify', 'media_artist') }}`
- **System Status**:
  `🏠 House Temp: {{ states('sensor.house_temp') }}°C | CPU: {{ states('sensor.processor_use') }}%`

---

## 🧩 VRCOSC Flow Nodes

The module adds custom visual programming nodes under the **HomeAssistant** category:

1. **Call Service**:
   - Executable node to trigger any service in Home Assistant with optional JSON or Map payload data.
2. **Get Entity State**:
   - Fetches current state, boolean state, and attribute dictionary/JSON for any entity ID.
3. **Render Template**:
   - Evaluates arbitrary Jinja template expressions on demand.

---

## 📊 Module Events, Variables & System Parameters

### System Parameters (VRCOSC -> VRChat Avatar)
- `VRCOSC/HomeAssistant/Connected` (`bool`): `True` when VRCOSC is actively connected to Home Assistant.
- `VRCOSC/HomeAssistant/EventReceived` (`bool`): Pulsed `True` for 1s when an event is received.
- `VRCOSC/HomeAssistant/Failed` (`bool`): Pulsed `True` for 1s when an error occurs.

### Internal Variables
- `Connected` (`bool`)
- `LastEntity` (`string`)
- `LastState` (`string`)
- `StatesCount` (`int`)

### Module Events
- `OnStateChanged`: Triggered whenever any tracked HA entity changes state.
- `OnServiceExecuted`: Triggered when a service call succeeds.
- `OnError`: Triggered when an error occurs.
