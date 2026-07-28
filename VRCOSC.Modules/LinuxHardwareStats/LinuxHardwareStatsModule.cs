// Copyright (c) VolcanicArts / Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.IO;
using System.Text.RegularExpressions;
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
    private readonly LinuxNetwork _network = new();
    private bool _firstUpdateDone = false;

    protected override void OnPreLoad()
    {
        const string restartNote = " — requires a module restart to apply.";
        CreateTextBox(HardwareStatsSetting.SelectedCPU, "Selected CPU", "Index (0-based) of the CPU package to track. Most systems have only one (0)." + restartNote, 0);
        CreateTextBox(HardwareStatsSetting.SelectedGPU, "Selected GPU", "Index (0-based) of the GPU to track. Useful for iGPU + dGPU setups." + restartNote, 0);
        CreateTextBox(HardwareStatsSetting.NetworkInterface, "Network Interface", "Interface to monitor (e.g. enp6s0, eth0). Leave empty to combine all non-loopback interfaces." + restartNote, "");

        CreateTextBox(HardwareStatsSetting.RedactedWindowTitlePattern, "Redacted Window Title Pattern",
            "Regex pattern — if the active window title matches, it is replaced with the Redacted Text. Leave empty to disable.", "");
        CreateTextBox(HardwareStatsSetting.RedactedProcessNamePattern, "Redacted Process Name Pattern",
            "Regex pattern — if the active process name matches, it is replaced with the Redacted Text. Leave empty to disable.", "");
        CreateTextBox(HardwareStatsSetting.RedactedText, "Redacted Text",
            "Text shown when a window title or process name matches a redaction pattern.", "[REDACTED]");

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
        RegisterParameter<int>(HardwareStatsParameter.NetworkDownload, "VRCOSC/Hardware/Network/Download", ParameterMode.Write, "Network Download", "The network download speed (KB/s)");
        RegisterParameter<int>(HardwareStatsParameter.NetworkUpload, "VRCOSC/Hardware/Network/Upload", ParameterMode.Write, "Network Upload", "The network upload speed (KB/s)");
        RegisterParameter<int>(HardwareStatsParameter.SystemTemp, "VRCOSC/Hardware/System/Temp", ParameterMode.Write, "System Temp", "The system (ACPI/motherboard) temperature (C)");
        RegisterParameter<int>(HardwareStatsParameter.MaxTemp, "VRCOSC/Hardware/Max/Temp", ParameterMode.Write, "Max Temp", "The highest temperature across all sensors (C)");
        RegisterParameter<int>(HardwareStatsParameter.WindowFPS, "VRCOSC/Hardware/Window/FPS", ParameterMode.Write, "Window FPS", "The active window FPS (display refresh rate as baseline)");
    }

    protected override void OnPostLoad()
    {
        // --- CPU ---
        CreateVariable<string>(HardwareStatsVariable.CPUName, "CPU Name");
        CreateVariable<string>(HardwareStatsVariable.CPUManufacturer, "CPU Manufacturer");
        CreateVariable<string>(HardwareStatsVariable.CPUModel, "CPU Model");
        var cpuUsageReference = CreateVariable<int>(HardwareStatsVariable.CPUUsage, "CPU Usage (%)")!;
        CreateVariable<int>(HardwareStatsVariable.CPUPower, "CPU Power (W)");
        CreateVariable<int>(HardwareStatsVariable.CPUTemp, "CPU Temp (C)");

        // --- GPU ---
        CreateVariable<string>(HardwareStatsVariable.GPUName, "GPU Name");
        CreateVariable<string>(HardwareStatsVariable.GPUManufacturer, "GPU Manufacturer");
        CreateVariable<string>(HardwareStatsVariable.GPUModel, "GPU Model");
        var gpuUsageReference = CreateVariable<int>(HardwareStatsVariable.GPUUsage, "GPU Usage (%)")!;
        CreateVariable<int>(HardwareStatsVariable.GPUPower, "GPU Power (W)");
        CreateVariable<int>(HardwareStatsVariable.GPUTemp, "GPU Temp (C)");

        // --- RAM ---
        CreateVariable<float>(HardwareStatsVariable.RAMUsage, "RAM Usage (%)");
        var ramTotalReference = CreateVariable<float>(HardwareStatsVariable.RAMTotal, "RAM Total (GB)")!;
        var ramUsedReference = CreateVariable<float>(HardwareStatsVariable.RAMUsed, "RAM Used (GB)")!;
        CreateVariable<float>(HardwareStatsVariable.RAMFree, "RAM Free (GB)");

        // --- VRAM ---
        CreateVariable<float>(HardwareStatsVariable.VRAMUsage, "VRAM Usage (%)");
        CreateVariable<float>(HardwareStatsVariable.VRAMTotal, "VRAM Total (GB)");
        CreateVariable<float>(HardwareStatsVariable.VRAMUsed, "VRAM Used (GB)");
        CreateVariable<float>(HardwareStatsVariable.VRAMFree, "VRAM Free (GB)");

        // --- Network ---
        var netDownloadReference = CreateVariable<int>(HardwareStatsVariable.NetworkDownload, "Network Download (KB/s)")!;
        var netUploadReference = CreateVariable<int>(HardwareStatsVariable.NetworkUpload, "Network Upload (KB/s)")!;
        CreateVariable<float>(HardwareStatsVariable.NetworkRxTotal, "Network Received Total (MB)");
        CreateVariable<float>(HardwareStatsVariable.NetworkTxTotal, "Network Sent Total (MB)");
        CreateVariable<int>(HardwareStatsVariable.SystemTemp, "System Temp (C)");
        CreateVariable<int>(HardwareStatsVariable.MaxTemp, "Max Temp (C)");

        // --- Active Window ---
        CreateVariable<string>(HardwareStatsVariable.WindowTitle, "Active Window Title");
        CreateVariable<string>(HardwareStatsVariable.ProcessName, "Active Process Name");
        CreateVariable<int>(HardwareStatsVariable.WindowFPS, "Active Window FPS");

        CreateState(HardwareStatsState.Default, "Default", "CPU: {0}% | GPU: {1}%\nRAM: {2}GB/{3}GB", new[] { cpuUsageReference, gpuUsageReference, ramUsedReference, ramTotalReference });
        CreateState(HardwareStatsState.WithNetwork, "With Network", "CPU: {0}% | GPU: {1}%\nRAM: {2}GB/{3}GB\n↓{4} ↑{5} KB/s", new[] { cpuUsageReference, gpuUsageReference, ramUsedReference, ramTotalReference, netDownloadReference, netUploadReference });
    }

    protected override Task<bool> OnModuleStart()
    {
        DeployHelperScript();
        _firstUpdateDone = false;
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

            // Read the template and bake in the current settings
            using var reader = new StreamReader(stream);
            var gpuIndexRaw = GetSettingValue<string>(HardwareStatsSetting.SelectedGPU) ?? "0";
            var cpuIndexRaw = GetSettingValue<string>(HardwareStatsSetting.SelectedCPU) ?? "0";
            int.TryParse(gpuIndexRaw, out var gpuIndex);
            int.TryParse(cpuIndexRaw, out var cpuIndex);
            var netIface = GetSettingValue<string>(HardwareStatsSetting.NetworkInterface) ?? "";

            var scriptContent = reader.ReadToEnd()
                .Replace("GPU_INDEX=0", $"GPU_INDEX={gpuIndex}")
                .Replace("CPU_INDEX=0", $"CPU_INDEX={cpuIndex}")
                .Replace("NET_IFACE=\"\"", $"NET_IFACE=\"{netIface}\"");

            string wineHomeDir = "Z:" + homeDir.Replace('/', '\\');
            string wineTargetPath = Path.Combine(wineHomeDir, ".local", "bin", "vrcosc_hwstats.sh");

            string? dir = Path.GetDirectoryName(wineTargetPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(wineTargetPath, scriptContent);

            Log($"Linux hardware stats helper script deployed to {targetPath} (GPU={gpuIndex}, CPU={cpuIndex}, NET={(string.IsNullOrEmpty(netIface) ? "all" : netIface)})");
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
    public LinuxNetwork GetNetwork() => _network;

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

                // Parse structured name info
                var cpuInfo = HardwareNameParser.ParseCpu(cpuName);
                var gpuInfo = HardwareNameParser.ParseGpu(gpuName);

                _cpu.Name = cpuName;
                _cpu.Manufacturer = cpuInfo.Manufacturer;
                _cpu.Model = cpuInfo.Model;
                _cpu.Usage = cpuUsage;
                _cpu.Power = cpuPower;
                _cpu.Temperature = cpuTemp;

                _gpu.Name = gpuName;
                _gpu.Manufacturer = gpuInfo.Manufacturer;
                _gpu.Model = gpuInfo.Model;
                _gpu.Usage = gpuUsage;
                _gpu.Power = gpuPower;
                _gpu.Temperature = gpuTemp;
                _gpu.MemoryUsage = vramUsage;
                _gpu.MemoryTotal = vramTotal * 1000f;
                _gpu.MemoryUsed = vramUsed * 1000f;
                _gpu.MemoryFree = vramFree * 1000f;

                _ram.Usage = ramUsage * 100f;
                _ram.Total = ramTotal;
                _ram.Used = ramUsed;
                _ram.Available = ramFree;

                // Send Parameters
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
                SetVariableValue(HardwareStatsVariable.CPUManufacturer, _cpu.Manufacturer);
                SetVariableValue(HardwareStatsVariable.CPUModel, _cpu.Model);
                SetVariableValue(HardwareStatsVariable.CPUUsage, (int)Math.Round(cpuUsage));
                SetVariableValue(HardwareStatsVariable.CPUPower, cpuPower);
                SetVariableValue(HardwareStatsVariable.CPUTemp, cpuTemp);

                SetVariableValue(HardwareStatsVariable.GPUName, gpuName);
                SetVariableValue(HardwareStatsVariable.GPUManufacturer, _gpu.Manufacturer);
                SetVariableValue(HardwareStatsVariable.GPUModel, _gpu.Model);
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

                // Network + system/max temps (lines 16-21, only present in updated script)
                if (lines.Length >= 20)
                {
                    int.TryParse(lines[16].Trim(), out var netRxKbps);
                    int.TryParse(lines[17].Trim(), out var netTxKbps);
                    float.TryParse(lines[18].Trim(), out var netRxTotalMb);
                    float.TryParse(lines[19].Trim(), out var netTxTotalMb);

                    _network.RxKbps = netRxKbps;
                    _network.TxKbps = netTxKbps;
                    _network.RxTotalMb = netRxTotalMb;
                    _network.TxTotalMb = netTxTotalMb;

                    SendParameter(HardwareStatsParameter.NetworkDownload, netRxKbps);
                    SendParameter(HardwareStatsParameter.NetworkUpload, netTxKbps);

                    SetVariableValue(HardwareStatsVariable.NetworkDownload, netRxKbps);
                    SetVariableValue(HardwareStatsVariable.NetworkUpload, netTxKbps);
                    SetVariableValue(HardwareStatsVariable.NetworkRxTotal, netRxTotalMb);
                    SetVariableValue(HardwareStatsVariable.NetworkTxTotal, netTxTotalMb);
                }

                var systemTemp = 0;
                var maxTemp = 0;
                if (lines.Length >= 22)
                {
                    int.TryParse(lines[20].Trim(), out systemTemp);
                    int.TryParse(lines[21].Trim(), out maxTemp);

                    SendParameter(HardwareStatsParameter.SystemTemp, systemTemp);
                    SendParameter(HardwareStatsParameter.MaxTemp, maxTemp);

                    SetVariableValue(HardwareStatsVariable.SystemTemp, systemTemp);
                    SetVariableValue(HardwareStatsVariable.MaxTemp, maxTemp);
                }

                // Active window (lines 22-24)
                if (lines.Length >= 25)
                {
                    var windowTitle = lines[22].Trim();
                    var processName = lines[23].Trim();
                    int.TryParse(lines[24].Trim(), out var windowFps);

                    var titlePattern = GetSettingValue<string>(HardwareStatsSetting.RedactedWindowTitlePattern) ?? "";
                    var procPattern  = GetSettingValue<string>(HardwareStatsSetting.RedactedProcessNamePattern) ?? "";
                    var redactedText = GetSettingValue<string>(HardwareStatsSetting.RedactedText) ?? "[REDACTED]";

                    windowTitle = ApplyRedaction(windowTitle, titlePattern, redactedText);
                    processName = ApplyRedaction(processName, procPattern, redactedText);

                    SendParameter(HardwareStatsParameter.WindowFPS, windowFps);
                    SetVariableValue(HardwareStatsVariable.WindowTitle, windowTitle);
                    SetVariableValue(HardwareStatsVariable.ProcessName, processName);
                    SetVariableValue(HardwareStatsVariable.WindowFPS, windowFps);
                }

                if (!_firstUpdateDone)
                {
                    _firstUpdateDone = true;
                    LogDiagnostics(lines.Length, systemTemp);
                }
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

    private static string ApplyRedaction(string value, string pattern, string redactedText)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return value;
        try { return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase) ? redactedText : value; }
        catch { return value; } // invalid regex — leave unchanged
    }

    /// <summary>
    /// Runs once after the first successful data read and logs warnings for any
    /// sensor values that are zero / missing, with actionable remediation advice.
    /// </summary>
    private void LogDiagnostics(int lineCount, int systemTemp)
    {
        // --- CPU ---
        if (_cpu.Temperature == 0)
            Log("[DIAG] CPU temperature is 0 — no supported hwmon sensor found. " +
                "For Intel: ensure the 'coretemp' module is loaded (sudo modprobe coretemp). " +
                "For AMD: ensure 'k10temp' or 'zenpower' is loaded (sudo modprobe k10temp).");

        if (_cpu.Power == 0)
        {
            if (_cpu.Manufacturer.Equals("Intel", StringComparison.OrdinalIgnoreCase))
                Log("[DIAG] CPU power is 0 — Intel RAPL interface not available. " +
                    "Ensure the 'intel_rapl_common' module is loaded (sudo modprobe intel_rapl_common) " +
                    "and /sys/class/powercap/intel-rapl:0/energy_uj is readable (may need root or powercap group).");
            else if (_cpu.Manufacturer.Equals("AMD", StringComparison.OrdinalIgnoreCase))
                Log("[DIAG] CPU power is 0 — AMD CPU power via hwmon (k10temp/zenpower power1_average) " +
                    "is not exposed on this processor. Some Ryzen generations do not provide package power " +
                    "through sysfs; this is a hardware/driver limitation.");
            else
                Log("[DIAG] CPU power is 0 — power sensor not found. " +
                    "Intel: load 'intel_rapl_common'. AMD: load 'k10temp'.");
        }

        // --- GPU ---
        if (_gpu.Name is "Unknown GPU" or "AMD Radeon GPU")
            Log("[DIAG] GPU not detected or name is generic. " +
                "NVIDIA: ensure nvidia-smi is installed (nvidia-utils package). " +
                "AMD: ensure the 'amdgpu' kernel module is loaded and your card is supported.");

        if (_gpu.Temperature == 0)
            Log("[DIAG] GPU temperature is 0 — hwmon sensor not found for the GPU. " +
                "AMD: ensure 'amdgpu' module is loaded. " +
                "NVIDIA: ensure nvidia-smi is installed and working.");

        if (_gpu.Power == 0)
            Log("[DIAG] GPU power is 0 — power sensor not available. " +
                "AMD: check /sys/class/hwmon/hwmon*/power1_average (requires amdgpu driver). " +
                "NVIDIA: ensure nvidia-smi reports power.draw correctly (card may need to be in a supported mode).");

        // --- Temps ---
        if (lineCount >= 22 && systemTemp == 0)
            Log("[DIAG] System temperature is 0 — ACPI thermal zone (acpitz hwmon) not found. " +
                "This is normal on some systems; the sensor may simply not be exposed by firmware.");

        // --- Network ---
        if (lineCount < 20)
            Log("[DIAG] Network stats are missing — script is outdated. " +
                "The module will redeploy the script on next restart; " +
                "delete ~/.local/bin/vrcosc_hwstats.sh to force a fresh deploy.");
    }

    private enum HardwareStatsSetting
    {
        SelectedCPU,
        SelectedGPU,
        NetworkInterface,
        RedactedWindowTitlePattern,
        RedactedProcessNamePattern,
        RedactedText
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
        VRAMTotal,
        NetworkDownload,
        NetworkUpload,
        SystemTemp,
        MaxTemp,
        WindowFPS
    }

    private enum HardwareStatsState
    {
        Default,
        WithNetwork
    }

    private enum HardwareStatsVariable
    {
        CPUName,
        CPUManufacturer,
        CPUModel,
        CPUUsage,
        CPUPower,
        CPUTemp,
        GPUName,
        GPUManufacturer,
        GPUModel,
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
        VRAMTotal,
        NetworkDownload,
        NetworkUpload,
        NetworkRxTotal,
        NetworkTxTotal,
        SystemTemp,
        MaxTemp,
        WindowTitle,
        ProcessName,
        WindowFPS
    }
}

// ---------------------------------------------------------------------------
// Data classes
// ---------------------------------------------------------------------------

public class LinuxCPU
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public float Usage { get; set; }
    public int Power { get; set; }
    public int Temperature { get; set; }
}

public class LinuxGPU
{
    public string Name { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
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

public class LinuxNetwork
{
    public float RxKbps { get; set; }
    public float TxKbps { get; set; }
    public float RxTotalMb { get; set; }
    public float TxTotalMb { get; set; }
}

// ---------------------------------------------------------------------------
// Hardware name parser
// ---------------------------------------------------------------------------

/// <summary>
/// Parses raw CPU/GPU name strings (from /proc/cpuinfo and lspci/nvidia-smi)
/// into structured manufacturer, model, and supplementary fields.
/// </summary>
public static class HardwareNameParser
{
    public record CpuInfo(string Manufacturer, string Model, string FullName);
    public record GpuInfo(string Manufacturer, string Model, string FullName);

    // Compiled regexes - shared across calls
    private static readonly Regex CpuNoisyTokens    = new(@"\(R\)|\(TM\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CpuGenPrefix      = new(@"^\d+\w*\s+Gen\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CpuAtFreqSuffix   = new(@"\s+CPU\s*@.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CpuCoresSuffix    = new(@"\s+\d+-Core.*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Intel model number: i3/i5/i7/i9-NNNNN[K/T/H/X/...] or Xeon XXXXX [vN]
    private static readonly Regex IntelModelRegex   = new(@"\b([im][0-9]-[0-9]+[A-Z0-9]*(?:\s+v\d+)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IntelXeonRegex    = new(@"\b(Xeon\s+[A-Z0-9\-]+(?:\s+v\d+)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // VRAM label: trailing "16GB" / "8 GB" in GPU name
    private static readonly Regex GpuVramRegex      = new(@"\b(\d+\s*GB)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a raw /proc/cpuinfo "model name" string.
    /// Examples:
    ///   "Intel(R) Core(TM) i9-10900K CPU @ 3.70GHz"  → Intel / i9-10900K
    ///   "12th Gen Intel(R) Core(TM) i5-12600K"        → Intel / i5-12600K
    ///   "AMD Ryzen 9 5900X 12-Core Processor"         → AMD   / Ryzen 9 5900X
    ///   "AMD Ryzen 7 7800X3D 8-Core Processor"        → AMD   / Ryzen 7 7800X3D
    /// </summary>
    public static CpuInfo ParseCpu(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return new CpuInfo("", "", fullName);

        if (fullName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            // Strip noise tokens and normalise
            var s = CpuNoisyTokens.Replace(fullName, "");
            s = CpuGenPrefix.Replace(s, "");      // "12th Gen " → ""
            s = CpuAtFreqSuffix.Replace(s, "");   // " CPU @ 3.70GHz" → ""
            s = CpuCoresSuffix.Replace(s, "");    // " 12-Core Processor" → ""
            s = s.Trim();

            // Try model number patterns
            var m = IntelModelRegex.Match(s);
            if (!m.Success) m = IntelXeonRegex.Match(s);

            var model = m.Success
                ? m.Groups[1].Value.Trim()
                : s.Replace("Intel", "").Replace("Core", "").Trim();

            return new CpuInfo("Intel", model, fullName);
        }

        if (fullName.StartsWith("AMD", StringComparison.OrdinalIgnoreCase))
        {
            // Strip "X-Core Processor" suffix, then "AMD " prefix
            var s = CpuCoresSuffix.Replace(fullName, "").Trim();
            var model = Regex.Replace(s, @"^AMD\s+", "", RegexOptions.IgnoreCase).Trim();
            return new CpuInfo("AMD", model, fullName);
        }

        // Unknown vendor — return whole name as model
        return new CpuInfo("", fullName, fullName);
    }

    /// <summary>
    /// Parses a GPU name from nvidia-smi or lspci SDevice.
    /// Examples:
    ///   "NVIDIA GeForce RTX 4090"        → NVIDIA / GeForce RTX 4090
    ///   "AMD Radeon RX 9070 XT 16GB"     → AMD    / Radeon RX 9070 XT  / 16GB
    ///   "Radeon RX 6800 XT"              → AMD    / Radeon RX 6800 XT
    ///   "Intel Arc A770"                 → Intel  / Arc A770
    ///   "Intel Arc B580 Limited Edition" → Intel  / Arc B580 Limited Edition
    /// </summary>
    public static GpuInfo ParseGpu(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return new GpuInfo("", "", fullName);

        // Strip trailing VRAM label (e.g., "16GB") for cleaner model name
        var vramMatch = GpuVramRegex.Match(fullName);
        var withoutVram = vramMatch.Success
            ? fullName[..vramMatch.Index].TrimEnd()
            : fullName;

        if (fullName.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            var model = Regex.Replace(withoutVram, @"^NVIDIA\s+", "", RegexOptions.IgnoreCase).Trim();
            return new GpuInfo("NVIDIA", model, fullName);
        }

        if (fullName.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            fullName.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
        {
            var model = Regex.Replace(withoutVram, @"^AMD\s+", "", RegexOptions.IgnoreCase).Trim();
            return new GpuInfo("AMD", model, fullName);
        }

        if (fullName.Contains("Intel", StringComparison.OrdinalIgnoreCase))
        {
            var model = Regex.Replace(withoutVram, @"^Intel\s+", "", RegexOptions.IgnoreCase).Trim();
            return new GpuInfo("Intel", model, fullName);
        }

        return new GpuInfo("", fullName, fullName);
    }
}
