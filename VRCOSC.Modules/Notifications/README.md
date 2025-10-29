# Notifications Module for VRCOSC

Send notifications to Desktop (Windows), XSOverlay, and OVRToolkit from VRCOSC using pulse nodes.

## Features

- 📱 **Desktop Notifications** - Send native Windows toast notifications
- 🥽 **XSOverlay Integration** - Send VR overlay notifications to XSOverlay
- 🛠️ **OVRToolkit Integration** - Send HUD and wrist notifications to OVRToolkit
- 🎯 **Multi-Target Support** - Send to all enabled notification targets at once
- ⚙️ **Configurable Defaults** - Set default timeout, opacity, and icon path
- 📊 **OSC Parameters** - Track notification status via OSC
- 💬 **ChatBox Integration** - Display notification status in VRChat ChatBox
- 🔍 **Debug Logging** - Optional logging of all notification operations

## Installation

1. Copy the compiled `Bluscream.Modules.dll` to your VRCOSC modules folder:
   - `%APPDATA%\VRCOSC\packages\local\`
2. Restart VRCOSC
3. Enable the module in VRCOSC settings

## Module Settings

### Defaults

- **Default Timeout (ms)**: Default notification display duration (1000-30000ms, default: 5000ms)
- **Default Opacity**: Default notification opacity (0.0-1.0, default: 1.0)
- **Default Icon Path**: Path to default icon image (leave empty for built-in)

### Targets

- **Enable Desktop Notifications**: Show Windows desktop notifications (default: true)
- **Enable XSOverlay Notifications**: Send to XSOverlay (default: false)
- **Enable OVRToolkit Notifications**: Send to OVRToolkit (default: false)

### Debug

- **Log Notifications**: Log all notification operations to console (default: false)

## OSC Parameters

| Parameter           | Address                       | Type | Mode  | Description                                 |
| ------------------- | ----------------------------- | ---- | ----- | ------------------------------------------- |
| Notification Sent   | `VRCOSC/Notifications/Sent`   | Bool | Write | True for 1 second when notification is sent |
| Notification Failed | `VRCOSC/Notifications/Failed` | Bool | Write | True for 1 second when notification fails   |
| Notification Count  | `VRCOSC/Notifications/Count`  | Int  | Write | Total number of notifications sent          |

## ChatBox Integration

### Variables

- **Last Title**: Title of the last notification sent
- **Last Message**: Message of the last notification sent
- **Notification Count**: Total notifications sent this session
- **Last Target**: Target(s) of the last notification (Desktop, XSOverlay, OVRToolkit, etc.)

### States

- **Idle**: Module is idle, ready to send notifications
- **Sending**: Currently sending a notification

### Events

- **On Notification Sent**: Triggered when a notification is successfully sent
- **On Notification Failed**: Triggered when a notification fails to send

## Pulse Nodes

### Send Desktop Notification

Sends a Windows desktop toast notification.

**Inputs:**

- `Title` (string) - Notification title
- `Message` (string) - Notification message
- `Icon Path (Optional)` (string) - Path to icon image

**Outputs:**

- `Success` (bool) - Whether the notification was sent successfully

**Flow:**

- `Next` - Triggered on success
- `On Error` - Triggered on failure

---

### Send XSOverlay Notification

Sends a notification to XSOverlay (VR overlay).

**Inputs:**

- `Title` (string) - Notification title
- `Message` (string) - Notification message
- `Timeout (ms)` (int) - Display duration (0 = use default)
- `Opacity` (float) - Opacity 0.0-1.0 (0 = use default)
- `Icon Path (Optional)` (string) - Path to icon image or base64 string

**Outputs:**

- `Success` (bool) - Whether the notification was sent successfully

**Flow:**

- `Next` - Triggered on success
- `On Error` - Triggered on failure

**Note:** XSOverlay must be running and listening on UDP port 42069.

---

### Send OVRToolkit Notification

Sends a notification to OVRToolkit (VR overlay).

**Inputs:**

- `Title` (string) - Notification title
- `Message` (string) - Notification message
- `HUD Notification` (bool) - Show in HMD view (default: true if both false)
- `Wrist Notification` (bool) - Show above wristwatch (default: false)
- `Icon Path (Optional)` (string) - Path to icon image

**Outputs:**

- `Success` (bool) - Whether the notification was sent successfully

**Flow:**

- `Next` - Triggered on success
- `On Error` - Triggered on failure

**Note:** OVRToolkit must be running with WebSocket API enabled on port 11450.

---

### Send Notification (All Enabled)

Sends a notification to all enabled notification targets.

**Inputs:**

- `Title` (string) - Notification title
- `Message` (string) - Notification message
- `Timeout (ms)` (int) - Display duration (0 = use default)
- `Opacity` (float) - Opacity 0.0-1.0 (0 = use default)
- `Icon Path (Optional)` (string) - Path to icon image

**Outputs:**

- `Desktop Success` (bool) - Desktop notification success
- `XSOverlay Success` (bool) - XSOverlay notification success
- `OVRToolkit Success` (bool) - OVRToolkit notification success
- `Success Count` (int) - Number of successful notifications

**Flow:**

- `Next` - Triggered if at least one notification succeeded
- `On Error` - Triggered if all notifications failed

## Usage Examples

### Example 1: Simple Desktop Notification

```
[Trigger] -> [Send Desktop Notification]
  Title: "VRChat Status"
  Message: "Friend joined your world"
  Icon Path: "" (use default)
```

### Example 2: XSOverlay with Custom Settings

```
[Trigger] -> [Send XSOverlay Notification]
  Title: "Warning"
  Message: "Low battery detected"
  Timeout: 10000 (10 seconds)
  Opacity: 0.8
  Icon Path: "C:\path\to\icon.png"
```

### Example 3: OVRToolkit Both HUD and Wrist

```
[Trigger] -> [Send OVRToolkit Notification]
  Title: "Important"
  Message: "Event starting in 5 minutes"
  HUD Notification: true
  Wrist Notification: true
```

### Example 4: Send to All Enabled Targets

```
[Trigger] -> [Send Notification (All Enabled)]
  Title: "System Alert"
  Message: "VRCOSC module started"
  Timeout: 5000
  Opacity: 1.0
  Icon Path: "" (use default)
```

## Requirements

- **Desktop Notifications**: Windows 10/11 with PowerShell
- **XSOverlay**: XSOverlay running with UDP notifications enabled (port 42069)
- **OVRToolkit**: OVRToolkit running with WebSocket API enabled (port 11450)

## Troubleshooting

### Desktop notifications not showing

- Ensure Windows notifications are enabled in Settings > System > Notifications
- Check that PowerShell execution is not blocked
- Enable "Log Notifications" in module settings to see errors

### XSOverlay notifications not appearing

- Verify XSOverlay is running
- Check that port 42069 is not blocked by firewall
- Ensure XSOverlay notification settings are enabled

### OVRToolkit notifications not appearing

- Verify OVRToolkit is running
- Check that the WebSocket API is enabled in OVRToolkit settings
- Ensure port 11450 is not blocked by firewall
- OVRToolkit may need to be restarted after enabling the API

## Icon Paths

- Leave empty to use the built-in VRCOSC icon
- Provide absolute path to PNG/JPG image file
- For XSOverlay, you can also use base64-encoded image strings

## Author

**Bluscream**  
[GitHub Repository](https://github.com/Bluscream/VRCOSC-Modules)

## License

This module is part of the VRCOSC Modules collection by Bluscream.
