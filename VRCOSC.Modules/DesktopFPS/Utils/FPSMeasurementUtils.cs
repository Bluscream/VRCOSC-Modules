// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Bluscream.Modules.DesktopFPS.Utils;

/// <summary>
/// Utilities for measuring FPS using Windows Performance Counters
/// </summary>
public static class FPSMeasurementUtils
{
    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

    [DllImport("kernel32.dll")]
    private static extern bool QueryPerformanceFrequency(out long lpFrequency);

    private static readonly Dictionary<int, ProcessFPSData> _processFpsData = new();
    private static long _systemFpsLastCounter = 0;
    private static long _systemFpsFrequency = 0;
    private static double _systemFpsLastTime = 0;

    static FPSMeasurementUtils()
    {
        if (QueryPerformanceFrequency(out _systemFpsFrequency))
        {
            QueryPerformanceCounter(out _systemFpsLastCounter);
            _systemFpsLastTime = (double)_systemFpsLastCounter / _systemFpsFrequency;
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
    /// Get FPS for a specific process using high-precision timing
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

            // Use QueryPerformanceCounter for high-precision timing
            if (!QueryPerformanceCounter(out long currentCounter))
            {
                return 0;
            }

            double currentTime = (double)currentCounter / _systemFpsFrequency;
            double deltaTime = currentTime - fpsData.LastTime;

            if (deltaTime <= 0)
            {
                return fpsData.LastFPS;
            }

            double currentFPS = 1.0 / deltaTime;

            // Add to smoothing queue
            fpsData.FPSQueue.Enqueue(currentFPS);
            if (fpsData.FPSQueue.Count > smoothingWindow)
            {
                fpsData.FPSQueue.Dequeue();
            }

            // Calculate average
            double averageFPS = fpsData.FPSQueue.Average();

            fpsData.LastTime = currentTime;
            fpsData.LastFPS = averageFPS;

            return averageFPS;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Get system-wide FPS using high-precision timing
    /// </summary>
    public static double GetSystemFPS(int smoothingWindow = 10)
    {
        if (_systemFpsFrequency == 0)
        {
            return 0;
        }

        try
        {
            if (!QueryPerformanceCounter(out long currentCounter))
            {
                return 0;
            }

            double currentTime = (double)currentCounter / _systemFpsFrequency;
            
            // Initialize on first call
            if (_systemFpsLastTime == 0)
            {
                _systemFpsLastTime = currentTime;
                return 0;
            }
            
            double deltaTime = currentTime - _systemFpsLastTime;

            if (deltaTime <= 0)
            {
                return 0;
            }

            double currentFPS = 1.0 / deltaTime;

            // Use a static queue for system FPS smoothing
            if (!_systemFpsData.TryGetValue(0, out var fpsData))
            {
                fpsData = new ProcessFPSData();
                _systemFpsData[0] = fpsData;
            }

            fpsData.FPSQueue.Enqueue(currentFPS);
            if (fpsData.FPSQueue.Count > smoothingWindow)
            {
                fpsData.FPSQueue.Dequeue();
            }

            double averageFPS = fpsData.FPSQueue.Average();

            _systemFpsLastTime = currentTime;
            _systemFpsLastCounter = currentCounter;

            return averageFPS;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Clean up FPS data for a process that no longer exists
    /// </summary>
    public static void CleanupProcessData(int processId)
    {
        _processFpsData.Remove(processId);
    }

    /// <summary>
    /// Clean up all process data
    /// </summary>
    public static void CleanupAll()
    {
        _processFpsData.Clear();
        _systemFpsData.Clear();
    }

    private static readonly Dictionary<int, ProcessFPSData> _systemFpsData = new();

    private class ProcessFPSData
    {
        public Queue<double> FPSQueue { get; } = new();
        public double LastTime { get; set; }
        public double LastFPS { get; set; }
    }
}
