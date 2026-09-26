# Home Assistant Module

Integrate Home Assistant entity states, Jinja templates, avatar parameters, custom HomeAssistantEntityClipVariable, and flow nodes via REST & WebSocket APIs.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- Home Assistant instance URL (e.g. `http://192.168.1.100:8123`).
- Long-Lived Access Token generated from your Home Assistant profile page.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **ServerUrl** | `TextBox` | Home Assistant base URL | `http://homeassistant.local:8123` |
| **AccessToken** | `TextBox` | Long-Lived Access Token | `empty` |
| **OscPrefix** | `TextBox` | OSC parameter prefix for HA entities | `HomeAssistant/` |
| **AllowAnywhereOscPrefix** | `Toggle` | Match OSC prefix anywhere in parameter path (e.g. for VRCFury prefixes) | `true` |
| **EnableWebSocket** | `Toggle` | Enable real-time state change updates via WebSocket API | `true` |
| **LogDebug** | `Toggle` | Log detailed Home Assistant debug messages | `false` |
| **LogOscParams** | `Toggle` | Log incoming/outgoing OSC parameters | `false` |
| **EntityFilter** | `TextBox` | Comma-separated list of entity IDs or domains to track (empty = all) | `empty` |
| **RegisterAllEntityVariables** | `Toggle` | Register every HA entity state as an individual ChatBox variable (HAState.{entity_id}) | `false` |
| **TemplateVariables** | `KeyValuePairList` | Configure custom ChatBox variables mapped to Jinja templates | `empty` |
| **ParameterRedirects** | `List` | Alias any avatar parameter to any entity (see below) | `empty` |
| **RedirectRateLimitMs** | `TextBox` | Minimum time between service calls per redirect row for float/int values | `200` |

## Parameter Redirects

The prefix convention above needs the avatar parameter to be named after the entity
(`HomeAssistant/switch/desk_socket`). Redirects let you keep whatever parameter your avatar
already has and point it at an entity instead, so nothing on the avatar has to be renamed.
Each row of the **Parameter Redirects** list has:

| Field | Meaning |
|---|---|
| **On** | Enable/disable the row without deleting it |
| **Source parameter** | The address VRChat sends, without `/avatar/parameters/` (e.g. `HomeAssistant/fan/desk_socket_fan`). Generator prefixes such as VRCFury's `VF52_..._OSC/` are tolerated: the row also matches when the received address *ends* with the source |
| **Target entity** | The entity id (e.g. `switch.desk_socket`); the domain before the dot picks the service |
| **Conversion** | How the value is translated, see table below |
| **Invert** | Flips bools and levels (`true` becomes off, 0.8 becomes 0.2) |
| **Min / Max** | The float range of the source parameter (default `0`..`1`). Values are mapped from Min..Max onto 0..1 before the entity's own scale is applied; for `StateToFloat` the entity level is mapped back into Min..Max |

Example: the avatar has a bool `HomeAssistant/fan/desk_socket_fan` but the fan hangs off a
smart plug. A row `HomeAssistant/fan/desk_socket_fan` -> `switch.desk_socket`, conversion
`Passthrough` (or `BoolToOnOff`), turns the plug on and off.

Conversions and the service used per target domain:

| Conversion | Value in | light | fan | cover / valve | media_player | number / input_number | select / input_select | climate | humidifier | switch, input_boolean, script, automation, siren, remote, water_heater, camera | lock | vacuum | scene | button / input_button |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| **Passthrough** | any | picks `BoolToOnOff`, `FloatToLevel` or `IntToValue` from the received type | | | | | | | | | | | | |
| **BoolToOnOff** | bool (float >= 0.5, int != 0) | `turn_on` / `turn_off` | `turn_on` / `turn_off` | `open_cover` / `close_cover`, `open_valve` / `close_valve` | `turn_on` / `turn_off` | not applicable | not applicable | `turn_on` / `turn_off` | `turn_on` / `turn_off` | `turn_on` / `turn_off` | `lock` / `unlock` | `start` / `return_to_base` | `turn_on` (false ignored) | `press` (false ignored) |
| **FloatToLevel** | float Min..Max -> 0..1 | `turn_on` `brightness` 0..255 (0 = `turn_off`) | `set_percentage` 0..100 | `set_cover_position` / `set_valve_position` 0..100 | `volume_set` 0..1 | `set_value` scaled into the entity's `min`..`max` attributes | not applicable | `set_temperature` scaled into `min_temp`..`max_temp` | `set_humidity` 0..100 | on/off at 0.5 | on/off at 0.5 | on/off at 0.5 | on at >= 0.5 | press at >= 0.5 |
| **IntToValue** | int (raw) | `brightness` 1..255 (0 = `turn_off`) | `set_percentage` | position 0..100 | `volume_set` int / 100 | `set_value` raw | `select_option` by index into the entity's `options` | `set_temperature` raw | `set_humidity` | on/off at != 0 | lock/unlock at != 0 | start/return at != 0 | on at != 0 | press at != 0 |

The other three conversions go the opposite way and write the entity's state to the source
parameter whenever Home Assistant reports a change for the target (WebSocket must be enabled):

| Conversion | Written to the source parameter |
|---|---|
| **StateToBool** | `true` for `on`, `open`, `locked`, `playing`, `home`, `active`, `cleaning`, `running` and climate modes; Invert flips it |
| **StateToFloat** | The entity's level (brightness/255, percentage/100, position/100, volume, humidity, number and temperature within their min/max) mapped into Min..Max. Entities without a level (sensors) send their numeric state as-is when Min/Max are left at 0..1 |
| **StateToInt** | Brightness, percentage, position, volume in percent, temperature, the select's option index, or the rounded numeric state (`on` = 1, `off` = 0 for non-numeric states) |

Float and int values are rate-limited per row by **Redirect Rate Limit (ms)** (default 200 ms,
the last value in the window wins); bools are sent immediately. A row whose target is not a
valid `domain.object_id` or whose domain has no mapping is logged once and skipped. A matched
redirect takes precedence over the prefix convention for that address.

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | True if connected to Home Assistant REST/WebSocket API |
| **Last Entity** | `lastentity` | `string` | Entity ID of the last updated entity |
| **Last State** | `laststate` | `string` | State string of the last updated entity |
| **States Count** | `statescount` | `int` | Total entities tracked in state cache |
| **Entity State / Attribute** | `entitystate` | `HomeAssistantEntityClipVariable` | Generic clip variable with EntityID, Attribute, RoundDecimals, TitleCase, AppendUnit, FormatString options |
| **HATemplate.<Name>** | `HATemplate.<Name>` | `string` | Custom Jinja template variables configured in module settings |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `HA Disconnected` | Disconnected from Home Assistant |
| **Connecting** | `connecting` | `HA Connecting...` | Connecting to REST/WebSocket API |
| **Connected** | `connected` | `HA Connected ({0})` | Connected and receiving updates |
| **Error** | `error` | `HA Error: {0}` | Connection or authentication error |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On State Changed** | `onstatechanged` | `HA {0} = {1}` | Triggered when any entity state updates |
| **On Service Executed** | `onserviceexecuted` | `HA Service: {0}.{1}` | Triggered when an HA service is executed |
| **On Error** | `onerror` | `HA Error: {0}` | Triggered on API or Jinja template rendering error |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/HomeAssistant/Connected` | `bool` | `Write` | True if Home Assistant is connected |
| `VRCOSC/HomeAssistant/EventReceived` | `bool` | `Write` | Flashes true on state change event |
| `VRCOSC/HomeAssistant/Failed` | `bool` | `Write` | True if connection/auth failed |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Call Home Assistant Service** | Domain (string), Service (string), Service Data (Dict) | Success (bool), Error (string) | Executes an HA service call (e.g. light.turn_on) |
| **Get Entity State** | Entity ID (string) | State (string), Exists (bool) | Returns current state of an HA entity |
| **Get Entity Attribute** | Entity ID (string), Attribute Name (string) | Attribute Value (object), Exists (bool) | Returns specific attribute of an HA entity |
| **Render Jinja Template** | Jinja Template (string) | Rendered Output (string), Error (string) | Renders a Jinja template string on Home Assistant |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **ServerUrl** | `TextBox` | `Home Assistant base URL` | `http://homeassistant.local:8123` |
| **AccessToken** | `TextBox` | `Long-Lived Access Token` | `empty` |
| **OscPrefix** | `TextBox` | `OSC parameter prefix for HA entities` | `HomeAssistant/` |
| **AllowAnywhereOscPrefix** | `Toggle` | `Match OSC prefix anywhere in parameter path (e.g. for VRCFury prefixes)` | `true` |
| **EnableWebSocket** | `Toggle` | `Enable real-time state change updates via WebSocket API` | `true` |
| **LogDebug** | `Toggle` | `Log detailed Home Assistant debug messages` | `false` |
| **LogOscParams** | `Toggle` | `Log incoming/outgoing OSC parameters` | `false` |
| **EntityFilter** | `TextBox` | `Comma-separated list of entity IDs or domains to track (empty = all)` | `empty` |
| **RegisterAllEntityVariables** | `Toggle` | `Register every HA entity state as an individual ChatBox variable (HAState.{entity_id})` | `false` |
| **TemplateVariables** | `KeyValuePairList` | `Configure custom ChatBox variables mapped to Jinja templates` | `empty` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | `True if connected to Home Assistant REST/WebSocket API` |
| **Last Entity** | `lastentity` | `string` | `Entity ID of the last updated entity` |
| **Last State** | `laststate` | `string` | `State string of the last updated entity` |
| **States Count** | `statescount` | `int` | `Total entities tracked in state cache` |
| **Entity State / Attribute** | `entitystate` | `HomeAssistantEntityClipVariable` | `Generic clip variable with EntityID, Attribute, RoundDecimals, TitleCase, AppendUnit, FormatString options` |
| **HATemplate.<Name>** | `HATemplate.<Name>` | `string` | `Custom Jinja template variables configured in module settings` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `HA Disconnected` | `Disconnected from Home Assistant` |
| **Connecting** | `connecting` | `HA Connecting...` | `Connecting to REST/WebSocket API` |
| **Connected** | `connected` | `HA Connected ({0})` | `Connected and receiving updates` |
| **Error** | `error` | `HA Error: {0}` | `Connection or authentication error` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On State Changed** | `onstatechanged` | `HA {0} = {1}` | `Triggered when any entity state updates` |
| **On Service Executed** | `onserviceexecuted` | `HA Service: {0}.{1}` | `Triggered when an HA service is executed` |
| **On Error** | `onerror` | `HA Error: {0}` | `Triggered on API or Jinja template rendering error` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/HomeAssistant/Connected** | `bool` | `Write` | `True if Home Assistant is connected` |
| **VRCOSC/HomeAssistant/EventReceived** | `bool` | `Write` | `Flashes true on state change event` |
| **VRCOSC/HomeAssistant/Failed** | `bool` | `Write` | `True if connection/auth failed` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Call Home Assistant Service** | `Domain (string), Service (string), Service Data (Dict)` | `Success (bool), Error (string)` | `Executes an HA service call (e.g. light.turn_on)` |
| **Get Entity State** | `Entity ID (string)` | `State (string), Exists (bool)` | `Returns current state of an HA entity` |
| **Get Entity Attribute** | `Entity ID (string), Attribute Name (string)` | `Attribute Value (object), Exists (bool)` | `Returns specific attribute of an HA entity` |
| **Render Jinja Template** | `Jinja Template (string)` | `Rendered Output (string), Error (string)` | `Renders a Jinja template string on Home Assistant` |
<!-- AUTOGEN:NODES:END -->

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Server URL** | `TextBox` | `Home Assistant base URL (e.g. http://192.168.1.100:8123)` | `"http://homeassistant.local:8123"` |
| **Access Token** | `TextBox` | `Long-Lived Access Token generated in Home Assistant profile` | `empty` |
| **OSC Prefix** | `TextBox` | `Prefix for Home Assistant avatar parameters (e.g. HomeAssistant/)` | `"HomeAssistant/"` |
| **Match OSC Prefix Anywhere** | `Toggle` | `Allow matching the OSC prefix anywhere in parameter paths to support generator prefixes (e.g. VRCFury's VF52_..._OSC/HomeAssistant/). If disabled, parameters must start with the exact prefix.` | `true` |
| **Enable Realtime WebSocket** | `Toggle` | `Enable real-time state change updates via WebSocket API` | `true` |
| **Log Debug** | `Toggle` | `Log detailed Home Assistant debug messages to console` | `false` |
| **Log OSC Parameters** | `Toggle` | `Log incoming/outgoing Home Assistant OSC parameters to console` | `false` |
| **Entity Filter** | `TextBox` | `Comma-separated list of entity IDs or domains to track (leave empty for all)` | `empty` |
| **Register All Entity Variables** | `Toggle` | `Register every HA entity state as an individual ChatBox variable (HAState.{entity_id}). Disabled by default to prevent cluttering.` | `false` |
| **Custom ChatBox Template Variables** | `KeyValuePairList` | `Configure custom ChatBox variables mapped to Jinja templates or entity states.\nKey: Variable Name (e.g. LivingRoomTemp)\nValue: Jinja Template (e.g. {{ states('sensor.living_room_temp') }}°C)` | `Array.Empty<MutableKeyValuePair>(` |
| **Redirect Rate Limit (ms)** | `TextBox` | `Minimum time between service calls per redirect row for float/int values, so sliders do not flood Home Assistant. Bools are sent immediately.` | `200` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | `ChatBox variable Connected` |
| **Last Entity** | `lastentity` | `string` | `ChatBox variable Last Entity` |
| **Last State** | `laststate` | `string` | `ChatBox variable Last State` |
| **States Count** | `statescount` | `int` | `ChatBox variable States Count` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `HA Disconnected` | `Disconnected state` |
| **Connecting** | `connecting` | `HA Connecting...` | `Connecting state` |
| **Connected** | `connected` | `HA Connected ({0})` | `Connected state` |
| **Error** | `error` | `HA Error` | `Error state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On State Changed** | `onstatechanged` | `HA {0} = {1}` | `Triggered on On State Changed` |
| **On Service Executed** | `onserviceexecuted` | `On Service Executed` | `Triggered on On Service Executed` |
| **On Error** | `onerror` | `On Error` | `Triggered on On Error` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/HomeAssistant/Connected** | `bool` | `Write` | `True when connected to Home Assistant` |
| **VRCOSC/HomeAssistant/EventReceived** | `bool` | `Write` | `True for 1 second when a state change event is received` |
| **VRCOSC/HomeAssistant/Failed** | `bool` | `Write` | `True for 1 second when a request or connection fails` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **H A Call Service** | `Flow trigger` | `Output` | `Node node for H A Call Service` |
| **H A Get State** | `Flow trigger` | `Output` | `Node node for H A Get State` |
| **H A Render Template** | `Flow trigger` | `Output` | `Node node for H A Render Template` |
<!-- NODES_TABLE_END -->
