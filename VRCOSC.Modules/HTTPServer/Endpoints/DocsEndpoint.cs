using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Bluscream.Modules.HTTPServer.Endpoints;

/// <summary>
/// Handles GET /docs and GET /openapi.json
/// </summary>
public static class DocsEndpoint
{
    public static async Task HandleSwaggerUI(HttpListenerContext context, HTTPServerModule module)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Bluscream.Modules.HTTPServer.swagger-ui.html";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            module.SendJsonResponse(context.Response, 404, new { error = "Swagger UI not found" });
            return;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        context.Response.ContentType = "text/html";
        var buffer = Encoding.UTF8.GetBytes(content);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    public static async Task HandleOpenApiSpec(HttpListenerContext context, HTTPServerModule module)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Bluscream.Modules.HTTPServer.openapi.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            module.SendJsonResponse(context.Response, 404, new { error = "OpenAPI spec not found" });
            return;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        // Update server URL dynamically - convert + wildcard to localhost for browser compatibility
        var serverUrl = module.GetServerUrl().Replace("http://+:", "http://localhost:");
        content = content.Replace("http://localhost:8080", serverUrl);
        
        context.Response.ContentType = "application/json";
        var buffer = Encoding.UTF8.GetBytes(content);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }
}
