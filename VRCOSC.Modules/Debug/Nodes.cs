// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

[Node("Dump All Parameters")]
public sealed class DumpAllParametersNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    public ValueOutput<string> FilePath = new("File Path");
    public ValueOutput<int> TotalParameters = new("Total Parameters");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var filepath = await Module.DumpParametersAsync();
            var incoming = Module.GetIncomingParameters();
            var outgoing = Module.GetOutgoingParameters();
            var total = incoming.Count + outgoing.Count;

            FilePath.Write(filepath, c);
            TotalParameters.Write(total, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            var errorMsg = $"Failed to dump parameters: {ex.Message}";
            Error.Write(errorMsg, c);
            await OnError.Execute(c);
        }
    }
}

[Node("Dump Incoming Parameters")]
public sealed class DumpIncomingParametersNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    public ValueOutput<string> FilePath = new("File Path");
    public ValueOutput<int> ParameterCount = new("Parameter Count");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var filepath = await Module.DumpParametersAsync(includeIncoming: true, includeOutgoing: false);
            var count = Module.GetIncomingParameters().Count;

            FilePath.Write(filepath, c);
            ParameterCount.Write(count, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            var errorMsg = $"Failed to dump incoming parameters: {ex.Message}";
            Error.Write(errorMsg, c);
            await OnError.Execute(c);
        }
    }
}

[Node("Dump Outgoing Parameters")]
public sealed class DumpOutgoingParametersNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    public ValueOutput<string> FilePath = new("File Path");
    public ValueOutput<int> ParameterCount = new("Parameter Count");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var filepath = await Module.DumpParametersAsync(includeIncoming: false, includeOutgoing: true);
            var count = Module.GetOutgoingParameters().Count;

            FilePath.Write(filepath, c);
            ParameterCount.Write(count, c);
            await Next.Execute(c);
        }
        catch (System.Exception ex)
        {
            var errorMsg = $"Failed to dump outgoing parameters: {ex.Message}";
            Error.Write(errorMsg, c);
            await OnError.Execute(c);
        }
    }
}

[Node("Clear Parameter Tracking")]
public sealed class ClearParameterTrackingNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");

    protected override async Task Process(PulseContext c)
    {
        Module.ClearTracking();
        await Next.Execute(c);
    }
}

[Node("Get Parameter Counts")]
public sealed class GetParameterCountsNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public ValueOutput<int> IncomingCount = new("Incoming Count");
    public ValueOutput<int> OutgoingCount = new("Outgoing Count");
    public ValueOutput<int> TotalCount = new("Total Count");

    protected override async Task Process(PulseContext c)
    {
        var incoming = Module.GetIncomingParameters().Count;
        var outgoing = Module.GetOutgoingParameters().Count;
        var total = incoming + outgoing;

        IncomingCount.Write(incoming, c);
        OutgoingCount.Write(outgoing, c);
        TotalCount.Write(total, c);
        await Next.Execute(c);
    }
}

[Node("Get Incoming Parameters")]
public sealed class GetIncomingParametersNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public ValueOutput<Dictionary<string, object>> Parameters = new("Parameters");
    public ValueOutput<int> Count = new("Count");

    protected override async Task Process(PulseContext c)
    {
        var incoming = Module.GetIncomingParameters();
        var dict = new Dictionary<string, object>();
        
        foreach (var param in incoming.Values)
        {
            dict[param.Path] = param.Value ?? "null";
        }

        Parameters.Write(dict, c);
        Count.Write(dict.Count, c);
        await Next.Execute(c);
    }
}

[Node("Get Outgoing Parameters")]
public sealed class GetOutgoingParametersNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public ValueOutput<Dictionary<string, object>> Parameters = new("Parameters");
    public ValueOutput<int> Count = new("Count");

    protected override async Task Process(PulseContext c)
    {
        var outgoing = Module.GetOutgoingParameters();
        var dict = new Dictionary<string, object>();
        
        foreach (var param in outgoing.Values)
        {
            dict[param.Path] = param.Value ?? "null";
        }

        Parameters.Write(dict, c);
        Count.Write(dict.Count, c);
        await Next.Execute(c);
    }
}

[Node("Get All Parameters")]
public sealed class GetAllParametersNode : ModuleNode<DebugModule>, IFlowInput
{
    public FlowContinuation Next = new("Next");
    public ValueOutput<Dictionary<string, object>> Parameters = new("Parameters");
    public ValueOutput<int> IncomingCount = new("Incoming Count");
    public ValueOutput<int> OutgoingCount = new("Outgoing Count");
    public ValueOutput<int> TotalCount = new("Total Count");

    protected override async Task Process(PulseContext c)
    {
        var incoming = Module.GetIncomingParameters();
        var outgoing = Module.GetOutgoingParameters();
        var dict = new Dictionary<string, object>();
        
        foreach (var param in incoming.Values)
        {
            dict[$"IN:{param.Path}"] = param.Value ?? "null";
        }
        
        foreach (var param in outgoing.Values)
        {
            dict[$"OUT:{param.Path}"] = param.Value ?? "null";
        }

        Parameters.Write(dict, c);
        IncomingCount.Write(incoming.Count, c);
        OutgoingCount.Write(outgoing.Count, c);
        TotalCount.Write(dict.Count, c);
        await Next.Execute(c);
    }
}
