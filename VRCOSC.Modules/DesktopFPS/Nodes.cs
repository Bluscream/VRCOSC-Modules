// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

// ============================
// FPS Value Nodes
// ============================

[Node("Get FPS")]
[NodeForceReprocess]
public sealed class GetFPSNode : ModuleNode<DesktopFPSModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> FPS = new();

    protected override Task Process(PulseContext c)
    {
        FPS.Write(Module.GetFPS(), c);
        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}

[Node("Is VRChat Running")]
[NodeForceReprocess]
public sealed class IsVRChatRunningNode : ModuleNode<DesktopFPSModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<bool> IsRunning = new();

    protected override Task Process(PulseContext c)
    {
        IsRunning.Write(Module.IsVRChatRunning(), c);
        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}
