using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bluscream;

namespace Bluscream.Modules;

public class OVRToolkitNotificationSender
{
    private static readonly Uri OVRT_WEBSOCKET_URI = new("ws://127.0.0.1:11450/api");
    private static ClientWebSocket? _websocket;
    private static readonly SemaphoreSlim _connectionLock = new(1, 1);

    private struct OvrtMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("messageType")]
        public string MessageType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("json")]
        public string Json { get; set; }
    }

    private struct OvrtHudNotificationMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("icon")]
        public byte[] Icon { get; set; }
    }

    private struct OvrtWristNotificationMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; }
    }

    private static async Task<bool> EnsureConnectedAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_websocket?.State == WebSocketState.Open)
                return true;

            _websocket?.Dispose();
            _websocket = new ClientWebSocket();
            _websocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);

            await _websocket.ConnectAsync(OVRT_WEBSOCKET_URI, CancellationToken.None);
            return _websocket.State == WebSocketState.Open;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OVRToolkit Notification] Connection error: {ex.Message}");
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public static async Task<bool> SendNotificationAsync(bool hudNotification, bool wristNotification, string title, string message)
    {
        try
        {
            if (!await EnsureConnectedAsync())
                return false;

            // Send wrist notification
            if (wristNotification)
            {
                var body = message;
                if (!string.IsNullOrWhiteSpace(title))
                    body = $"{title} - {message}";
                var wristMsg = new OvrtMessage
                {
                    MessageType = "SendWristNotification",
                    Json = JsonSerializer.Serialize(new OvrtWristNotificationMessage
                    {
                        Body = body
                    })
                };

                var wristJson = JsonSerializer.Serialize(wristMsg);
                var wristBytes = Encoding.UTF8.GetBytes(wristJson);
                await _websocket!.SendAsync(new ArraySegment<byte>(wristBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            // Send HUD notification
            if (hudNotification)
            {
                var hudMsg = new OvrtMessage
                {
                    MessageType = "SendNotification",
                    Json = JsonSerializer.Serialize(new OvrtHudNotificationMessage
                    {
                        Title = title,
                        Body = message,
                        Icon = NotificationsModule.LOGO_BYTES
                    })
                };

                var hudJson = JsonSerializer.Serialize(hudMsg);
                var hudBytes = Encoding.UTF8.GetBytes(hudJson);
                await _websocket!.SendAsync(new ArraySegment<byte>(hudBytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OVRToolkit Notification] Error: {ex.Message}");
            return false;
        }
    }
}
