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

    protected override void OnPreLoad()
    {
        // OSC Parameters
        RegisterParameter<int>(DesktopFPSParameter.FPS, "Info/FPS", ParameterMode.Write, "FPS", "Current VRChat FPS");
    }

    protected override void OnPostLoad()
    {
        // Variables
        CreateVariable<int>(DesktopFPSVariable.FPS, "FPS");
        // Initialize to -1 (before first measurement)
        SetVariableValue(DesktopFPSVariable.FPS, -1);
    }

    protected override async Task<bool> OnModuleStart()
    {
        // Find VRChat process
        _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();

        // Say so up front rather than sitting at -1 forever. PresentMon is ETW-based, so
        // it cannot see a Proton-launched VRChat from inside this Wine prefix.
        if (!FPSMeasurementUtils.IsFpsMeasurementSupported)
        {
            Log(FPSMeasurementUtils.IsVRChatRunning()
                ? "VRChat is running on the host, but PresentMon (ETW) cannot measure it from inside Wine. "
                  + "Use the LinuxHardwareStats module instead — it publishes FPS to VRCOSC/VR/FPS/Value."
                : "FPS measurement is unavailable on Linux (PresentMon is ETW/Windows-only). "
                  + "Use the LinuxHardwareStats module instead.");
        }

        // Send initial parameter value now that OSC is connected
        SendParameter(DesktopFPSParameter.FPS, -1);

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
        if (!Bluscream.ModuleUtils.IsStarted()) return;
        try
        {
            if (_vrchatProcess == null || _vrchatProcess.HasExited)
            {
                _vrchatProcess = FPSMeasurementUtils.FindVRChatProcess();
                
                if (_vrchatProcess == null)
                {
                    // Set to -1 if process not found
                    SetVariableValue(DesktopFPSVariable.FPS, -1);
                    SendParameter(DesktopFPSParameter.FPS, -1);
                    return;
                }
            }

            if (_vrchatProcess != null && !_vrchatProcess.HasExited)
            {
                double vrchatFPS = FPSMeasurementUtils.GetProcessFPS(_vrchatProcess);
                
                if (vrchatFPS > 0)
                {
                    // Cap FPS between 0 and 1000, then convert to int
                    int fpsInt = (int)Math.Round(Math.Max(0, Math.Min(1000, vrchatFPS)));
                    
                    // Update variables and parameters
                    SetVariableValue(DesktopFPSVariable.FPS, fpsInt);
                    SendParameter(DesktopFPSParameter.FPS, fpsInt);
                }
                else
                {
                    // Set to -1 if measurement failed
                    SetVariableValue(DesktopFPSVariable.FPS, -1);
                    SendParameter(DesktopFPSParameter.FPS, -1);
                }
            }
            else
            {
                // Set to -1 if process has exited
                SetVariableValue(DesktopFPSVariable.FPS, -1);
                SendParameter(DesktopFPSParameter.FPS, -1);
            }
        }
        catch (Exception ex)
        {
            Log($"Error updating FPS: {ex.Message}");
            // Set to -1 on error
            SetVariableValue(DesktopFPSVariable.FPS, -1);
            SendParameter(DesktopFPSParameter.FPS, -1);
        }
    }

    // Public accessor methods for nodes
    public int GetFPS()
    {
        // GetVariableValue<T>(Enum) is protected in the SDK; ReflectionUtils owns that call.
        return ReflectionUtils.GetModuleVariableValue(this, DesktopFPSVariable.FPS, -1);
    }

    public bool IsVRChatRunning()
    {
        return _vrchatProcess != null && !_vrchatProcess.HasExited;
    }
}
