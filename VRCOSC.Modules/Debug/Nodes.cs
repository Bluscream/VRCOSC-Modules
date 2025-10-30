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
    public ValueInput<string?> File = new("File", "Optional custom file path (leave empty for auto-generated)");
    public FlowContinuation Next = new("Next");
    public FlowContinuation OnError = new("On Error");
    public ValueOutput<string> FilePath = new("File Path");
    public ValueOutput<int> TotalParameters = new("Total Parameters");
    public ValueOutput<string> Error = new();

    protected override async Task Process(PulseContext c)
    {
        try
        {
            var customPath = File.Read(c);
            var filepath = await Module.DumpParametersAsync(customFilePath: customPath);
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

// Commented out - Clear does not work with VRCOSC's built-in tracking
// [Node("Clear Parameter Tracking")]
// public sealed class ClearParameterTrackingNode : ModuleNode<DebugModule>, IFlowInput
// {
//     public FlowContinuation Next = new("Next");
// 
//     protected override async Task Process(PulseContext c)
//     {
//         Module.ClearTracking();
//         await Next.Execute(c);
//     }
// }

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
