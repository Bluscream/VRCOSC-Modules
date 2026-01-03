// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Management;

namespace Bluscream.Modules.DesktopFPS.Utils;

/// <summary>
/// Utilities for measuring FPS using Windows Performance Counters and display refresh rate
/// </summary>
public static class FPSMeasurementUtils
{
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    private const int ENUM_CURRENT_SETTINGS = -1;

    private static readonly Dictionary<int, ProcessFPSData> _processFpsData = new();
    private static readonly Dictionary<int, PerformanceCounter?> _processPerformanceCounters = new();
    private static readonly Dictionary<int, PerformanceCounter?> _processGpuEngineCounters = new();
    private static double _systemRefreshRate = 0;
    private static bool _systemRefreshRateInitialized = false;
    private static long _systemFpsFrequency = 0;

    static FPSMeasurementUtils()
    {
        if (QueryPerformanceFrequency(out _systemFpsFrequency))
        {
            InitializeSystemRefreshRate();
        }
    }

    private static void InitializeSystemRefreshRate()
    {
        try
        {
            DEVMODE devMode = new DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            
            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
            {
                _systemRefreshRate = devMode.dmDisplayFrequency;
                _systemRefreshRateInitialized = true;
            }
        }
        catch
        {
            // Fallback: try WMI query
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT CurrentRefreshRate FROM Win32_VideoController WHERE CurrentRefreshRate IS NOT NULL"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var refreshRate = Convert.ToDouble(obj["CurrentRefreshRate"]);
                        if (refreshRate > 0)
                        {
                            // Round refresh rates like 59 Hz to 60 Hz (common reporting quirk)
                            _systemRefreshRate = refreshRate >= 58 && refreshRate <= 62 ? 60.0 : refreshRate;
                            _systemRefreshRateInitialized = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // If all else fails, default to 60 Hz
                _systemRefreshRate = 60.0;
                _systemRefreshRateInitialized = true;
            }
        }
    }

    /// <summary>
    /// Find VRChat.exe process
    /// </summary>
    public static Process? FindVRChatProcess()
    {
        try
        {
            return Process.GetProcessesByName("VRChat").FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get FPS for a specific process using Performance Counters or CPU time measurement
    /// </summary>
    public static double GetProcessFPS(Process? process, int smoothingWindow = 10)
    {
        if (process == null || process.HasExited)
        {
            return 0;
        }

        try
        {
            if (!_processFpsData.TryGetValue(process.Id, out var fpsData))
            {
                fpsData = new ProcessFPSData();
                _processFpsData[process.Id] = fpsData;
            }

            double currentFPS = 0;

            // Try to use Direct3D Performance Counter first (GPU frame rate)
            PerformanceCounter? perfCounter = null;
            if (!_processPerformanceCounters.TryGetValue(process.Id, out perfCounter) || perfCounter == null)
            {
                // Try to create performance counter for GPU frame rate
                // Try NVIDIA first
                try
                {
                    perfCounter = new PerformanceCounter("NVIDIA Direct3D Driver", "D3D FPS", "CPU", true);
                    perfCounter.NextValue(); // First call returns 0
                    _processPerformanceCounters[process.Id] = perfCounter;
                }
                catch
                {
                    // Try AMD
                    try
                    {
                        perfCounter = new PerformanceCounter("AMD Direct3D Driver", "D3D FPS", "CPU", true);
                        perfCounter.NextValue();
                        _processPerformanceCounters[process.Id] = perfCounter;
                    }
                    catch
                    {
                        // Try Intel
                        try
                        {
                            perfCounter = new PerformanceCounter("Intel Direct3D Driver", "D3D FPS", "CPU", true);
                            perfCounter.NextValue();
                            _processPerformanceCounters[process.Id] = perfCounter;
                        }
                        catch
                        {
                            _processPerformanceCounters[process.Id] = null;
                        }
                    }
                }
            }

            if (perfCounter != null)
            {
                try
                {
                    currentFPS = perfCounter.NextValue();
                    if (currentFPS > 0)
                    {
                        // Use performance counter value
                        fpsData.FPSQueue.Enqueue(currentFPS);
                        if (fpsData.FPSQueue.Count > smoothingWindow)
                        {
                            fpsData.FPSQueue.Dequeue();
                        }
                        double averageFPS = fpsData.FPSQueue.Average();
                        fpsData.LastFPS = averageFPS;
                        return averageFPS;
                    }
                }
                catch
                {
                    // Performance counter failed, fall through to GPU Engine method
                }
            }

            // Try GPU Engine counter (more reliable fallback)
            PerformanceCounter? gpuEngineCounter = null;
            if (!_processGpuEngineCounters.TryGetValue(process.Id, out gpuEngineCounter) || gpuEngineCounter == null)
            {
                // Try to find GPU Engine 3D counter for this process
                try
                {
                    // Build instance name: pid_<pid>_luid_*_engtype_3d
                    // We need to find the actual instance name dynamically
                    var category = new PerformanceCounterCategory("GPU Engine");
                    var instances = category.GetInstanceNames();
                    var processInstance = instances.FirstOrDefault(inst => 
                        inst.Contains($"pid_{process.Id}_") && inst.Contains("engtype_3d"));
                    
                    if (!string.IsNullOrEmpty(processInstance))
                    {
                        gpuEngineCounter = new PerformanceCounter("GPU Engine", "Running Time", processInstance, true);
                        gpuEngineCounter.NextValue(); // First call returns 0
                        _processGpuEngineCounters[process.Id] = gpuEngineCounter;
                    }
                    else
                    {
                        _processGpuEngineCounters[process.Id] = null;
                    }
                }
                catch
                {
                    _processGpuEngineCounters[process.Id] = null;
                }
            }

            if (gpuEngineCounter != null)
            {
                try
                {
                    if (!QueryPerformanceCounter(out long gpuCounter))
                    {
                        return fpsData.LastFPS > 0 ? fpsData.LastFPS : 0;
                    }

                    double gpuTime = (double)gpuCounter / _systemFpsFrequency;
                    double runningTimeMs = gpuEngineCounter.NextValue(); // Cumulative running time in ms
                    
                    // Initialize on first call
                    if (fpsData.LastGpuRunningTime == 0)
                    {
                        fpsData.LastGpuRunningTime = runningTimeMs;
                        fpsData.LastTime = gpuTime;
                        return 0;
                    }

                    double gpuDeltaTime = gpuTime - fpsData.LastTime;
                    
                    if (gpuDeltaTime > 0 && gpuDeltaTime <= 1.0) // Valid delta (0-1 second)
                    {
                        double deltaRunningTime = runningTimeMs - fpsData.LastGpuRunningTime; // ms of GPU time used
                        
                        // Calculate FPS based on GPU utilization
                        // If GPU is fully utilized (deltaRunningTime ≈ gpuDeltaTime * 1000), FPS ≈ refresh rate
                        // If GPU is partially utilized, estimate FPS proportionally
                        if (deltaRunningTime > 0)
                        {
                            double gpuUtilization = deltaRunningTime / (gpuDeltaTime * 1000.0); // 0.0 to 1.0+
                            
                            // Estimate FPS: if GPU is being used, frames are being rendered
                            // Scale based on GPU utilization and refresh rate
                            if (gpuUtilization > 0.01) // At least 1% GPU usage
                            {
                                // Estimate FPS: GPU utilization * refresh rate (with some scaling)
                                // For a game running at refresh rate, GPU should be ~100% utilized
                                double estimatedFPS = Math.Min(
                                    _systemRefreshRate > 0 ? _systemRefreshRate * 1.5 : 120.0, // Cap at 150% of refresh rate
                                    Math.Max(1.0, gpuUtilization * _systemRefreshRate * 1.2) // Scale by GPU utilization
                                );
                                
                                currentFPS = estimatedFPS;
                            }
                        }
                        
                        fpsData.LastGpuRunningTime = runningTimeMs;
                        fpsData.LastTime = gpuTime;
                    }
                    else if (gpuDeltaTime > 1.0)
                    {
                        // Reset if delta is too large
                        fpsData.LastGpuRunningTime = runningTimeMs;
                        fpsData.LastTime = gpuTime;
                        return fpsData.LastFPS > 0 ? fpsData.LastFPS : 0;
                    }

                    if (currentFPS > 0)
                    {
                        fpsData.FPSQueue.Enqueue(currentFPS);
                        if (fpsData.FPSQueue.Count > smoothingWindow)
                        {
                            fpsData.FPSQueue.Dequeue();
                        }
                        double averageFPS = fpsData.FPSQueue.Average();
                        fpsData.LastFPS = averageFPS;
                        return averageFPS;
                    }
                }
                catch
                {
                    // GPU Engine counter failed, fall through to CPU time method
                }
            }

            // Final fallback: Use CPU time measurement (less accurate)
            // This is only used if GPU counters aren't available
            if (!QueryPerformanceCounter(out long cpuCounter))
            {
                return fpsData.LastFPS > 0 ? fpsData.LastFPS : 0;
            }

            double cpuTime = (double)cpuCounter / _systemFpsFrequency;
            
            // Initialize on first call
            if (fpsData.LastTime == 0)
            {
                fpsData.LastTime = cpuTime;
                TimeSpan cpuTimeSpan = process.TotalProcessorTime;
                fpsData.LastCpuTime = cpuTimeSpan.TotalSeconds;
                return 0;
            }

            double cpuDeltaTime = cpuTime - fpsData.LastTime;

            if (cpuDeltaTime <= 0 || cpuDeltaTime > 1.0) // Skip if delta is invalid or too large (>1 second)
            {
                fpsData.LastTime = cpuTime;
                return fpsData.LastFPS > 0 ? fpsData.LastFPS : 0;
            }

            // Use process CPU time to detect activity
            TimeSpan cpuTimeSpan2 = process.TotalProcessorTime;
            double cpuTimeSeconds = cpuTimeSpan2.TotalSeconds;
            double deltaCpuTime = cpuTimeSeconds - fpsData.LastCpuTime;

            // Very rough estimate: assume active process is rendering at refresh rate
            // This is a last resort fallback
            if (deltaCpuTime > 0 && cpuDeltaTime > 0)
            {
                double cpuUtilization = deltaCpuTime / cpuDeltaTime;
                
                // If CPU is being used significantly, assume rendering at refresh rate
                if (cpuUtilization > 0.05) // At least 5% CPU usage
                {
                    // Assume rendering at refresh rate (this is a rough estimate)
                    currentFPS = _systemRefreshRate > 0 ? _systemRefreshRate : 60.0;
                }
                else
                {
                    // Low CPU usage, might be idle or GPU-bound
                    currentFPS = 0;
                }
            }
            else
            {
                currentFPS = 0;
            }

            if (currentFPS > 0)
            {
                fpsData.FPSQueue.Enqueue(currentFPS);
                if (fpsData.FPSQueue.Count > smoothingWindow)
                {
                    fpsData.FPSQueue.Dequeue();
                }
                double averageFPS = fpsData.FPSQueue.Average();
                fpsData.LastTime = cpuTime;
                fpsData.LastCpuTime = cpuTimeSeconds;
                fpsData.LastFPS = averageFPS;
                return averageFPS;
            }

            fpsData.LastTime = cpuTime;
            fpsData.LastCpuTime = cpuTimeSeconds;
            return fpsData.LastFPS > 0 ? fpsData.LastFPS : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get system-wide FPS using display refresh rate
    /// </summary>
    public static double GetSystemFPS(int smoothingWindow = 10)
    {
        try
        {
            // Initialize refresh rate if not done yet
            if (!_systemRefreshRateInitialized)
            {
                InitializeSystemRefreshRate();
            }

            // Return the display refresh rate (this is what "system FPS" typically means)
            if (_systemRefreshRate > 0)
            {
                return _systemRefreshRate;
            }

            // Fallback to 60 Hz if we couldn't detect it
            return 60.0;
        }
        catch
        {
            return 60.0;
        }
    }

    /// <summary>
    /// Clean up FPS data for a process that no longer exists
    /// </summary>
    public static void CleanupProcessData(int processId)
    {
        _processFpsData.Remove(processId);
        
        // Dispose Direct3D performance counter if it exists
        if (_processPerformanceCounters.TryGetValue(processId, out var counter) && counter != null)
        {
            try
            {
                counter.Dispose();
            }
            catch { }
            _processPerformanceCounters.Remove(processId);
        }
        
        // Dispose GPU Engine counter if it exists
        if (_processGpuEngineCounters.TryGetValue(processId, out var gpuCounter) && gpuCounter != null)
        {
            try
            {
                gpuCounter.Dispose();
            }
            catch { }
            _processGpuEngineCounters.Remove(processId);
        }
    }

    /// <summary>
    /// Clean up all process data
    /// </summary>
    public static void CleanupAll()
    {
        // Dispose all Direct3D performance counters
        foreach (var counter in _processPerformanceCounters.Values)
        {
            try
            {
                counter?.Dispose();
            }
            catch { }
        }
        
        // Dispose all GPU Engine counters
        foreach (var counter in _processGpuEngineCounters.Values)
        {
            try
            {
                counter?.Dispose();
            }
            catch { }
        }
        
        _processFpsData.Clear();
        _processPerformanceCounters.Clear();
        _processGpuEngineCounters.Clear();
    }

    private class ProcessFPSData
    {
        public Queue<double> FPSQueue { get; } = new();
        public double LastTime { get; set; }
        public double LastCpuTime { get; set; }
        public double LastGpuRunningTime { get; set; }
        public double LastFPS { get; set; }
    }
}
