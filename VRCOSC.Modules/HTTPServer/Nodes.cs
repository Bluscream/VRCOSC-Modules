// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

// Start HTTP Server Node
[Node("Start HTTP Server")]
public sealed class StartHTTPServerNode : ModuleNode<HTTPServerModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");

    public ValueOutput<bool> Success = new();
    public ValueOutput<string> ServerUrl = new("Server URL");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var success = await Module.StartServer();
            Success.Write(success, c);

            if (success)
            {
                ServerUrl.Write(Module.GetServerUrl(), c);
                await Next.Execute(c);
            }
            else
            {
                Error.Write("Failed to start server", c);
                await OnError.Execute(c);
            }
        }
        catch (System.Exception ex)
        {
            Success.Write(false, c);
            Error.Write(ex.Message, c);
            await OnError.Execute(c);
        }
    }
}

// Stop HTTP Server Node
[Node("Stop HTTP Server")]
public sealed class StopHTTPServerNode : ModuleNode<HTTPServerModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");

    public ValueOutput<bool> Success = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            Module.StopServer();
            Success.Write(true, c);
            await Next.Execute(c);
        }
        catch (System.Exception)
        {
            Success.Write(false, c);
            await Next.Execute(c);
        }
    }
}

// Get Server Status Node
[Node("Get HTTP Server Status")]
public sealed class GetHTTPServerStatusNode : ModuleNode<HTTPServerModule>
{
    public ValueOutput<bool> IsRunning = new("Is Running");
    public ValueOutput<string> ServerUrl = new("Server URL");

    protected override Task Process(PulseContext c)
    {
        IsRunning.Write(Module.IsRunning, c);
        ServerUrl.Write(Module.GetServerUrl(), c);
        return Task.CompletedTask;
    }
}
