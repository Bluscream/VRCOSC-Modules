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

[Node("Get VRChat FPS")]
[NodeForceReprocess]
public sealed class GetVRChatFPSNode : ModuleNode<DesktopFPSModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> FPS = new();
    public ValueOutput<float> AverageFPS = new();
    public ValueOutput<float> MinFPS = new();
    public ValueOutput<float> MaxFPS = new();

    protected override Task Process(PulseContext c)
    {
        FPS.Write(Module.GetVRChatFPS(), c);
        AverageFPS.Write(Module.GetAverageVRChatFPS(), c);
        MinFPS.Write(Module.GetMinVRChatFPS(), c);
        MaxFPS.Write(Module.GetMaxVRChatFPS(), c);

        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}

[Node("Get System FPS")]
[NodeForceReprocess]
public sealed class GetSystemFPSNode : ModuleNode<DesktopFPSModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> FPS = new();
    public ValueOutput<float> AverageFPS = new();

    protected override Task Process(PulseContext c)
    {
        FPS.Write(Module.GetSystemFPS(), c);
        AverageFPS.Write(Module.GetAverageSystemFPS(), c);

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

// ============================
// Event Nodes
// ============================

[Node("On VRChat FPS Changed")]
public sealed class OnVRChatFPSChangedNode : ModuleNode<DesktopFPSModule>, IModuleNodeEventHandler
{
    public FlowCall OnChanged = new("On Changed");

    public ValueOutput<float> FPS = new();
    public ValueOutput<float> AverageFPS = new();

    public Task Write(object[] args, PulseContext c)
    {
        FPS.Write(Module.GetVRChatFPS(), c);
        AverageFPS.Write(Module.GetAverageVRChatFPS(), c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnChanged.Execute(c);
    }
}

[Node("On System FPS Changed")]
public sealed class OnSystemFPSChangedNode : ModuleNode<DesktopFPSModule>, IModuleNodeEventHandler
{
    public FlowCall OnChanged = new("On Changed");

    public ValueOutput<float> FPS = new();
    public ValueOutput<float> AverageFPS = new();

    public Task Write(object[] args, PulseContext c)
    {
        FPS.Write(Module.GetSystemFPS(), c);
        AverageFPS.Write(Module.GetAverageSystemFPS(), c);
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnChanged.Execute(c);
    }
}

[Node("On VRChat Process Found")]
public sealed class OnVRChatProcessFoundNode : ModuleNode<DesktopFPSModule>, IModuleNodeEventHandler
{
    public FlowCall OnFound = new("On Found");

    public Task Write(object[] args, PulseContext c)
    {
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnFound.Execute(c);
    }
}

[Node("On VRChat Process Lost")]
public sealed class OnVRChatProcessLostNode : ModuleNode<DesktopFPSModule>, IModuleNodeEventHandler
{
    public FlowCall OnLost = new("On Lost");

    public Task Write(object[] args, PulseContext c)
    {
        return Task.CompletedTask;
    }

    protected override async Task Process(PulseContext c)
    {
        await OnLost.Execute(c);
    }
}
