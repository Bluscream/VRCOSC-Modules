// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using VRCOSC.App.SDK.Parameters;
using Bluscream.Modules.DesktopFPS.Utils;

namespace Bluscream.Modules;

[ModuleTitle("Desktop FPS")]
[ModuleDescription("Monitors VRChat and system FPS using Windows Performance Counters")]
[ModuleType(ModuleType.Generic)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class DesktopFPSModule : Module
{
    private Process? _vrchatProcess;
    private double _lastVRChatFPS = 0;
    private double _lastSystemFPS = 0;
    private readonly List<double> _vrchatFpsHistory = new();
    private readonly List<double> _systemFpsHistory = new();
    private double _minVRChatFPS = double.MaxValue;
    private double _maxVRChatFPS = 0;
    private double _minSystemFPS = double.MaxValue;
    private double _maxSystemFPS = 0;
    private bool _vrchatProcessFound = false;

    protected override void OnPreLoad()
    {
        // Settings
        CreateTextBox(DesktopFPSSetting.UpdateInterval, "Update Interval (ms)", "How often to update FPS measurements", 100);
        CreateTextBox(DesktopFPSSetting.SmoothingWindow, "Smoothing Window", "Number of frames to average for smoothing", 10);
        CreateToggle(DesktopFPSSetting.TrackVRChatFPS, "Track VRChat FPS", "Monitor VRChat process FPS", true);
        CreateToggle(DesktopFPSSetting.TrackSystemFPS, "Track System FPS", "Monitor system-wide FPS", true);
        CreateToggle(DesktopFPSSetting.LogFPS, "Log FPS", "Log FPS values to console", false);

        // OSC Parameters
        RegisterParameter<float>(DesktopFPSParameter.VRChatFPS, "VRCOSC/DesktopFPS/VRChatFPS", ParameterMode.Write, "VRChat FPS", "Current VRChat FPS");
        RegisterParameter<float>(DesktopFPSParameter.SystemFPS, "VRCOSC/DesktopFPS/SystemFPS", ParameterMode.Write, "System FPS", "Current system FPS");
        RegisterParameter<float>(DesktopFPSParameter.AverageVRChatFPS, "VRCOSC/DesktopFPS/AverageVRChatFPS", ParameterMode.Write, "Average VRChat FPS", "Average VRChat FPS");
        RegisterParameter<float>(DesktopFPSParameter.AverageSystemFPS, "VRCOSC/DesktopFPS/AverageSystemFPS", ParameterMode.Write, "Average System FPS", "Average system FPS");
        RegisterParameter<float>(DesktopFPSParameter.MinVRChatFPS, "VRCOSC/DesktopFPS/MinVRChatFPS", ParameterMode.Write, "Min VRChat FPS", "Minimum VRChat FPS recorded");
        RegisterParameter<float>(DesktopFPSParameter.MaxVRChatFPS, "VRCOSC/DesktopFPS/MaxVRChatFPS", ParameterMode.Write, "Max VRChat FPS", "Maximum VRChat FPS recorded");

        // Groups
        CreateGroup("Monitoring", "FPS monitoring settings", DesktopFPSSetting.UpdateInterval, DesktopFPSSetting.SmoothingWindow);
        CreateGroup("Tracking", "What to track", DesktopFPSSetting.TrackVRChatFPS, DesktopFPSSetting.TrackSystemFPS);
        CreateGroup("Debug", "Debug settings", DesktopFPSSetting.LogFPS);
    }

    protected override void OnPostLoad()
    {
        // Variables
        CreateVariable<float>(DesktopFPSVariable.VRChatFPS, "VRChat FPS");
        CreateVariable<float>(DesktopFPSVariable.SystemFPS, "System FPS");
        CreateVariable<float>(DesktopFPSVariable.AverageVRChatFPS, "Average VRChat FPS");
        CreateVariable<float>(DesktopFPSVariable.AverageSystemFPS, "Average System FPS");
        CreateVariable<float>(DesktopFPSVariable.MinVRChatFPS, "Min VRChat FPS");
        CreateVariable<float>(DesktopFPSVariable.MaxVRChatFPS, "Max VRChat FPS");
        CreateVariable<bool>(DesktopFPSVariable.VRChatProcessFound, "VRChat Process Found");

        // States
        CreateState(DesktopFPSState.Monitoring, "Monitoring", "Desktop FPS: Monitoring");
        CreateState(DesktopFPSState.VRChatNotFound, "VRChat Not Found", "Desktop FPS: VRChat not found");
        CreateState(DesktopFPSState.Error, "Error", "Desktop FPS: Error");

        // Events
        CreateEvent(DesktopFPSEvent.OnVRChatFPSChanged, "On VRChat FPS Changed");
        CreateEvent(DesktopFPSEvent.OnSystemFPSChanged, "On System FPS Changed");
        CreateEvent(DesktopFPSEvent.OnVRChatProcessFound, "On VRChat Process Found");
        CreateEvent(DesktopFPSEvent.OnVRChatProcessLost, "On VRChat Process Lost");

        ChangeState(DesktopFPSState.Monitoring);
    }

    protected override async Task<bool> OnModuleStart()
    {
        // Find VRChat process
        _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
        _vrchatProcessFound = _vrchatProcess != null;

        SetVariableValue(DesktopFPSVariable.VRChatProcessFound, _vrchatProcessFound);

        if (_vrchatProcessFound)
        {
            TriggerEvent(DesktopFPSEvent.OnVRChatProcessFound);
            ChangeState(DesktopFPSState.Monitoring);
        }
        else
        {
            ChangeState(DesktopFPSState.VRChatNotFound);
        }

        // Reset min/max values
        _minVRChatFPS = double.MaxValue;
        _maxVRChatFPS = 0;
        _minSystemFPS = double.MaxValue;
        _maxSystemFPS = 0;
        _vrchatFpsHistory.Clear();
        _systemFpsHistory.Clear();

        return true;
    }

    protected override Task OnModuleStop()
    {
        // Cleanup
        if (_vrchatProcess != null)
        {
            try
            {
                _vrchatProcess.Dispose();
            }
            catch { }
            _vrchatProcess = null;
        }

        FPSMeasurementUtils.CleanupAll();

        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 100)]
    private void updateFPS()
    {
        try
        {
            var updateInterval = GetSettingValue<int>(DesktopFPSSetting.UpdateInterval);
            var smoothingWindow = GetSettingValue<int>(DesktopFPSSetting.SmoothingWindow);
            var trackVRChat = GetSettingValue<bool>(DesktopFPSSetting.TrackVRChatFPS);
            var trackSystem = GetSettingValue<bool>(DesktopFPSSetting.TrackSystemFPS);
            var logFPS = GetSettingValue<bool>(DesktopFPSSetting.LogFPS);

            // Check if VRChat process still exists
            if (trackVRChat)
            {
                if (_vrchatProcess == null || _vrchatProcess.HasExited)
                {
                    _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
                    
                    if (_vrchatProcess == null)
                    {
                        if (_vrchatProcessFound)
                        {
                            _vrchatProcessFound = false;
                            SetVariableValue(DesktopFPSVariable.VRChatProcessFound, false);
                            TriggerEvent(DesktopFPSEvent.OnVRChatProcessLost);
                            ChangeState(DesktopFPSState.VRChatNotFound);
                        }
                    }
                    else
                    {
                        if (!_vrchatProcessFound)
                        {
                            _vrchatProcessFound = true;
                            SetVariableValue(DesktopFPSVariable.VRChatProcessFound, true);
                            TriggerEvent(DesktopFPSEvent.OnVRChatProcessFound);
                            ChangeState(DesktopFPSState.Monitoring);
                        }
                    }
                }

                if (_vrchatProcess != null && !_vrchatProcess.HasExited)
                {
                    double vrchatFPS = FPSMeasurementUtils.GetProcessFPS(_vrchatProcess, smoothingWindow);
                    
                    if (vrchatFPS > 0)
                    {
                        // Update history for average calculation
                        _vrchatFpsHistory.Add(vrchatFPS);
                        if (_vrchatFpsHistory.Count > 100) // Keep last 100 samples
                        {
                            _vrchatFpsHistory.RemoveAt(0);
                        }

                        // Update min/max
                        if (vrchatFPS < _minVRChatFPS) _minVRChatFPS = vrchatFPS;
                        if (vrchatFPS > _maxVRChatFPS) _maxVRChatFPS = vrchatFPS;

                        // Calculate average
                        double averageVRChatFPS = _vrchatFpsHistory.Count > 0 
                            ? _vrchatFpsHistory.Average() 
                            : vrchatFPS;

                        // Update variables and parameters
                        SetVariableValue(DesktopFPSVariable.VRChatFPS, (float)vrchatFPS);
                        SetVariableValue(DesktopFPSVariable.AverageVRChatFPS, (float)averageVRChatFPS);
                        SetVariableValue(DesktopFPSVariable.MinVRChatFPS, (float)_minVRChatFPS);
                        SetVariableValue(DesktopFPSVariable.MaxVRChatFPS, (float)_maxVRChatFPS);

                        SendParameter(DesktopFPSParameter.VRChatFPS, (float)vrchatFPS);
                        SendParameter(DesktopFPSParameter.AverageVRChatFPS, (float)averageVRChatFPS);
                        SendParameter(DesktopFPSParameter.MinVRChatFPS, (float)_minVRChatFPS);
                        SendParameter(DesktopFPSParameter.MaxVRChatFPS, (float)_maxVRChatFPS);

                        // Trigger event on significant change (threshold: 5 FPS)
                        if (Math.Abs(vrchatFPS - _lastVRChatFPS) >= 5.0)
                        {
                            TriggerEvent(DesktopFPSEvent.OnVRChatFPSChanged);
                            _lastVRChatFPS = vrchatFPS;
                        }

                        if (logFPS)
                        {
                            Log($"VRChat FPS: {vrchatFPS:F2} (Avg: {averageVRChatFPS:F2}, Min: {_minVRChatFPS:F2}, Max: {_maxVRChatFPS:F2})");
                        }
                    }
                }
            }

            // System FPS
            if (trackSystem)
            {
                double systemFPS = FPSMeasurementUtils.GetSystemFPS(smoothingWindow);
                
                if (systemFPS > 0)
                {
                    // Update history for average calculation
                    _systemFpsHistory.Add(systemFPS);
                    if (_systemFpsHistory.Count > 100) // Keep last 100 samples
                    {
                        _systemFpsHistory.RemoveAt(0);
                    }

                    // Update min/max
                    if (systemFPS < _minSystemFPS) _minSystemFPS = systemFPS;
                    if (systemFPS > _maxSystemFPS) _maxSystemFPS = systemFPS;

                    // Calculate average
                    double averageSystemFPS = _systemFpsHistory.Count > 0 
                        ? _systemFpsHistory.Average() 
                        : systemFPS;

                    // Update variables and parameters
                    SetVariableValue(DesktopFPSVariable.SystemFPS, (float)systemFPS);
                    SetVariableValue(DesktopFPSVariable.AverageSystemFPS, (float)averageSystemFPS);

                    SendParameter(DesktopFPSParameter.SystemFPS, (float)systemFPS);
                    SendParameter(DesktopFPSParameter.AverageSystemFPS, (float)averageSystemFPS);

                    // Trigger event on significant change (threshold: 5 FPS)
                    if (Math.Abs(systemFPS - _lastSystemFPS) >= 5.0)
                    {
                        TriggerEvent(DesktopFPSEvent.OnSystemFPSChanged);
                        _lastSystemFPS = systemFPS;
                    }

                    if (logFPS)
                    {
                        Log($"System FPS: {systemFPS:F2} (Avg: {averageSystemFPS:F2})");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error updating FPS: {ex.Message}");
            ChangeState(DesktopFPSState.Error);
        }
    }

    // Public accessor methods for nodes
    public float GetVRChatFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.VRChatFPS }) ?? 0f);
        }
        return 0f;
    }

    public float GetSystemFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.SystemFPS }) ?? 0f);
        }
        return 0f;
    }

    public float GetAverageVRChatFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.AverageVRChatFPS }) ?? 0f);
        }
        return 0f;
    }

    public float GetAverageSystemFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.AverageSystemFPS }) ?? 0f);
        }
        return 0f;
    }

    public float GetMinVRChatFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.MinVRChatFPS }) ?? 0f);
        }
        return 0f;
    }

    public float GetMaxVRChatFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.MaxVRChatFPS }) ?? 0f);
        }
        return 0f;
    }

    public bool IsVRChatRunning()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(bool));
            return (bool)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.VRChatProcessFound }) ?? false);
        }
        return false;
    }
}
