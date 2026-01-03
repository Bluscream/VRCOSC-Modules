// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using VRCOSC.App.SDK.Parameters;
using Bluscream.Modules.DesktopFPS.Utils;

namespace Bluscream.Modules;

[ModuleTitle("Desktop FPS")]
[ModuleDescription("Monitors VRChat FPS using Windows Performance Counters")]
[ModuleType(ModuleType.Generic)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class DesktopFPSModule : Module
{
    private Process? _vrchatProcess;
    private double _lastVRChatFPS = 0;
    private bool _vrchatProcessFound = false;

    protected override void OnPreLoad()
    {
        // OSC Parameters
        RegisterParameter<float>(DesktopFPSParameter.FPS, "VRCOSC/DesktopFPS/FPS", ParameterMode.Write, "FPS", "Current VRChat FPS");
    }

    protected override void OnPostLoad()
    {
        // Variables
        CreateVariable<float>(DesktopFPSVariable.FPS, "FPS");
        CreateVariable<bool>(DesktopFPSVariable.VRChatProcessFound, "VRChat Process Found");

        // Events
        CreateEvent(DesktopFPSEvent.OnFPSChanged, "On FPS Changed");
    }

    protected override async Task<bool> OnModuleStart()
    {
        // Find VRChat process
        _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
        _vrchatProcessFound = _vrchatProcess != null;

        SetVariableValue(DesktopFPSVariable.VRChatProcessFound, _vrchatProcessFound);

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
            // Check if VRChat process still exists
            if (_vrchatProcess == null || _vrchatProcess.HasExited)
            {
                _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
                
                if (_vrchatProcess == null)
                {
                    if (_vrchatProcessFound)
                    {
                        _vrchatProcessFound = false;
                        SetVariableValue(DesktopFPSVariable.VRChatProcessFound, false);
                    }
                }
                else
                {
                    if (!_vrchatProcessFound)
                    {
                        _vrchatProcessFound = true;
                        SetVariableValue(DesktopFPSVariable.VRChatProcessFound, true);
                    }
                }
            }

            if (_vrchatProcess != null && !_vrchatProcess.HasExited)
            {
                const int smoothingWindow = 10; // Default smoothing window
                double vrchatFPS = FPSMeasurementUtils.GetProcessFPS(_vrchatProcess, smoothingWindow);
                
                if (vrchatFPS > 0)
                {
                    // Update variables and parameters
                    SetVariableValue(DesktopFPSVariable.FPS, (float)vrchatFPS);
                    SendParameter(DesktopFPSParameter.FPS, (float)vrchatFPS);

                    // Trigger event on significant change (threshold: 5 FPS)
                    if (Math.Abs(vrchatFPS - _lastVRChatFPS) >= 5.0)
                    {
                        TriggerEvent(DesktopFPSEvent.OnFPSChanged);
                        _lastVRChatFPS = vrchatFPS;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error updating FPS: {ex.Message}");
        }
    }

    // Public accessor methods for nodes
    public float GetFPS()
    {
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(float));
            return (float)(genericMethod.Invoke(this, new object[] { DesktopFPSVariable.FPS }) ?? 0f);
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
