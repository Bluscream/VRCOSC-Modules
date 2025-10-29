using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Bluscream.Modules;

public static class XSOverlayNotificationSender
{
    private const int XSO_PORT = 42069;

    private struct XSOMessage
    {
        public int messageType { get; set; }
        public string title { get; set; }
        public string content { get; set; }
        public float height { get; set; }
        public string sourceApp { get; set; }
        public float timeout { get; set; }
        public string audioPath { get; set; }
        public bool useBase64Icon { get; set; }
        public string icon { get; set; }
        public float opacity { get; set; }
    }

    public static bool SendNotification(string title, string message, int timeout = 5000, double opacity = 1.0, string? iconPath = null)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var endPoint = new IPEndPoint(IPAddress.Loopback, XSO_PORT);

            var useBase64Icon = string.IsNullOrEmpty(iconPath);
            var icon = useBase64Icon ? NotificationsModule.LOGO_BASE64 : iconPath!;

            // Calculate height based on message length
            float height = 110f;
            if (message.Length > 300)
                height = 250f;
            else if (message.Length > 200)
                height = 200f;
            else if (message.Length > 100)
                height = 150f;

            var msg = new XSOMessage
            {
                messageType = 1,
                title = title,
                content = message,
                height = height,
                sourceApp = "VRCOSC",
                timeout = timeout / 1000f, // Convert ms to seconds
                audioPath = string.Empty,
                useBase64Icon = useBase64Icon,
                icon = icon,
                opacity = (float)opacity
            };

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(msg);
            socket.SendTo(jsonBytes, endPoint);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[XSOverlay Notification] Error: {ex.Message}");
            return false;
        }
    }
}
