using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Bluscream;

namespace Bluscream.Modules.HTTPServer.Endpoints;

/// <summary>
/// Handles GET/POST /api/chatbox/send
/// GET returns current chatbox text (plain text)
/// POST sends a message to chatbox (JSON)
/// </summary>
public static class ChatBoxEndpoint
{
    public static async Task HandleGet(HttpListenerContext context, HTTPServerModule module)
    {
        try
        {
            // Get current chatbox text from ChatBoxManager
            var chatBoxText = ReflectionUtils.GetChatBoxText();
            
            if (chatBoxText == null)
            {
                context.Response.StatusCode = 503;
                context.Response.ContentType = "text/plain";
                var errorMsg = "Unable to get chatbox text - is VRCOSC started?";
                var buffer = System.Text.Encoding.UTF8.GetBytes(errorMsg);
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                context.Response.Close();
                return;
            }

            // Return raw chatbox text as plain text
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain; charset=utf-8";
            var textBuffer = System.Text.Encoding.UTF8.GetBytes(chatBoxText);
            context.Response.ContentLength64 = textBuffer.Length;
            await context.Response.OutputStream.WriteAsync(textBuffer, 0, textBuffer.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            module.SendJsonResponse(context.Response, 500, new { success = false, error = $"Error getting chatbox text: {ex.Message}" });
        }
    }

    public static async Task HandlePost(HttpListenerContext context, HTTPServerModule module)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body);

            var message = data.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "";
            var send = data.RootElement.TryGetProperty("send", out var sendElement) && sendElement.GetBoolean();
            var minimalBackground = data.RootElement.TryGetProperty("minimalBackground", out var bgElement) && bgElement.GetBoolean();

            if (string.IsNullOrEmpty(message))
            {
                module.SendJsonResponse(context.Response, 400, new { success = false, error = "Missing 'message' field in request body" });
                return;
            }

            // Use utilities to send chatbox message
            var success = ReflectionUtils.SendChatBox(message, minimalBackground);

            var responseObj = new
            {
                success = success,
                message = message,
                sent = send,
                implemented = true
            };

            module.SendJsonResponse(context.Response, 200, responseObj);
        }
        catch (JsonException ex)
        {
            module.SendJsonResponse(context.Response, 400, new { success = false, error = "Invalid JSON", message = ex.Message });
        }
        catch (Exception ex)
        {
            module.SendJsonResponse(context.Response, 500, new { success = false, error = $"Error sending chatbox message: {ex.Message}" });
        }
    }
}
