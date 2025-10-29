using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

// Send Desktop Notification Node
[Node("Send Desktop Notification")]
public sealed class SendDesktopNotificationNode : ModuleNode<NotificationsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Title = new("Title");
    public ValueInput<string> Message = new("Message");
    public ValueInput<string> IconPath = new("Icon Path (Optional)");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var title = Title.Read(c);
            var message = Message.Read(c);
            var iconPath = IconPath.Read(c);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                Success.Write(false, c);
                await OnError.Execute(c);
                return;
            }

            if (Module.IsLoggingEnabled())
                Module.Log($"Sending Desktop notification: {title}");

            Module.SetSending();
            var success = DesktopNotificationSender.SendNotification(title, message, iconPath, Module.GetDefaultTimeout());
            Success.Write(success, c);

            Module.UpdateNotificationStatus(title, message, "Desktop", success);

            if (success)
                await Next.Execute(c);
            else
                await OnError.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"Desktop notification error: {ex.Message}");
            Success.Write(false, c);
            await OnError.Execute(c);
        }
    }
}

// Send XSOverlay Notification Node
[Node("Send XSOverlay Notification")]
public sealed class SendXSOverlayNotificationNode : ModuleNode<NotificationsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Title = new("Title");
    public ValueInput<string> Message = new("Message");
    public ValueInput<int> Timeout = new("Timeout (ms)");
    public ValueInput<float> Opacity = new("Opacity");
    public ValueInput<string> IconPath = new("Icon Path (Optional)");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var title = Title.Read(c);
            var message = Message.Read(c);
            var timeout = Timeout.Read(c);
            var opacity = Opacity.Read(c);
            var iconPath = IconPath.Read(c);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                Success.Write(false, c);
                await OnError.Execute(c);
                return;
            }

            // Use defaults if not provided
            if (timeout <= 0)
                timeout = Module.GetDefaultTimeout();
            if (opacity <= 0)
                opacity = Module.GetDefaultOpacity();

            if (Module.IsLoggingEnabled())
                Module.Log($"Sending XSOverlay notification: {title}");

            Module.SetSending();
            var success = XSOverlayNotificationSender.SendNotification(title, message, timeout, opacity, iconPath);
            Success.Write(success, c);

            Module.UpdateNotificationStatus(title, message, "XSOverlay", success);

            if (success)
                await Next.Execute(c);
            else
                await OnError.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"XSOverlay notification error: {ex.Message}");
            Success.Write(false, c);
            await OnError.Execute(c);
        }
    }
}

// Send OVRToolkit Notification Node
[Node("Send OVRToolkit Notification")]
public sealed class SendOVRToolkitNotificationNode : ModuleNode<NotificationsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Title = new("Title");
    public ValueInput<string> Message = new("Message");
    public ValueInput<bool> HudNotification = new("HUD Notification");
    public ValueInput<bool> WristNotification = new("Wrist Notification");
    public ValueInput<string> IconPath = new("Icon Path (Optional)");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var title = Title.Read(c);
            var message = Message.Read(c);
            var hudNotification = HudNotification.Read(c);
            var wristNotification = WristNotification.Read(c);
            var iconPath = IconPath.Read(c);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                Success.Write(false, c);
                await OnError.Execute(c);
                return;
            }

            if (!hudNotification && !wristNotification)
            {
                // Default to HUD if neither is specified
                hudNotification = true;
            }

            if (Module.IsLoggingEnabled())
                Module.Log($"Sending OVRToolkit notification: {title}");

            Module.SetSending();
            var success = await OVRToolkitNotificationSender.SendNotificationAsync(hudNotification, wristNotification, title, message, iconPath);
            Success.Write(success, c);

            Module.UpdateNotificationStatus(title, message, "OVRToolkit", success);

            if (success)
                await Next.Execute(c);
            else
                await OnError.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"OVRToolkit notification error: {ex.Message}");
            Success.Write(false, c);
            await OnError.Execute(c);
        }
    }
}

// Send Notification to All Enabled Targets Node
[Node("Send Notification")]
public sealed class SendNotificationAllNode : ModuleNode<NotificationsModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueInput<string> Title = new("Title");
    public ValueInput<string> Message = new("Message");
    public ValueInput<int> Timeout = new("Timeout (ms)");
    public ValueInput<float> Opacity = new("Opacity");
    public ValueInput<string> IconPath = new("Icon (Optional)");

    public ValueOutput<bool> DesktopSuccess = new("Desktop");
    public ValueOutput<bool> XSOverlaySuccess = new("XSOverlay");
    public ValueOutput<bool> OVRToolkitSuccess = new("OVRToolkit");
    public ValueOutput<int> SuccessCount = new("Count");

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var title = Title.Read(c);
            var message = Message.Read(c);
            var timeout = Timeout.Read(c);
            var opacity = Opacity.Read(c);
            var iconPath = IconPath.Read(c);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                DesktopSuccess.Write(false, c);
                XSOverlaySuccess.Write(false, c);
                OVRToolkitSuccess.Write(false, c);
                SuccessCount.Write(0, c);
                await OnError.Execute(c);
                return;
            }

            // Use defaults if not provided
            if (timeout <= 0)
                timeout = Module.GetDefaultTimeout();
            if (opacity <= 0)
                opacity = Module.GetDefaultOpacity();
            if (string.IsNullOrWhiteSpace(iconPath))
                iconPath = Module.GetDefaultIcon();

            if (Module.IsLoggingEnabled())
                Module.Log($"Sending notification to all enabled targets: {title}");

            Module.SetSending();

            var desktopSuccess = false;
            var xsoSuccess = false;
            var ovrtSuccess = false;
            var successCount = 0;
            var targets = new List<string>();

            // Send to Desktop
            if (Module.IsDesktopEnabled())
            {
                desktopSuccess = DesktopNotificationSender.SendNotification(title, message, iconPath, timeout);
                if (desktopSuccess)
                {
                    successCount++;
                    targets.Add("Desktop");
                }
            }

            // Send to XSOverlay
            if (Module.IsXSOverlayEnabled())
            {
                xsoSuccess = XSOverlayNotificationSender.SendNotification(title, message, timeout, opacity, iconPath);
                if (xsoSuccess)
                {
                    successCount++;
                    targets.Add("XSOverlay");
                }
            }

            // Send to OVRToolkit
            if (Module.IsOVRToolkitEnabled())
            {
                ovrtSuccess = await OVRToolkitNotificationSender.SendNotificationAsync(true, false, title, message, iconPath);
                if (ovrtSuccess)
                {
                    successCount++;
                    targets.Add("OVRToolkit");
                }
            }

            DesktopSuccess.Write(desktopSuccess, c);
            XSOverlaySuccess.Write(xsoSuccess, c);
            OVRToolkitSuccess.Write(ovrtSuccess, c);
            SuccessCount.Write(successCount, c);

            var targetString = targets.Count > 0 ? string.Join(", ", targets) : "None";
            Module.UpdateNotificationStatus(title, message, targetString, successCount > 0);

            if (successCount > 0)
                await Next.Execute(c);
            else
                await OnError.Execute(c);
        }
        catch (Exception ex)
        {
            Module.Log($"Multi-target notification error: {ex.Message}");
            await OnError.Execute(c);
        }
    }
}
