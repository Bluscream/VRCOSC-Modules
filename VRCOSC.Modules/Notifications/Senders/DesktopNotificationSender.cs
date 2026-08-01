using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Bluscream;

namespace Bluscream.Modules;

public static class DesktopNotificationSender
{
    public static bool SendNotification(string title, string message, int timeout = 5000)
    {
        // On Linux, go through the host's notify-send. The PowerShell toast path below
        // does work under Wine, but the toast is raised *inside the prefix* and never
        // reaches the real desktop, so the user sees nothing.
        if (Utilities.LinuxUtils.IsLinux)
        {
            var failed = false;
            Utilities.LinuxUtils.NotifySend(title, message,
                onError: ex =>
                {
                    failed = true;
                    Console.WriteLine($"[Desktop Notification] notify-send failed: {ex.Message}");
                });
            return !failed;
        }

        try
        {
            // Use PowerShell with BurntToast or fallback to simpler notification
            var escapedTitle = title.Replace("\"", "\"\"").Replace("'", "''");
            var escapedMessage = message.Replace("\"", "\"\"").Replace("'", "''");
            
            // Simpler PowerShell script using Windows.UI.Notifications
            var script = $@"
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null

$APP_ID = 'VRCOSC Notifications'
$template = @'
<toast>
    <visual>
        <binding template='ToastGeneric'>
            <text>{escapedTitle}</text>
            <text>{escapedMessage}</text>
        </binding>
    </visual>
</toast>
'@

$xml = [Windows.Data.Xml.Dom.XmlDocument]::new()
$xml.LoadXml($template)
$toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($APP_ID).Show($toast)
";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine($"[Desktop Notification] Failed to start PowerShell process");
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            
            process.WaitForExit(5000);
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"[Desktop Notification] PowerShell error (exit code {process.ExitCode}): {error}");
                return false;
            }
            
            if (!error.IsNullOrEmpty())
            {
                Console.WriteLine($"[Desktop Notification] PowerShell stderr: {error}");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Desktop Notification] Exception: {ex.Message}");
            Console.WriteLine($"[Desktop Notification] Stack trace: {ex.StackTrace}");
            return false;
        }
    }
}
