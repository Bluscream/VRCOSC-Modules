# Notifications Module

Send notifications to Windows Desktop toasts, XSOverlay (UDP 42010), OVRToolkit (WebSocket 15000), and Webhooks.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- XSOverlay UDP port 42010 enabled (if using XSOverlay).
- OVRToolkit WebSocket port 15000 enabled (if using OVRToolkit).
- Target Webhook server URL (if using Webhook notifications).

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **EnableDesktop** | `Toggle` | Send Windows desktop toast notifications | `true` |
| **EnableXSOverlay** | `Toggle` | Send XSOverlay notifications via UDP 42010 | `true` |
| **EnableOVRToolkit** | `Toggle` | Send OVRToolkit notifications via WebSocket 15000 | `false` |
| **EnableWebhook** | `Toggle` | Send webhook HTTP notifications | `false` |
| **WebhookUrl** | `TextBox` | Target Webhook endpoint URL | `empty` |
| **WebhookMethod** | `Dropdown` | HTTP method for webhook (POST, GET, PUT) | `POST` |
| **DefaultTitle** | `TextBox` | Default notification title | `VRCOSC` |
| **DefaultMessage** | `TextBox` | Default notification message body | `empty` |
| **DefaultTimeoutMs** | `Slider` | Default notification display duration (ms) | `3000` |
| **DefaultOpacity** | `Slider` | Default notification opacity (0.0 to 1.0) | `1.0` |
| **LogDebug** | `Toggle` | Log notification dispatch details to console | `false` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Last Title** | `lasttitle` | `string` | Title of last sent notification |
| **Last Message** | `lastmessage` | `string` | Message body of last sent notification |
| **Notification Count** | `notificationcount` | `int` | Total notifications dispatched |
| **Last Target** | `lasttarget` | `string` | Target system of last notification (Desktop, XSOverlay, etc.) |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `Notifications Idle` | Module ready |
| **Sending** | `sending` | `Sending: {0}` | Dispatching notification |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Notification Sent** | `onnotificationsent` | `Notification Sent: {0}` | Triggered when notification succeeds |
| **On Notification Failed** | `onnotificationfailed` | `Notification Failed: {0}` | Triggered when notification fails |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/Notifications/Send` | `bool` | `Read` | Set to true to dispatch default notification |
| `VRCOSC/Notifications/SentCount` | `int` | `Write` | Total notifications successfully sent |
| `VRCOSC/Notifications/FailedCount` | `int` | `Write` | Total failed notification attempts |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Send Desktop Notification** | Title (string), Message (string), TimeoutMs (int) | Success (bool) | Sends Windows desktop toast |
| **Send XSOverlay Notification** | Title (string), Message (string), TimeoutMs (int), Opacity (float) | Success (bool) | Sends XSOverlay UDP notification |
| **Send OVRToolkit Notification** | Title (string), Message (string) | Success (bool) | Sends OVRToolkit WebSocket notification |
| **Send Notification (All Enabled)** | Title (string), Message (string), TimeoutMs (int) | WebhookSuccess (bool) | Dispatches to all enabled targets |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **EnableDesktop** | `Toggle` | `Send Windows desktop toast notifications` | `true` |
| **EnableXSOverlay** | `Toggle` | `Send XSOverlay notifications via UDP 42010` | `true` |
| **EnableOVRToolkit** | `Toggle` | `Send OVRToolkit notifications via WebSocket 15000` | `false` |
| **EnableWebhook** | `Toggle` | `Send webhook HTTP notifications` | `false` |
| **WebhookUrl** | `TextBox` | `Target Webhook endpoint URL` | `empty` |
| **WebhookMethod** | `Dropdown` | `HTTP method for webhook (POST, GET, PUT)` | `POST` |
| **DefaultTitle** | `TextBox` | `Default notification title` | `VRCOSC` |
| **DefaultMessage** | `TextBox` | `Default notification message body` | `empty` |
| **DefaultTimeoutMs** | `Slider` | `Default notification display duration (ms)` | `3000` |
| **DefaultOpacity** | `Slider` | `Default notification opacity (0.0 to 1.0)` | `1.0` |
| **LogDebug** | `Toggle` | `Log notification dispatch details to console` | `false` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Last Title** | `lasttitle` | `string` | `Title of last sent notification` |
| **Last Message** | `lastmessage` | `string` | `Message body of last sent notification` |
| **Notification Count** | `notificationcount` | `int` | `Total notifications dispatched` |
| **Last Target** | `lasttarget` | `string` | `Target system of last notification (Desktop, XSOverlay, etc.)` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `Notifications Idle` | `Module ready` |
| **Sending** | `sending` | `Sending: {0}` | `Dispatching notification` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Notification Sent** | `onnotificationsent` | `Notification Sent: {0}` | `Triggered when notification succeeds` |
| **On Notification Failed** | `onnotificationfailed` | `Notification Failed: {0}` | `Triggered when notification fails` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Notifications/Send** | `bool` | `Read` | `Set to true to dispatch default notification` |
| **VRCOSC/Notifications/SentCount** | `int` | `Write` | `Total notifications successfully sent` |
| **VRCOSC/Notifications/FailedCount** | `int` | `Write` | `Total failed notification attempts` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Send Desktop Notification** | `Title (string), Message (string), TimeoutMs (int)` | `Success (bool)` | `Sends Windows desktop toast` |
| **Send XSOverlay Notification** | `Title (string), Message (string), TimeoutMs (int), Opacity (float)` | `Success (bool)` | `Sends XSOverlay UDP notification` |
| **Send OVRToolkit Notification** | `Title (string), Message (string)` | `Success (bool)` | `Sends OVRToolkit WebSocket notification` |
| **Send Notification (All Enabled)** | `Title (string), Message (string), TimeoutMs (int)` | `WebhookSuccess (bool)` | `Dispatches to all enabled targets` |
<!-- AUTOGEN:NODES:END -->

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Title** | `TextBox` | `Default notification title (used if input is empty)` | `"VRCOSC"` |
| **Message** | `TextBox` | `Default notification message (used if input is empty)` | `""` |
| **Timeout (ms)** | `Slider` | `Default notification display duration in milliseconds` | `5000, 1000, 30000, 1000` |
| **Opacity (%)** | `Slider` | `Default notification opacity percentage (0-100)` | `100, 0, 95, 5` |
| **Enable Desktop Notifications** | `Toggle` | `Show Windows desktop notifications` | `true` |
| **Enable XSOverlay Notifications** | `Toggle` | `Send notifications to XSOverlay` | `false` |
| **Enable OVRToolkit Notifications** | `Toggle` | `Send notifications to OVRToolkit` | `false` |
| **Enable Webhook Notifications** | `Toggle` | `Send notifications to webhook URL` | `false` |
| **Webhook URL** | `TextBox` | `HTTP(S) URL to send notifications to` | `empty` |
| **Webhook Method** | `Dropdown` | `HTTP method for webhook requests` | `WebhookMethod.POST` |
| **Log Notifications** | `Toggle` | `Log all notification sends to console` | `false` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Last Title** | `lasttitle` | `string` | `ChatBox variable Last Title` |
| **Last Message** | `lastmessage` | `string` | `ChatBox variable Last Message` |
| **Notification Count** | `notificationcount` | `int` | `ChatBox variable Notification Count` |
| **Last Target** | `lasttarget` | `string` | `ChatBox variable Last Target` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Notification Sent** | `onnotificationsent` | `On Notification Sent` | `Triggered on On Notification Sent` |
| **On Notification Failed** | `onnotificationfailed` | `On Notification Failed` | `Triggered on On Notification Failed` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Notifications/Sent** | `bool` | `Write` | `True for 1 second when notification is sent` |
| **VRCOSC/Notifications/Failed** | `bool` | `Write` | `True for 1 second when notification fails` |
| **VRCOSC/Notifications/Count** | `int` | `Write` | `Total number of notifications sent` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Send Desktop Notification** | `Flow trigger` | `Output` | `Node node for Send Desktop Notification` |
| **Send X S Overlay Notification** | `Flow trigger` | `Output` | `Node node for Send X S Overlay Notification` |
| **Send O V R Toolkit Notification** | `Flow trigger` | `Output` | `Node node for Send O V R Toolkit Notification` |
| **Send Notification All** | `Flow trigger` | `Output` | `Node node for Send Notification All` |
<!-- NODES_TABLE_END -->
