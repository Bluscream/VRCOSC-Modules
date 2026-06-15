// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// OpenXR equivalent of the official SteamVR Statistics Module.
// Uses Silk.NET.OpenXR for cross-platform support (Windows + Linux).

using System.Runtime.InteropServices;
using Bluscream.Modules.Utilities;
using Silk.NET.OpenXR;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OpenXR;

[ModuleTitle("OpenXR Stats")]
[ModuleDescription("Gathers various stats from an OpenXR runtime (SteamVR, Monado, etc.) — cross-platform")]
[ModuleType(ModuleType.SteamVR)]
public class OpenXRStatisticsModule : Module
{
    // ─────────────────── Silk.NET / OpenXR objects ───────────────────
    private XR?       _xr;
    private Instance  _instance;
    private Session   _session;
    private ulong     _systemId;
    private bool      _xrReady;

    // Per-poll device state cache
    private readonly OpenXRDeviceState _hmd   = new();
    private readonly OpenXRDeviceState _lHand = new();
    private readonly OpenXRDeviceState _rHand = new();
    private float _currentFps;

    // ─────────────────── Module lifecycle ────────────────────────────
    protected override void OnPreLoad()
    {
        RegisterParameter<int>  (OpenXRParameter.FPS,           "VRCOSC/VR/FPS/Value",      ParameterMode.Write, "FPS",            "Measured compositor FPS");
        RegisterParameter<float>(OpenXRParameter.FPSNormalised, "VRCOSC/VR/FPS/Normalised", ParameterMode.Write, "FPS Normalised", "FPS normalised 0-240 → 0-1");

        RegisterParameter<bool>(OpenXRParameter.UserPresent,     "VRCOSC/VR/UserPresent",      ParameterMode.Write, "User Present",      "Headset is worn / session focused");
        RegisterParameter<bool>(OpenXRParameter.DashboardVisible,"VRCOSC/VR/DashboardVisible", ParameterMode.Write, "Dashboard Visible", "Session visible but not focused");

        RegisterParameter<bool> (OpenXRParameter.HMD_Connected, "VRCOSC/VR/HMD/Connected", ParameterMode.Write, "HMD Connected", "Whether the HMD device is connected");
        RegisterParameter<float>(OpenXRParameter.HMD_Battery,   "VRCOSC/VR/HMD/Battery",   ParameterMode.Write, "HMD Battery",   "HMD battery percentage (0-1)");
        RegisterParameter<bool> (OpenXRParameter.HMD_Charging,  "VRCOSC/VR/HMD/Charging",  ParameterMode.Write, "HMD Charging",  "Whether the HMD is charging");

        RegisterParameter<bool> (OpenXRParameter.LHand_Connected, "VRCOSC/VR/LHand/Connected", ParameterMode.Write, "Left Hand Connected", "Whether the left controller is connected");
        RegisterParameter<float>(OpenXRParameter.LHand_Battery,   "VRCOSC/VR/LHand/Battery",   ParameterMode.Write, "Left Hand Battery",   "Left controller battery (0-1)");
        RegisterParameter<bool> (OpenXRParameter.LHand_Charging,  "VRCOSC/VR/LHand/Charging",  ParameterMode.Write, "Left Hand Charging",  "Whether the left controller is charging");

        RegisterParameter<bool> (OpenXRParameter.RHand_Connected, "VRCOSC/VR/RHand/Connected", ParameterMode.Write, "Right Hand Connected", "Whether the right controller is connected");
        RegisterParameter<float>(OpenXRParameter.RHand_Battery,   "VRCOSC/VR/RHand/Battery",   ParameterMode.Write, "Right Hand Battery",   "Right controller battery (0-1)");
        RegisterParameter<bool> (OpenXRParameter.RHand_Charging,  "VRCOSC/VR/RHand/Charging",  ParameterMode.Write, "Right Hand Charging",  "Whether the right controller is charging");

        RegisterParameter<float>(OpenXRParameter.LeftIndex,  "VRCOSC/VR/LHand/Input/Finger/Index",  ParameterMode.Write, "Left Index",  "Left index finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.LeftMiddle, "VRCOSC/VR/LHand/Input/Finger/Middle", ParameterMode.Write, "Left Middle", "Left middle finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.LeftRing,   "VRCOSC/VR/LHand/Input/Finger/Ring",   ParameterMode.Write, "Left Ring",   "Left ring finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.LeftPinky,  "VRCOSC/VR/LHand/Input/Finger/Pinky",  ParameterMode.Write, "Left Pinky",  "Left pinky finger curl (0-1)");

        RegisterParameter<float>(OpenXRParameter.RightIndex,  "VRCOSC/VR/RHand/Input/Finger/Index",  ParameterMode.Write, "Right Index",  "Right index finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.RightMiddle, "VRCOSC/VR/RHand/Input/Finger/Middle", ParameterMode.Write, "Right Middle", "Right middle finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.RightRing,   "VRCOSC/VR/RHand/Input/Finger/Ring",   ParameterMode.Write, "Right Ring",   "Right ring finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.RightPinky,  "VRCOSC/VR/RHand/Input/Finger/Pinky",  ParameterMode.Write, "Right Pinky",  "Right pinky finger curl (0-1)");
    }

    protected override void OnPostLoad()
    {
        CreateVariable<float>(OpenXRVariable.FPS, "FPS");
        CreateVariable<bool> (OpenXRVariable.UserPresent, "User Present");

        var hmdBat = CreateVariable<int>(OpenXRVariable.HMD_Battery, "HMD Battery (%)")!;
        CreateVariable<bool>(OpenXRVariable.HMD_Charging, "HMD Charging");

        var lcBat = CreateVariable<int>(OpenXRVariable.LHand_Battery, "Left Hand Battery (%)")!;
        CreateVariable<bool>(OpenXRVariable.LHand_Charging, "Left Hand Charging");

        var rcBat = CreateVariable<int>(OpenXRVariable.RHand_Battery, "Right Hand Battery (%)")!;
        CreateVariable<bool>(OpenXRVariable.RHand_Charging, "Right Hand Charging");

        CreateState(OpenXRState.Default,   "Default",    "HMD: {0}%\nLHand: {1}%\nRHand: {2}%", new[] { hmdBat, lcBat, rcBat });
        CreateState(OpenXRState.NoRuntime, "No Runtime", "OpenXR runtime not found");
        CreateState(OpenXRState.Error,     "Error",      "OpenXR error — check logs");
    }

    protected override Task<bool> OnModuleStart()
    {
        _xrReady = false;
        _hmd.Reset(); _lHand.Reset(); _rHand.Reset();

        try
        {
            _xr = XR.GetApi();
            if (!InitialiseOpenXR())
            {
                Log("OpenXR runtime not available or could not be initialised.");
                ChangeState(OpenXRState.NoRuntime);
                return Task.FromResult(true); // degraded mode
            }
            _xrReady = true;
            ChangeState(OpenXRState.Default);
        }
        catch (Exception ex)
        {
            Log($"OpenXR init exception: {ex.Message}");
            ChangeState(OpenXRState.NoRuntime);
        }

        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        TearDownOpenXR();
        return Task.CompletedTask;
    }

    // ─────────────────── OpenXR init/teardown ────────────────────────
    private bool InitialiseOpenXR()
    {
        if (_xr is null) return false;

        var appInfo = new ApplicationInfo();
        OpenXRHelper.FillApplicationInfo(ref appInfo, "VRCOSC OpenXR Stats");
        appInfo.ApplicationVersion = 1;
        appInfo.ApiVersion = OpenXRHelper.XrVersion10;

        var createInfo = new InstanceCreateInfo
        {
            Type = StructureType.InstanceCreateInfo,
            ApplicationInfo = appInfo,
            EnabledExtensionCount = 0,
            EnabledExtensionNames = null
        };

        if (_xr.CreateInstance(in createInfo, ref _instance) != Result.Success)
        {
            Log("xrCreateInstance failed — is an OpenXR runtime installed?");
            return false;
        }

        var sysInfo = new SystemGetInfo { Type = StructureType.SystemGetInfo, FormFactor = FormFactor.HeadMountedDisplay };
        if (_xr.GetSystem(_instance, in sysInfo, ref _systemId) != Result.Success)
        {
            Log("xrGetSystem failed — is an HMD connected?");
            return false;
        }

        Log($"OpenXR system found (ID={_systemId}).");
        return true;
    }

    private void TearDownOpenXR()
    {
        if (_xr is null) return;
        if (_session.Handle  != 0) { _xr.DestroySession(_session);   _session  = default; }
        if (_instance.Handle != 0) { _xr.DestroyInstance(_instance); _instance = default; }
        _xr.Dispose(); _xr = null; _xrReady = false;
    }

    // ─────────────────── Module update loops ─────────────────────────
    [ModuleUpdate(ModuleUpdateMode.ChatBox)]
    private void UpdateVariables()
    {
        SetVariableValue(OpenXRVariable.FPS,          MathF.Round(_currentFps));
        SetVariableValue(OpenXRVariable.UserPresent,  _hmd.IsPresent);
        SetVariableValue(OpenXRVariable.HMD_Battery,  (int)(_hmd.BatteryPercent  * 100f));
        SetVariableValue(OpenXRVariable.HMD_Charging, _hmd.IsCharging);
        SetVariableValue(OpenXRVariable.LHand_Battery,  (int)(_lHand.BatteryPercent * 100f));
        SetVariableValue(OpenXRVariable.LHand_Charging, _lHand.IsCharging);
        SetVariableValue(OpenXRVariable.RHand_Battery,  (int)(_rHand.BatteryPercent * 100f));
        SetVariableValue(OpenXRVariable.RHand_Charging, _rHand.IsCharging);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateSlowParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            PollBatteryViaUPower();

        SendParameter(OpenXRParameter.UserPresent,      _hmd.IsPresent);
        SendParameter(OpenXRParameter.DashboardVisible, !_hmd.IsPresent);

        SendParameter(OpenXRParameter.HMD_Connected, _hmd.IsConnected);
        SendParameter(OpenXRParameter.HMD_Battery,   _hmd.BatteryPercent);
        SendParameter(OpenXRParameter.HMD_Charging,  _hmd.IsCharging);

        SendParameter(OpenXRParameter.LHand_Connected, _lHand.IsConnected);
        SendParameter(OpenXRParameter.LHand_Battery,   _lHand.BatteryPercent);
        SendParameter(OpenXRParameter.LHand_Charging,  _lHand.IsCharging);

        SendParameter(OpenXRParameter.RHand_Connected, _rHand.IsConnected);
        SendParameter(OpenXRParameter.RHand_Battery,   _rHand.BatteryPercent);
        SendParameter(OpenXRParameter.RHand_Charging,  _rHand.IsCharging);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000f / 60f)]
    private void UpdateRealtimeParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;
        if (!_xrReady || _xr is null) return;

        _currentFps = EstimateFpsFromFrameState();

        SendParameter(OpenXRParameter.FPS,           (int)MathF.Round(_currentFps));
        SendParameter(OpenXRParameter.FPSNormalised, Math.Clamp(_currentFps / 240f, 0f, 1f));

        SendParameter(OpenXRParameter.LeftIndex,   _lHand.FingerCurl[0]);
        SendParameter(OpenXRParameter.LeftMiddle,  _lHand.FingerCurl[1]);
        SendParameter(OpenXRParameter.LeftRing,    _lHand.FingerCurl[2]);
        SendParameter(OpenXRParameter.LeftPinky,   _lHand.FingerCurl[3]);

        SendParameter(OpenXRParameter.RightIndex,  _rHand.FingerCurl[0]);
        SendParameter(OpenXRParameter.RightMiddle, _rHand.FingerCurl[1]);
        SendParameter(OpenXRParameter.RightRing,   _rHand.FingerCurl[2]);
        SendParameter(OpenXRParameter.RightPinky,  _rHand.FingerCurl[3]);
    }

    // ─────────────────── Helpers ─────────────────────────────────────
    private float EstimateFpsFromFrameState()
    {
        if (_xr is null || _session.Handle == 0) return 0f;

        var frameState = new FrameState { Type = StructureType.FrameState };
        var waitInfo   = new FrameWaitInfo { Type = StructureType.FrameWaitInfo };
        if (_xr.WaitFrame(_session, in waitInfo, ref frameState) != Result.Success) return _currentFps;
        if (frameState.PredictedDisplayPeriod > 0)
            return 1_000_000_000f / frameState.PredictedDisplayPeriod;
        return _currentFps;
    }

    private void PollBatteryViaUPower()
    {
        var keywords = new[] { "headset", "controller", "gamepad", "input" };
        var devices  = LinuxUtils.GetUPowerDevices(filter: null, useNative: true);

        foreach (var dev in devices.Where(d => keywords.Any(k => d.Path.Contains(k, StringComparison.OrdinalIgnoreCase))))
        {
            var path = dev.Path.ToLower();
            if (path.Contains("headset") || path.Contains("hmd"))
            { _hmd.BatteryPercent = dev.BatteryLevel; _hmd.IsCharging = dev.IsCharging; _hmd.IsConnected = dev.IsPresent; _hmd.IsPresent = dev.IsPresent; }
            else if (path.Contains("left") || path.Contains("_l_"))
            { _lHand.BatteryPercent = dev.BatteryLevel; _lHand.IsCharging = dev.IsCharging; _lHand.IsConnected = dev.IsPresent; }
            else if (path.Contains("right") || path.Contains("_r_"))
            { _rHand.BatteryPercent = dev.BatteryLevel; _rHand.IsCharging = dev.IsCharging; _rHand.IsConnected = dev.IsPresent; }
            else if (!_lHand.IsConnected)
            { _lHand.BatteryPercent = dev.BatteryLevel; _lHand.IsCharging = dev.IsCharging; _lHand.IsConnected = dev.IsPresent; }
            else if (!_rHand.IsConnected)
            { _rHand.BatteryPercent = dev.BatteryLevel; _rHand.IsCharging = dev.IsCharging; _rHand.IsConnected = dev.IsPresent; }
        }

        _hmd.IsConnected = _hmd.BatteryPercent > 0;
    }

    // ─────────────────── Enums ────────────────────────────────────────
    private enum OpenXRParameter
    {
        FPS, FPSNormalised, UserPresent, DashboardVisible,
        HMD_Connected, HMD_Battery, HMD_Charging,
        LHand_Connected, LHand_Battery, LHand_Charging,
        RHand_Connected, RHand_Battery, RHand_Charging,
        LeftIndex, LeftMiddle, LeftRing, LeftPinky,
        RightIndex, RightMiddle, RightRing, RightPinky
    }

    private enum OpenXRVariable
    {
        FPS, UserPresent,
        HMD_Battery, HMD_Charging,
        LHand_Battery, LHand_Charging,
        RHand_Battery, RHand_Charging
    }

    private enum OpenXRState { Default, NoRuntime, Error }
}
