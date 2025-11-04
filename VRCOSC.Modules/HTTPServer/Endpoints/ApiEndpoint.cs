using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bluscream;

namespace Bluscream.Modules.HTTPServer.Endpoints;

/// <summary>
/// Handles GET /api - Comprehensive API data endpoint
/// </summary>
public static class ApiEndpoint
{
    public static async Task Handle(HttpListenerContext context, HTTPServerModule module)
    {
        try
        {
            // Get avatar info
            var avatarInfo = ReflectionUtils.GetCurrentAvatarInfo();
            var (avatarId, avatarName) = avatarInfo ?? (null, null);
            
            // Get chatbox state
            var chatBoxState = ReflectionUtils.GetChatBoxState();
            
            // Get all modules info
            var modulesInfo = ReflectionUtils.GetAllModulesInfo();
            
            // Get all OSC parameters as a dictionary
            var parametersDict = new Dictionary<string, object>();
            
            // Try Debug module first (has both incoming and outgoing)
            var debugParams = ReflectionUtils.GetDebugModuleParameters();
            if (debugParams != null)
            {
                var (incoming, outgoing) = debugParams.Value;
                
                // Merge incoming and outgoing into one dict
                if (incoming != null)
                {
                    foreach (var kvp in incoming)
                    {
                        parametersDict[kvp.Key] = kvp.Value;
                    }
                }
                
                if (outgoing != null)
                {
                    foreach (var kvp in outgoing)
                    {
                        if (!parametersDict.ContainsKey(kvp.Key))
                            parametersDict[kvp.Key] = kvp.Value;
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
                        parametersDict[p.Name] = new { value = p.Value, type = p.Type };
                    }
                }
            }

            var responseObj = new
            {
                server = new
                {
                    version = AssemblyUtils.GetVersion(),
                    uptime = module.GetUptime(),
                    requestCount = module.GetRequestCount(),
                    url = module.GetDisplayUrl()
                },
                player = new
                {
                    avatar = new
                    {
                        id = avatarId,
                        name = avatarName ?? "Unknown",
                        loaded = !string.IsNullOrEmpty(avatarId)
                    }
                },
                chatbox = chatBoxState ?? new
                {
                    currentText = (string?)"",
                    liveText = (string?)"",
                    pulseText = (string?)null,
                    minimalBackground = false,
                    isTyping = false,
                    sendEnabled = false
                },
                modules = modulesInfo ?? new List<object>(),
                parameters = parametersDict,
                endpoints = module.GetEndpointsList(),
                timestamp = DateTime.UtcNow.ToString("o")
            };

            module.SendJsonResponse(context.Response, 200, responseObj);
        }
        catch (Exception ex)
        {
            module.SendJsonResponse(context.Response, 500, new { success = false, error = $"Error getting API data: {ex.Message}" });
        }

        await Task.CompletedTask;
    }
}
