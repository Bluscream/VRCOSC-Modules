using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Bluscream.Modules;

public static class DesktopNotificationSender
{
    public static bool SendNotification(string title, string message, string? iconPath = null, int timeout = 5000)
    {
        try
        {
            // Use PowerShell to send Windows toast notification
            var escapedTitle = title.Replace("\"", "`\"").Replace("'", "''");
            var escapedMessage = message.Replace("\"", "`\"").Replace("'", "''");
            
            var script = $@"
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

$APP_ID = 'VRCOSC'

$template = @""
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{escapedTitle}</text>
            <text>{escapedMessage}</text>
        </binding>
    </visual>
</toast>
""@

$xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$xml.LoadXml($template)
$toast = New-Object Windows.UI.Notifications.ToastNotification $xml
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($APP_ID).Show($toast)
";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Desktop Notification] Error: {ex.Message}");
            return false;
        }
    }
}
