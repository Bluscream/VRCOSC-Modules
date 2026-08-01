using EmbedIO;
using System;
using System.Net;
using System.Threading.Tasks;
using Bluscream;

namespace Bluscream.Modules.HTTPServer.Endpoints;

/// <summary>
/// Handles GET / - Server information endpoint
/// </summary>
internal static class RootEndpoint
{
    internal static async Task Handle(IHttpContext context, HTTPServerModule module)
    {
        var responseObj = new
        {
            message = "VRCOSC HTTP/MCP Server is running",
            version = AssemblyUtils.GetVersion(),
            documentation = $"{module.GetDisplayUrl()}/docs",
            endpoints = module.GetEndpointsList(),
            timestamp = DateTime.UtcNow.ToIso8601(),
            requestCount = module.GetRequestCount()
        };

        module.SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }
}
