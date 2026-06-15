// Copyright (c) VolcanicArts / Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.IO;
using System.Threading.Tasks;
using Bluscream.Modules.Utilities;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("Linux Hardware Stats")]
[ModuleDescription("Sends hardware stats as avatar parameters and allows for displaying them in the ChatBox on Linux hosts")]
[ModuleType(ModuleType.Generic)]
public sealed class LinuxHardwareStatsModule : Module
{
    private readonly LinuxCPU _cpu = new();
    private readonly LinuxGPU _gpu = new();
    private readonly LinuxRAM _ram = new();

    protected override void OnPreLoad()
    {
        CreateTextBox(HardwareStatsSetting.SelectedCPU, "Selected CPU", "Enter the (0th based) index of the CPU you want to track", 0);
        CreateTextBox(HardwareStatsSetting.SelectedGPU, "Selected GPU", "Enter the (0th based) index of the GPU you want to track", 0);

        RegisterParameter<float>(HardwareStatsParameter.CPUUsage, "VRCOSC/Hardware/CPU/Usage", ParameterMode.Write, "CPU Usage", "The CPU usage (0-1)");
        RegisterParameter<int>(HardwareStatsParameter.CPUPower, "VRCOSC/Hardware/CPU/Power", ParameterMode.Write, "CPU Power", "The CPU power draw (W)");
        RegisterParameter<int>(HardwareStatsParameter.CPUTemp, "VRCOSC/Hardware/CPU/Temp", ParameterMode.Write, "CPU Temp", "The CPU temperature (C)");
        RegisterParameter<float>(HardwareStatsParameter.GPUUsage, "VRCOSC/Hardware/GPU/Usage", ParameterMode.Write, "GPU Usage", "The GPU usage (0-1)");
        RegisterParameter<int>(HardwareStatsParameter.GPUPower, "VRCOSC/Hardware/GPU/Power", ParameterMode.Write, "GPU Power", "The GPU power draw (W)");
        RegisterParameter<int>(HardwareStatsParameter.GPUTemp, "VRCOSC/Hardware/GPU/Temp", ParameterMode.Write, "GPU Temp", "The GPU temperature (C)");
        RegisterParameter<float>(HardwareStatsParameter.RAMUsage, "VRCOSC/Hardware/RAM/Usage", ParameterMode.Write, "RAM Usage", "The RAM usage (0-1)");
        RegisterParameter<int>(HardwareStatsParameter.RAMTotal, "VRCOSC/Hardware/RAM/Total", ParameterMode.Write, "RAM Total", "The total RAM amount (GB)");
        RegisterParameter<int>(HardwareStatsParameter.RAMUsed, "VRCOSC/Hardware/RAM/Used", ParameterMode.Write, "RAM Used", "The used RAM amount (GB)");
        RegisterParameter<int>(HardwareStatsParameter.RAMFree, "VRCOSC/Hardware/RAM/Free", ParameterMode.Write, "RAM Free", "The free RAM amount (GB)");
        RegisterParameter<float>(HardwareStatsParameter.VRAMUsage, "VRCOSC/Hardware/VRAM/Usage", ParameterMode.Write, "VRAM Usage", "The VRAM usage (0-1)");
        RegisterParameter<int>(HardwareStatsParameter.VRAMTotal, "VRCOSC/Hardware/VRAM/Total", ParameterMode.Write, "VRAM Total", "The total VRAM amount (GB)");
        RegisterParameter<int>(HardwareStatsParameter.VRAMUsed, "VRCOSC/Hardware/VRAM/Used", ParameterMode.Write, "VRAM Used", "The used VRAM amount (GB)");
        RegisterParameter<int>(HardwareStatsParameter.VRAMFree, "VRCOSC/Hardware/VRAM/Free", ParameterMode.Write, "VRAM Free", "The free VRAM amount (GB)");
    }

    protected override void OnPostLoad()
    {
        CreateVariable<string>(HardwareStatsVariable.CPUName, "CPU Name");
        var cpuUsageReference = CreateVariable<int>(HardwareStatsVariable.CPUUsage, "CPU Usage (%)")!;
        CreateVariable<int>(HardwareStatsVariable.CPUPower, "CPU Power (W)");
        CreateVariable<int>(HardwareStatsVariable.CPUTemp, "CPU Temp (C)");

        CreateVariable<string>(HardwareStatsVariable.GPUName, "GPU Name");
        var gpuUsageReference = CreateVariable<int>(HardwareStatsVariable.GPUUsage, "GPU Usage (%)")!;
        CreateVariable<int>(HardwareStatsVariable.GPUPower, "GPU Power (W)");
        CreateVariable<int>(HardwareStatsVariable.GPUTemp, "GPU Temp (C)");

        CreateVariable<float>(HardwareStatsVariable.RAMUsage, "RAM Usage (%)");
        var ramTotalReference = CreateVariable<float>(HardwareStatsVariable.RAMTotal, "RAM Total (GB)")!;
        var ramUsedReference = CreateVariable<float>(HardwareStatsVariable.RAMUsed, "RAM Used (GB)")!;
        CreateVariable<float>(HardwareStatsVariable.RAMFree, "RAM Free (GB)");

        CreateVariable<float>(HardwareStatsVariable.VRAMUsage, "VRAM Usage (%)");
        CreateVariable<float>(HardwareStatsVariable.VRAMTotal, "VRAM Total (GB)");
        CreateVariable<float>(HardwareStatsVariable.VRAMUsed, "VRAM Used (GB)");
        CreateVariable<float>(HardwareStatsVariable.VRAMFree, "VRAM Free (GB)");

        CreateState(HardwareStatsState.Default, "Default", "CPU: {0}% | GPU: {1}%\nRAM: {2}GB/{3}GB", new[] { cpuUsageReference, gpuUsageReference, ramUsedReference, ramTotalReference });
    }

    protected override Task<bool> OnModuleStart()
    {
        DeployHelperScript();
        ChangeState(HardwareStatsState.Default);
        return Task.FromResult(true);
    }

    private void DeployHelperScript()
    {
        try
        {
            string homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/home/blu";
            string targetPath = $"{homeDir}/.local/bin/vrcosc_hwstats.sh";

            var assembly = typeof(LinuxHardwareStatsModule).Assembly;
            using var stream = assembly.GetManifestResourceStream("Bluscream.Modules.LinuxHardwareStats.vrcosc_hwstats.sh");
            if (stream == null)
            {
                Log("Error: Could not find embedded hardware stats script resource.");
                return;
            }

            string wineHomeDir = "Z:" + homeDir.Replace('/', '\\');
            string wineTargetPath = Path.Combine(wineHomeDir, ".local", "bin", "vrcosc_hwstats.sh");

            string? dir = Path.GetDirectoryName(wineTargetPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fileStream = File.Create(wineTargetPath))
            {
                stream.CopyTo(fileStream);
            }

            Log($"Linux hardware stats helper script deployed to {targetPath}");
            LinuxUtils.ChmodPlusX(targetPath, ex => Log($"Error making script executable: {ex.Message}"));
        }
        catch (Exception ex)
        {
            Log($"Error deploying hardware stats helper script: {ex.Message}");
        }
    }

    public LinuxCPU GetCPU() => _cpu;
    public LinuxGPU GetGPU() => _gpu;
    public LinuxRAM GetRAM() => _ram;

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 2000)]
    private void UpdateParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;
        try
        {
            string homeDir = Environment.GetEnvironmentVariable("HOME") ?? "/home/blu";
            string wineHomeDir = "Z:" + homeDir.Replace('/', '\\');
            string tempFile = Path.Combine(wineHomeDir, ".vrcosc_hwstats.txt");

            // Run the host script to generate stats
            LinuxUtils.RunHost("/home/blu/.local/bin/vrcosc_hwstats.sh", ex => Log($"Error running hwstats script: {ex.Message}"));

            if (!File.Exists(tempFile))
            {
                return;
            }

            string[] lines = File.ReadAllLines(tempFile);
            if (lines.Length >= 16)
            {
                float.TryParse(lines[0].Trim(), out var cpuUsage);
                int.TryParse(lines[1].Trim(), out var cpuPower);
                int.TryParse(lines[2].Trim(), out var cpuTemp);
                float.TryParse(lines[3].Trim(), out var gpuUsage);
                int.TryParse(lines[4].Trim(), out var gpuPower);
                int.TryParse(lines[5].Trim(), out var gpuTemp);
                float.TryParse(lines[6].Trim(), out var ramUsage);
                float.TryParse(lines[7].Trim(), out var ramTotal);
                float.TryParse(lines[8].Trim(), out var ramUsed);
                float.TryParse(lines[9].Trim(), out var ramFree);
                float.TryParse(lines[10].Trim(), out var vramUsage);
                float.TryParse(lines[11].Trim(), out var vramTotal);
                float.TryParse(lines[12].Trim(), out var vramUsed);
                float.TryParse(lines[13].Trim(), out var vramFree);
                var cpuName = lines[14].Trim();
                var gpuName = lines[15].Trim();

                _cpu.Name = cpuName;
                _cpu.Usage = cpuUsage;
                _cpu.Power = cpuPower;
                _cpu.Temperature = cpuTemp;

                _gpu.Name = gpuName;
                _gpu.Usage = gpuUsage;
                _gpu.Power = gpuPower;
                _gpu.Temperature = gpuTemp;
                _gpu.MemoryUsage = vramUsage;
                _gpu.MemoryTotal = vramTotal * 1000f; // Store in MB to match Components.cs units if needed
                _gpu.MemoryUsed = vramUsed * 1000f;
                _gpu.MemoryFree = vramFree * 1000f;

                _ram.Usage = ramUsage * 100f;
                _ram.Total = ramTotal;
                _ram.Used = ramUsed;
                _ram.Available = ramFree;

                // Send Parameters (match Types of parameters in pre-load)
                SendParameter(HardwareStatsParameter.CPUUsage, cpuUsage / 100f);
                SendParameter(HardwareStatsParameter.CPUPower, cpuPower);
                SendParameter(HardwareStatsParameter.CPUTemp, cpuTemp);

                SendParameter(HardwareStatsParameter.GPUUsage, gpuUsage / 100f);
                SendParameter(HardwareStatsParameter.GPUPower, gpuPower);
                SendParameter(HardwareStatsParameter.GPUTemp, gpuTemp);

                SendParameter(HardwareStatsParameter.RAMUsage, ramUsage);
                SendParameter(HardwareStatsParameter.RAMTotal, (int)Math.Round(ramTotal));
                SendParameter(HardwareStatsParameter.RAMUsed, (int)Math.Round(ramUsed));
                SendParameter(HardwareStatsParameter.RAMFree, (int)Math.Round(ramFree));

                SendParameter(HardwareStatsParameter.VRAMUsage, vramUsage);
                SendParameter(HardwareStatsParameter.VRAMTotal, (int)Math.Round(vramTotal));
                SendParameter(HardwareStatsParameter.VRAMUsed, (int)Math.Round(vramUsed));
                SendParameter(HardwareStatsParameter.VRAMFree, (int)Math.Round(vramFree));

                // Set Variable Values
                SetVariableValue(HardwareStatsVariable.CPUName, cpuName);
                SetVariableValue(HardwareStatsVariable.CPUUsage, (int)Math.Round(cpuUsage));
                SetVariableValue(HardwareStatsVariable.CPUPower, cpuPower);
                SetVariableValue(HardwareStatsVariable.CPUTemp, cpuTemp);

                SetVariableValue(HardwareStatsVariable.GPUName, gpuName);
                SetVariableValue(HardwareStatsVariable.GPUUsage, (int)Math.Round(gpuUsage));
                SetVariableValue(HardwareStatsVariable.GPUPower, gpuPower);
                SetVariableValue(HardwareStatsVariable.GPUTemp, gpuTemp);

                SetVariableValue(HardwareStatsVariable.RAMUsage, ramUsage * 100f);
                SetVariableValue(HardwareStatsVariable.RAMTotal, ramTotal);
                SetVariableValue(HardwareStatsVariable.RAMUsed, ramUsed);
                SetVariableValue(HardwareStatsVariable.RAMFree, ramFree);

                SetVariableValue(HardwareStatsVariable.VRAMUsage, vramUsage * 100f);
                SetVariableValue(HardwareStatsVariable.VRAMTotal, vramTotal);
                SetVariableValue(HardwareStatsVariable.VRAMUsed, vramUsed);
                SetVariableValue(HardwareStatsVariable.VRAMFree, vramFree);
            }
        }
        catch (Exception ex)
        {
            Log($"Error updating hardware stats: {ex.Message}");
        }
    }

    protected override Task OnModuleStop()
    {
        return Task.CompletedTask;
    }

    private enum HardwareStatsSetting
    {
        SelectedCPU,
        SelectedGPU
    }

    private enum HardwareStatsParameter
    {
        CPUUsage,
        CPUPower,
        CPUTemp,
        GPUUsage,
        GPUPower,
        GPUTemp,
        RAMUsage,
        RAMTotal,
        RAMUsed,
        RAMFree,
        VRAMUsage,
        VRAMFree,
        VRAMUsed,
        VRAMTotal
    }

    private enum HardwareStatsState
    {
        Default
    }

    private enum HardwareStatsVariable
    {
        CPUName,
        CPUUsage,
        CPUPower,
        CPUTemp,
        GPUName,
        GPUUsage,
        GPUPower,
        GPUTemp,
        RAMUsage,
        RAMTotal,
        RAMUsed,
        RAMFree,
        VRAMUsage,
        VRAMFree,
        VRAMUsed,
        VRAMTotal
    }
}

public class LinuxCPU
{
    public string Name { get; set; } = string.Empty;
    public float Usage { get; set; }
    public int Power { get; set; }
    public int Temperature { get; set; }
}

public class LinuxGPU
{
    public string Name { get; set; } = string.Empty;
    public float Usage { get; set; }
    public int Power { get; set; }
    public int Temperature { get; set; }
    public float MemoryUsage { get; set; }
    public float MemoryTotal { get; set; }
    public float MemoryUsed { get; set; }
    public float MemoryFree { get; set; }
}

public class LinuxRAM
{
    public float Usage { get; set; }
    public float Total { get; set; }
    public float Used { get; set; }
    public float Available { get; set; }
}
