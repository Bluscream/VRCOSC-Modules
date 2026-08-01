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
