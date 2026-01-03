// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PresentMonFps;

namespace Bluscream.Modules.DesktopFPS.Utils;

/// <summary>
/// Utilities for measuring FPS using PresentMon ETW (Event Tracing for Windows)
/// </summary>
public static class FPSMeasurementUtils
{
    private static readonly Dictionary<int, FpsMeasurementSession> _activeSessions = new();
    private static readonly object _sessionsLock = new object();

    /// <summary>
    /// Finds the VRChat process by name
    /// </summary>
    public static Process? FindVRChatProcess()
    {
        try
        {
            var processes = Process.GetProcessesByName("VRChat");
            return processes.Length > 0 ? processes[0] : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the FPS for a process using PresentMon ETW
    /// </summary>
    public static double GetProcessFPS(Process process, int smoothingWindow = 10)
    {
        if (process == null || process.HasExited)
        {
            return 0;
        }

        try
        {
            int processId = process.Id;
            lock (_sessionsLock)
            {
                if (!_activeSessions.TryGetValue(processId, out var session))
                {
                    // Create new session
                    session = new FpsMeasurementSession(processId);
                    _activeSessions[processId] = session;
                }

                // Check if process has exited
                if (process.HasExited)
                {
                    CleanupProcessData(processId);
                    return 0;
                }

                // Get latest FPS value
                return session.GetLatestFps();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting FPS for process {process.Id}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Cleans up FPS measurement data for a specific process
    /// </summary>
    public static void CleanupProcessData(int processId)
    {
        lock (_sessionsLock)
        {
            if (_activeSessions.TryGetValue(processId, out var session))
            {
                session.Dispose();
                _activeSessions.Remove(processId);
            }
        }
    }

    /// <summary>
    /// Cleans up all FPS measurement data
    /// </summary>
    public static void CleanupAll()
    {
        lock (_sessionsLock)
        {
            foreach (var session in _activeSessions.Values)
            {
                session.Dispose();
            }
            _activeSessions.Clear();
        }

        // Stop any remaining trace sessions
        try
        {
            if (FpsInspector.IsAvailable)
            {
                FpsInspector.StopTraceSession();
            }
        }
        catch
        {
            // Ignore errors when stopping trace session
        }
    }

    /// <summary>
    /// Internal session class to manage ETW-based FPS measurement
    /// </summary>
    private class FpsMeasurementSession : IDisposable
    {
        private readonly int _processId;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _measurementTask;
        private double _latestFps = 0;
        private readonly object _fpsLock = new object();
        private bool _disposed = false;

        public FpsMeasurementSession(int processId)
        {
            _processId = processId;
            _cancellationTokenSource = new CancellationTokenSource();

            // Start continuous FPS measurement
            _measurementTask = Task.Run(async () =>
            {
                try
                {
                    var request = new FpsRequest((uint)processId)
                    {
                        PeriodMillisecond = 100 // Update every 100ms
                    };

                    await FpsInspector.StartForeverAsync(request, OnFpsReceived, _cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in FPS measurement session for process {processId}: {ex.Message}");
                }
            });
        }

        private void OnFpsReceived(FpsResult result)
        {
            lock (_fpsLock)
            {
                _latestFps = result.Fps;
            }
        }

        public double GetLatestFps()
        {
            lock (_fpsLock)
            {
                return _latestFps;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _cancellationTokenSource.Cancel();

            try
            {
                _measurementTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Ignore timeout errors
            }

            _cancellationTokenSource.Dispose();
        }
    }
}
