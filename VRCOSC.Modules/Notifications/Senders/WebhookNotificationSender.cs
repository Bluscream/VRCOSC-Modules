using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bluscream.Modules;

public static class WebhookNotificationSender
{
    private static readonly HttpClient _httpClient = new();

    public static async Task<bool> SendNotificationAsync(string url, string method, string title, string message, int timeout = 5000)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Console.WriteLine("[Webhook Notification] No URL configured");
                return false;
            }

            // Build query params for both GET and POST
            var queryParams = new List<string>
            {
                $"title={Uri.EscapeDataString(title)}",
                $"message={Uri.EscapeDataString(message)}",
                $"timeout={timeout}",
                $"timestamp={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                $"icon={Uri.EscapeDataString(NotificationsModule.LOGO_BASE64)}"
            };

            var queryString = string.Join("&", queryParams);
            var fullUrl = url.Contains("?") ? $"{url}&{queryString}" : $"{url}?{queryString}";

            HttpResponseMessage response;

            if (method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                // POST with JSON body AND query params
                var payload = new Dictionary<string, object>
                {
                    ["title"] = title,
                    ["message"] = message,
                    ["timeout"] = timeout,
                    ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    ["icon"] = NotificationsModule.LOGO_BASE64
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _httpClient.PostAsync(fullUrl, content);
            }
            else if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                response = await _httpClient.GetAsync(fullUrl);
            }
            else
            {
                Console.WriteLine($"[Webhook Notification] Unsupported method: {method}");
                return false;
            }
            
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Webhook Notification] {method} failed with status {response.StatusCode}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Webhook Notification] Error: {ex.Message}");
            return false;
        }
    }
}
