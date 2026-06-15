// Copyright (c) VolcanicArts / Bluscream. Licensed under the LGPL License.
// See the LICENSE file in the repository root for full license text.

using System.Threading.Tasks;
using VRCOSC.App.Nodes;
using VRCOSC.App.SDK.Nodes;

namespace Bluscream.Modules;

[Node("Linux CPU Info Source")]
[NodeForceReprocess]
public sealed class LinuxCPUInfoSourceNode : ModuleNode<LinuxHardwareStatsModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> Usage = new();
    public ValueOutput<int> Power = new();
    public ValueOutput<int> Temperature = new();

    protected override Task Process(PulseContext c)
    {
        var cpu = Module.GetCPU();
        if (cpu is null) return Task.CompletedTask;

        Usage.Write(cpu.Usage / 100f, c);
        Power.Write(cpu.Power, c);
        Temperature.Write(cpu.Temperature, c);
        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}

[Node("Linux GPU Info Source")]
public sealed class LinuxGPUInfoSourceNode : ModuleNode<LinuxHardwareStatsModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> Usage = new();
    public ValueOutput<int> Power = new();
    public ValueOutput<int> Temperature = new();

    protected override Task Process(PulseContext c)
    {
        var gpu = Module.GetGPU();
        if (gpu is null) return Task.CompletedTask;

        Usage.Write(gpu.Usage / 100f, c);
        Power.Write(gpu.Power, c);
        Temperature.Write(gpu.Temperature, c);
        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}

[Node("Linux RAM Info Source")]
public sealed class LinuxRAMInfoSourceNode : ModuleNode<LinuxHardwareStatsModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> Usage = new();
    public ValueOutput<float> Total = new();
    public ValueOutput<float> Used = new();
    public ValueOutput<float> Free = new();

    protected override Task Process(PulseContext c)
    {
        var ram = Module.GetRAM();
        if (ram is null) return Task.CompletedTask;

        Usage.Write(ram.Usage / 100f, c);
        Total.Write(ram.Total, c);
        Used.Write(ram.Used, c);
        Free.Write(ram.Available, c);
        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}

[Node("Linux VRAM Info Source")]
public sealed class LinuxVRAMInfoSourceNode : ModuleNode<LinuxHardwareStatsModule>, IActiveUpdateNode
{
    public int UpdateOffset => 0;
    public ValueOutput<float> Usage = new();
    public ValueOutput<float> Total = new();
    public ValueOutput<float> Used = new();
    public ValueOutput<float> Free = new();

    protected override Task Process(PulseContext c)
    {
        var gpu = Module.GetGPU();
        if (gpu is null) return Task.CompletedTask;

        Usage.Write(gpu.MemoryUsage, c);
        Total.Write(gpu.MemoryTotal / 1000f, c);
        Used.Write(gpu.MemoryUsed / 1000f, c);
        Free.Write(gpu.MemoryFree / 1000f, c);
        return Task.CompletedTask;
    }

    public Task<bool> OnUpdate(PulseContext c) => Task.FromResult(true);
}
