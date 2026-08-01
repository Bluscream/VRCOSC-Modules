// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ModelContextProtocol;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Bluscream.Modules.HTTPServer.Mcp;

/// <summary>
/// Serves the Model Context Protocol over streamable HTTP at a fixed path.
///
/// The protocol itself is handled by ModelContextProtocol.Core — this module only
/// bridges EmbedIO's request/response streams to the SDK's transport. We deliberately
/// avoid ModelContextProtocol.AspNetCore: it carries a FrameworkReference on
/// Microsoft.AspNetCore.App, which is not installed in the Wine dotnet runtime.
///
/// Runs stateless (a fresh transport + server per request), which keeps the module
/// free of session bookkeeping. That is sufficient for tools/list and tools/call;
/// server-initiated notifications would need a session-scoped GET stream.
/// </summary>
internal sealed class McpWebModule : WebModuleBase
{
    private readonly McpServerOptions _options;

    public McpWebModule(string baseRoute) : base(baseRoute)
    {
        _options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "vrcosc",
                Version = typeof(McpWebModule).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            },
            ServerInstructions = "Read and control this running VRCOSC instance. Changes apply live.",
        };

        _options.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var tool in DiscoverTools())
            _options.ToolCollection.Add(tool);
    }

    /// <summary>Build tools from the [McpServerTool] methods on <see cref="McpTools"/>.</summary>
    private static IEnumerable<McpServerTool> DiscoverTools() =>
        ReflectionUtils.GetMethodsWithAttribute<McpServerToolAttribute>(
                typeof(McpTools), BindingFlags.Public | BindingFlags.Static)
            // Explicit lambda, not a method group: McpServerTool.Create is overloaded and
            // the group conversion is ambiguous (CS0411).
            .Select(m => McpServerTool.Create(m));

    public override bool IsFinalHandler => true;

    protected override async Task OnRequestAsync(IHttpContext context)
    {
        if (context.Request.HttpVerb != HttpVerbs.Post)
        {
            context.Response.StatusCode = 405;
            context.Response.ContentType = "application/json";
            await using var w = new StreamWriter(context.Response.OutputStream);
            await w.WriteAsync("{\"error\":\"Method not allowed\",\"hint\":\"MCP uses JSON-RPC over POST\"}");
            return;
        }

        context.Response.ContentType = "application/json";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        JsonRpcMessage? message;
        try
        {
            message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(
                context.Request.InputStream, McpJsonUtilities.DefaultOptions, cts.Token);
        }
        catch (JsonException ex)
        {
            context.Response.StatusCode = 400;
            await using var w = new StreamWriter(context.Response.OutputStream);
            await w.WriteAsync($"{{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{{\"code\":-32700,\"message\":\"Parse error: {ex.Message}\"}}}}");
            return;
        }

        if (message == null)
        {
            context.Response.StatusCode = 400;
            await using var w = new StreamWriter(context.Response.OutputStream);
            await w.WriteAsync("{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32600,\"message\":\"Empty request\"}}");
            return;
        }

        await using var transport = new StreamableHttpServerTransport { Stateless = true };
        await using var server = McpServer.Create(transport, _options);

        // The server loop and the request handler run concurrently: the handler feeds
        // the message in and writes the response out, the loop dispatches the tool call.
        var serverTask = server.RunAsync(cts.Token);

        await transport.HandlePostRequestAsync(message, context.Response.OutputStream, cts.Token);

        cts.Cancel();
        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            // Expected — we cancel the loop once the response has been written.
        }
    }
}
