// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PresentMonFps;
using Bluscream.Modules.Utilities;

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
    /// True when VRChat is running, checking the Linux host as well as this prefix.
    /// </summary>
    /// <remarks>
    /// <see cref="FindVRChatProcess"/> only ever sees processes inside VRCOSC's OWN Wine
    /// prefix. On Linux, VRChat runs in a separate Proton prefix (Steam appid 438100), so
    /// that lookup always returned null and the module silently reported nothing.
    ///
    /// The host check matches the full command line, because a Proton-launched game
    /// appears as a wine loader process rather than a bare "VRChat" - the same reason
    /// LinuxHardwareStats' helper script uses `pgrep -f "VRChat.exe"`.
    /// </remarks>
    public static bool IsVRChatRunning()
        => FindVRChatProcess() is not null
           || (LinuxUtils.IsLinux && LinuxUtils.IsHostProcessRunning("VRChat.exe", matchFullCommandLine: true));

    /// <summary>
    /// Whether per-process FPS measurement can work on this platform.
    /// </summary>
    /// <remarks>
    /// PresentMon is ETW-based, i.e. Windows-kernel tracing. It cannot see a Proton game
    /// from inside a Wine prefix, so on Linux this returns false and callers should fall
    /// back to LinuxHardwareStats, which reads FPS from the host and publishes it to
    /// VRCOSC/VR/FPS/Value.
    /// </remarks>
    public static bool IsFpsMeasurementSupported => !LinuxUtils.IsLinux;

    /// <summary>
    /// Gets the FPS for a process using PresentMon ETW
    /// </summary>
    public static double GetProcessFPS(Process process)
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
