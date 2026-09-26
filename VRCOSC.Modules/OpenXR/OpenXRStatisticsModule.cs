// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// OpenXR equivalent of the official SteamVR Statistics Module, for runtimes VRCOSC's built-in
// OpenVR manager cannot reach (WiVRn, Monado). Same parameter names, so prefabs and ChatBox
// setups made for the official module keep working.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OpenXR;

[ModuleTitle("OpenXR Stats")]
[ModuleDescription("Gathers headset, controller, battery, FPS and finger stats from an OpenXR runtime (WiVRn, Monado, SteamVR)")]
[ModuleType(ModuleType.SteamVR)]
public class OpenXRStatisticsModule : Module
{
    private readonly OpenXRRuntime _runtime = OpenXRRuntime.Shared;
    private readonly OpenXRDeviceProbe _probe = OpenXRDeviceProbe.Shared;
    private bool _wasRunning;
    private DateTime _lastStuckReport = DateTime.MinValue;

    protected override void OnPreLoad()
    {
        Bluscream.ModuleUtils.RegisterNativeResolver(Log);

        CreateToggle(OpenXRSetting.OverlaySession, "Overlay session",
            "Ask the runtime for an overlay session so controller input stays readable while VRChat is focused. Turn off if the runtime refuses to start the session.", true);

        RegisterParameter<int>(OpenXRParameter.FPS, "VRCOSC/VR/FPS/Value", ParameterMode.Write, "FPS", "Display refresh rate of the headset (OpenXR has no per-app FPS)");
        RegisterParameter<float>(OpenXRParameter.FPSNormalised, "VRCOSC/VR/FPS/Normalised", ParameterMode.Write, "FPS Normalised", "Refresh rate normalised from 0-240 to 0-1");

        RegisterParameter<bool>(OpenXRParameter.UserPresent, "VRCOSC/VR/UserPresent", ParameterMode.Write, "User Present", "Whether the headset is being tracked (worn)");
        RegisterParameter<bool>(OpenXRParameter.DashboardVisible, "VRCOSC/VR/DashboardVisible", ParameterMode.Write, "Dashboard Visible", "Whether the main application is visible but not focused (system UI / dashboard open)");

        RegisterParameter<bool>(OpenXRParameter.HMD_Connected, "VRCOSC/VR/HMD/Connected", ParameterMode.Write, "HMD Connected", "Whether an OpenXR session with the headset is running");
        RegisterParameter<float>(OpenXRParameter.HMD_Battery, "VRCOSC/VR/HMD/Battery", ParameterMode.Write, "HMD Battery", "Headset battery normalised (0-1)");
        RegisterParameter<bool>(OpenXRParameter.HMD_Charging, "VRCOSC/VR/HMD/Charging", ParameterMode.Write, "HMD Charging", "Whether the headset is charging");

        RegisterParameter<bool>(OpenXRParameter.LHand_Connected, "VRCOSC/VR/LHand/Connected", ParameterMode.Write, "Left Hand Connected", "Whether the left controller or hand is tracked");
        RegisterParameter<float>(OpenXRParameter.LHand_Battery, "VRCOSC/VR/LHand/Battery", ParameterMode.Write, "Left Hand Battery", "Left controller battery normalised (0-1)");
        RegisterParameter<bool>(OpenXRParameter.LHand_Charging, "VRCOSC/VR/LHand/Charging", ParameterMode.Write, "Left Hand Charging", "Whether the left controller is charging");

        RegisterParameter<bool>(OpenXRParameter.LeftATouch, "VRCOSC/VR/LHand/Input/A/Touch", ParameterMode.Write, "Left Hand A Touch", "Whether the left primary button (A/X) is touched");
        RegisterParameter<bool>(OpenXRParameter.LeftBTouch, "VRCOSC/VR/LHand/Input/B/Touch", ParameterMode.Write, "Left Hand B Touch", "Whether the left secondary button (B/Y) is touched");
        RegisterParameter<bool>(OpenXRParameter.LeftPadTouch, "VRCOSC/VR/LHand/Input/Pad/Touch", ParameterMode.Write, "Left Hand Pad Touch", "Whether the left trackpad or thumbrest is touched");
        RegisterParameter<bool>(OpenXRParameter.LeftStickTouch, "VRCOSC/VR/LHand/Input/Stick/Touch", ParameterMode.Write, "Left Hand Stick Touch", "Whether the left thumbstick is touched");
        RegisterParameter<float>(OpenXRParameter.LeftIndex, "VRCOSC/VR/LHand/Input/Finger/Index", ParameterMode.Write, "Left Index", "Left index finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.LeftMiddle, "VRCOSC/VR/LHand/Input/Finger/Middle", ParameterMode.Write, "Left Middle", "Left middle finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.LeftRing, "VRCOSC/VR/LHand/Input/Finger/Ring", ParameterMode.Write, "Left Ring", "Left ring finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.LeftPinky, "VRCOSC/VR/LHand/Input/Finger/Pinky", ParameterMode.Write, "Left Pinky", "Left pinky finger curl (0-1)");

        RegisterParameter<bool>(OpenXRParameter.RHand_Connected, "VRCOSC/VR/RHand/Connected", ParameterMode.Write, "Right Hand Connected", "Whether the right controller or hand is tracked");
        RegisterParameter<float>(OpenXRParameter.RHand_Battery, "VRCOSC/VR/RHand/Battery", ParameterMode.Write, "Right Hand Battery", "Right controller battery normalised (0-1)");
        RegisterParameter<bool>(OpenXRParameter.RHand_Charging, "VRCOSC/VR/RHand/Charging", ParameterMode.Write, "Right Hand Charging", "Whether the right controller is charging");

        RegisterParameter<bool>(OpenXRParameter.RightATouch, "VRCOSC/VR/RHand/Input/A/Touch", ParameterMode.Write, "Right Hand A Touch", "Whether the right primary button (A) is touched");
        RegisterParameter<bool>(OpenXRParameter.RightBTouch, "VRCOSC/VR/RHand/Input/B/Touch", ParameterMode.Write, "Right Hand B Touch", "Whether the right secondary button (B) is touched");
        RegisterParameter<bool>(OpenXRParameter.RightPadTouch, "VRCOSC/VR/RHand/Input/Pad/Touch", ParameterMode.Write, "Right Hand Pad Touch", "Whether the right trackpad or thumbrest is touched");
        RegisterParameter<bool>(OpenXRParameter.RightStickTouch, "VRCOSC/VR/RHand/Input/Stick/Touch", ParameterMode.Write, "Right Hand Stick Touch", "Whether the right thumbstick is touched");
        RegisterParameter<float>(OpenXRParameter.RightIndex, "VRCOSC/VR/RHand/Input/Finger/Index", ParameterMode.Write, "Right Index", "Right index finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.RightMiddle, "VRCOSC/VR/RHand/Input/Finger/Middle", ParameterMode.Write, "Right Middle", "Right middle finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.RightRing, "VRCOSC/VR/RHand/Input/Finger/Ring", ParameterMode.Write, "Right Ring", "Right ring finger curl (0-1)");
        RegisterParameter<float>(OpenXRParameter.RightPinky, "VRCOSC/VR/RHand/Input/Finger/Pinky", ParameterMode.Write, "Right Pinky", "Right pinky finger curl (0-1)");
    }

    protected override void OnPostLoad()
    {
        CreateVariable<float>(OpenXRVariable.FPS, "FPS");
        CreateVariable<int>(OpenXRVariable.TargetHz, "Target Hz (headset refresh rate)");
        CreateVariable<bool>(OpenXRVariable.DashboardVisible, "Dashboard Visible");
        CreateVariable<string>(OpenXRVariable.RuntimeName, "Runtime Name");
        CreateVariable<string>(OpenXRVariable.SystemName, "System Name");
        CreateVariable<string>(OpenXRVariable.SessionState, "Session State");

        CreateVariable<bool>(OpenXRVariable.HMD_Charging, "HMD Charging");
        var hmdBattery = CreateVariable<int>(OpenXRVariable.HMD_Battery, "HMD Battery (%)")!;
        CreateVariable<bool>(OpenXRVariable.LHand_Charging, "Left Hand Charging");
        var leftBattery = CreateVariable<int>(OpenXRVariable.LHand_Battery, "Left Hand Battery (%)")!;
        CreateVariable<bool>(OpenXRVariable.RHand_Charging, "Right Hand Charging");
        var rightBattery = CreateVariable<int>(OpenXRVariable.RHand_Battery, "Right Hand Battery (%)")!;

        CreateState(OpenXRState.Default, "Default", "HMD: {0}\nLHand: {1}\nRHand: {2}", new[] { hmdBattery, leftBattery, rightBattery });
        CreateState(OpenXRState.NoRuntime, "No Runtime", "OpenXR runtime not available");
    }

    protected override Task<bool> OnModuleStart()
    {
        _runtime.UseOverlaySession = GetSettingValue<bool>(OpenXRSetting.OverlaySession);
        _runtime.Acquire(Log);
        _probe.Deploy(Log);
        _wasRunning = false;
        ChangeState(OpenXRState.NoRuntime);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _runtime.Release(Log);
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.ChatBox)]
    private void UpdateVariables()
    {
        var xr = _runtime.Snapshot;
        var probe = _probe.Latest;

        SetVariableValue(OpenXRVariable.FPS, MathF.Round(xr.DisplayRefreshRate));
        SetVariableValue(OpenXRVariable.TargetHz, (int)MathF.Round(xr.DisplayRefreshRate));
        SetVariableValue(OpenXRVariable.DashboardVisible, IsDashboardVisible(probe));
        SetVariableValue(OpenXRVariable.RuntimeName, xr.RuntimeName);
        SetVariableValue(OpenXRVariable.SystemName, xr.SystemName);
        SetVariableValue(OpenXRVariable.SessionState, xr.SessionState.ToString());

        SetVariableValue(OpenXRVariable.HMD_Battery, Percent(probe.Head));
        SetVariableValue(OpenXRVariable.HMD_Charging, probe.Head.Charging);
        SetVariableValue(OpenXRVariable.LHand_Battery, Percent(probe.Left));
        SetVariableValue(OpenXRVariable.LHand_Charging, probe.Left.Charging);
        SetVariableValue(OpenXRVariable.RHand_Battery, Percent(probe.Right));
        SetVariableValue(OpenXRVariable.RHand_Charging, probe.Right.Charging);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateMetadataParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;

        _probe.Poll(Log);
        var xr = _runtime.Snapshot;
        var probe = _probe.Latest;

        if (xr.SessionRunning && DateTime.UtcNow - _runtime.LastLoopUtc > TimeSpan.FromSeconds(5) && DateTime.UtcNow - _lastStuckReport > TimeSpan.FromSeconds(30))
        {
            _lastStuckReport = DateTime.UtcNow;
            Log($"OpenXR worker has not completed a loop for {(DateTime.UtcNow - _runtime.LastLoopUtc).TotalSeconds:0} s; stuck in '{_runtime.CurrentPhase}'.");
        }

        if (xr.SessionRunning != _wasRunning)
        {
            _wasRunning = xr.SessionRunning;
            ChangeState(_wasRunning ? OpenXRState.Default : OpenXRState.NoRuntime);
        }

        SendParameter(OpenXRParameter.UserPresent, xr.HeadTracked);
        SendParameter(OpenXRParameter.DashboardVisible, IsDashboardVisible(probe));

        SendParameter(OpenXRParameter.HMD_Connected, xr.SessionRunning || probe.Head.Present);
        SendParameter(OpenXRParameter.HMD_Battery, probe.Head.Charge);
        SendParameter(OpenXRParameter.HMD_Charging, probe.Head.Charging);

        SendParameter(OpenXRParameter.LHand_Connected, xr.Left.IsActive || probe.Left.Present);
        SendParameter(OpenXRParameter.LHand_Battery, probe.Left.Charge);
        SendParameter(OpenXRParameter.LHand_Charging, probe.Left.Charging);

        SendParameter(OpenXRParameter.RHand_Connected, xr.Right.IsActive || probe.Right.Present);
        SendParameter(OpenXRParameter.RHand_Battery, probe.Right.Charge);
        SendParameter(OpenXRParameter.RHand_Charging, probe.Right.Charging);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000f / 60f)]
    private void UpdateRealtimeParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;
        var xr = _runtime.Snapshot;

        SendParameter(OpenXRParameter.FPS, (int)MathF.Round(xr.DisplayRefreshRate));
        SendParameter(OpenXRParameter.FPSNormalised, Math.Clamp(xr.DisplayRefreshRate / 240f, 0f, 1f));

        if (xr.Left.IsActive)
        {
            SendParameter(OpenXRParameter.LeftATouch, xr.Left.PrimaryTouch);
            SendParameter(OpenXRParameter.LeftBTouch, xr.Left.SecondaryTouch);
            SendParameter(OpenXRParameter.LeftPadTouch, xr.Left.PadTouch);
            SendParameter(OpenXRParameter.LeftStickTouch, xr.Left.StickTouch);
            SendParameter(OpenXRParameter.LeftIndex, xr.Left.Index);
            SendParameter(OpenXRParameter.LeftMiddle, xr.Left.Middle);
            SendParameter(OpenXRParameter.LeftRing, xr.Left.Ring);
            SendParameter(OpenXRParameter.LeftPinky, xr.Left.Pinky);
        }

        if (xr.Right.IsActive)
        {
            SendParameter(OpenXRParameter.RightATouch, xr.Right.PrimaryTouch);
            SendParameter(OpenXRParameter.RightBTouch, xr.Right.SecondaryTouch);
            SendParameter(OpenXRParameter.RightPadTouch, xr.Right.PadTouch);
            SendParameter(OpenXRParameter.RightStickTouch, xr.Right.StickTouch);
            SendParameter(OpenXRParameter.RightIndex, xr.Right.Index);
            SendParameter(OpenXRParameter.RightMiddle, xr.Right.Middle);
            SendParameter(OpenXRParameter.RightRing, xr.Right.Ring);
            SendParameter(OpenXRParameter.RightPinky, xr.Right.Pinky);
        }
    }

    /// <summary>The game is visible but something else (system UI, dashboard) has focus.</summary>
    private static bool IsDashboardVisible(DeviceProbeResult probe)
        => probe.PrimaryApp is { IsVisible: true, IsFocused: false };

    private static int Percent(DeviceBattery battery) => battery.Present ? (int)MathF.Round(battery.Charge * 100f) : 0;

    private enum OpenXRSetting { OverlaySession }

    private enum OpenXRParameter
    {
        FPS, FPSNormalised, UserPresent, DashboardVisible,
        HMD_Connected, HMD_Battery, HMD_Charging,
        LHand_Connected, LHand_Battery, LHand_Charging,
        RHand_Connected, RHand_Battery, RHand_Charging,
        LeftATouch, LeftBTouch, LeftPadTouch, LeftStickTouch,
        LeftIndex, LeftMiddle, LeftRing, LeftPinky,
        RightATouch, RightBTouch, RightPadTouch, RightStickTouch,
        RightIndex, RightMiddle, RightRing, RightPinky
    }

    private enum OpenXRVariable
    {
        FPS, TargetHz, DashboardVisible, RuntimeName, SystemName, SessionState,
        HMD_Battery, HMD_Charging,
        LHand_Battery, LHand_Charging,
        RHand_Battery, RHand_Charging
    }

    private enum OpenXRState { Default, NoRuntime }
}
