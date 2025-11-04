using System;
using System.Net;
using System.Threading.Tasks;
using Bluscream;

namespace Bluscream.Modules.HTTPServer.Endpoints;

/// <summary>
/// Handles GET / - Server information endpoint
/// </summary>
public static class RootEndpoint
{
    public static async Task Handle(HttpListenerContext context, HTTPServerModule module)
    {
        var responseObj = new
        {
            message = "VRCOSC HTTP Server is running",
            version = AssemblyUtils.GetVersion(),
            documentation = $"{module.GetDisplayUrl()}/docs",
            endpoints = module.GetEndpointsList(),
            timestamp = DateTime.UtcNow.ToString("o"),
            requestCount = module.GetRequestCount()
        };

        module.SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }
}
