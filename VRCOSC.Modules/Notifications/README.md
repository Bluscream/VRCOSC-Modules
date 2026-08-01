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
