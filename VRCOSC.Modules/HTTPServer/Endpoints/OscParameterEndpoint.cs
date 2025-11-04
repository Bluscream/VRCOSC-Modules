using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Bluscream;

namespace Bluscream.Modules.HTTPServer.Endpoints;

/// <summary>
/// Handles OSC parameter operations
/// GET /api/osc/parameter - Get all parameters as dict
/// GET/POST /api/osc/parameter/{name} - Get/Set specific parameter
/// </summary>
public static class OscParameterEndpoint
{
    public static async Task HandleGetAll(HttpListenerContext context, HTTPServerModule module)
    {
        try
        {
            // Get all parameters and convert to simple dict[path] = value
            var parametersDict = new Dictionary<string, object>();
            
            // Try Debug module first
            var debugParams = ReflectionUtils.GetDebugModuleParameters();
            if (debugParams != null)
            {
                var (incoming, outgoing) = debugParams.Value;
                
                // Merge incoming and outgoing
                if (incoming != null)
                {
                    foreach (var kvp in incoming)
                    {
                        // Extract just the value from the ParameterData
                        var paramData = kvp.Value;
                        var valueProperty = paramData.GetType().GetProperty("Value");
                        parametersDict[kvp.Key] = valueProperty?.GetValue(paramData) ?? null!;
                    }
                }
                
                if (outgoing != null)
                {
                    foreach (var kvp in outgoing)
                    {
                        if (!parametersDict.ContainsKey(kvp.Key))
                        {
                            var paramData = kvp.Value;
                            var valueProperty = paramData.GetType().GetProperty("Value");
                            parametersDict[kvp.Key] = valueProperty?.GetValue(paramData) ?? null!;
                        }
                    }
                }
            }
            else
            {
                // Fallback to AppManager parameter cache
                var allParams = ReflectionUtils.GetAllOscParameters();
                if (allParams != null)
                {
                    foreach (var p in allParams)
                    {
                        parametersDict[p.Name] = p.Value ?? null!;
                    }
                }
            }

            module.SendJsonResponse(context.Response, 200, parametersDict);
        }
        catch (Exception ex)
        {
            module.SendJsonResponse(context.Response, 500, new { error = $"Error getting parameters: {ex.Message}" });
        }

        await Task.CompletedTask;
    }

    public static async Task HandleGet(HttpListenerContext context, HTTPServerModule module, string parameterName)
    {
        try
        {
            var parameter = ReflectionUtils.GetOscParameter(parameterName);
            
            if (parameter == null)
            {
                module.SendJsonResponse(context.Response, 404, new
                {
                    success = false,
                    error = $"Parameter '{parameterName}' not found",
                    hint = "Make sure the parameter has been received at least once, or check /api for available parameters"
                });
                return;
            }

            // Return raw value as plain text
            var value = parameter.Value.Value;
            var valueStr = value?.ToString() ?? "null";
            
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            var buffer = System.Text.Encoding.UTF8.GetBytes(valueStr);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            module.SendJsonResponse(context.Response, 500, new { success = false, error = $"Error getting parameter: {ex.Message}" });
        }
    }

    public static async Task HandleSet(HttpListenerContext context, HTTPServerModule module, string parameterName)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body);

            if (!data.RootElement.TryGetProperty("value", out var valueElement))
            {
                module.SendJsonResponse(context.Response, 400, new { success = false, error = "Missing 'value' field in request body" });
                return;
            }

            // Parse value based on JSON type
            object? value = valueElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => valueElement.TryGetInt32(out var intVal) ? intVal : valueElement.GetSingle(),
                JsonValueKind.String => valueElement.GetString(),
                _ => null
            };

            if (value == null)
            {
                module.SendJsonResponse(context.Response, 400, new { success = false, error = "Invalid or unsupported value type" });
                return;
            }

            // Send the parameter
            var (success, error) = ReflectionUtils.SendOscParameter(parameterName, value);

            if (!success)
            {
                module.SendJsonResponse(context.Response, 503, new 
                { 
                    success = false, 
                    error = error ?? "Failed to send parameter"
                });
                return;
            }

            var responseObj = new
            {
                success = true,
                parameter = parameterName,
                value = value,
                type = value.GetType().Name.ToLowerInvariant()
            };

            module.SendJsonResponse(context.Response, 200, responseObj);
        }
        catch (JsonException ex)
        {
            module.SendJsonResponse(context.Response, 400, new { success = false, error = "Invalid JSON", message = ex.Message });
        }
        catch (Exception ex)
        {
            module.SendJsonResponse(context.Response, 500, new { success = false, error = $"Error setting parameter: {ex.Message}" });
        }
    }
}
