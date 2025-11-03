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
            
            // Try to get parameters from Debug module first (better tracking)
            var debugParams = ReflectionUtils.GetDebugModuleParameters();
            List<object> incomingParams = new();
            List<object> outgoingParams = new();
            
            if (debugParams != null)
            {
                // Use Debug module's tracked parameters
                var (incoming, outgoing) = debugParams.Value;
                
                if (incoming != null)
                {
                    incomingParams = incoming.Values
                        .Cast<object>()
                        .ToList();
                }
                
                if (outgoing != null)
                {
                    outgoingParams = outgoing.Values
                        .Cast<object>()
                        .ToList();
                }
            }
            else
            {
                // Fallback to AppManager parameter cache
                var allParams = ReflectionUtils.GetAllOscParameters();
                if (allParams != null)
                {
                    incomingParams = allParams.Select(p => (object)new { 
                        name = p.Name, 
                        value = p.Value, 
                        type = p.Type 
                    }).ToList();
                }
            }

            var responseObj = new
            {
                success = true,
                server = new
                {
                    version = AssemblyUtils.GetVersion(),
                    uptime = module.GetUptime(),
                    requestCount = module.GetRequestCount(),
                    url = module.GetServerUrl(),
                    status = "running"
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
                osc = new
                {
                    incoming = new
                    {
                        count = incomingParams.Count,
                        parameters = incomingParams
                    },
                    outgoing = new
                    {
                        count = outgoingParams.Count,
                        parameters = outgoingParams
                    }
                },
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
