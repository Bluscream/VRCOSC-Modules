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
    }

    protected override async Task<bool> OnModuleStart()
    {
        // Find VRChat process
        _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
        _vrchatProcessFound = _vrchatProcess != null;

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

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void updateFPS()
    {
        try
        {
            if (_vrchatProcess == null || _vrchatProcess.HasExited)
            {
                _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
                
                if (_vrchatProcess == null)
                {
                    _vrchatProcessFound = false;
                }
                else
                {
                    _vrchatProcessFound = true;
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
                    _lastVRChatFPS = vrchatFPS;
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
        return _vrchatProcess != null && !_vrchatProcess.HasExited;
    }
}
