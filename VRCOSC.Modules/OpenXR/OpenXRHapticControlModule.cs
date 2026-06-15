// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// OpenXR equivalent of the official SteamVR Haptic Control Module.
// Uses Silk.NET.OpenXR xrApplyHapticFeedback for cross-platform haptic feedback.

using Silk.NET.OpenXR;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OpenXR;

[ModuleTitle("OpenXR Haptic Control")]
[ModuleDescription("Trigger haptic feedback on OpenXR controllers (SteamVR, Monado, etc.)")]
[ModuleType(ModuleType.SteamVR)]
public class OpenXRHapticControlModule : Module
{
    private float _duration;
    private float _frequency;
    private float _amplitude;

    private XR?       _xr;
    private Instance  _instance;
    private Session   _session;
    private ulong     _systemId;
    private bool      _xrReady;

    private ActionSet              _actionSet;
    private Silk.NET.OpenXR.Action _hapticLeft;
    private Silk.NET.OpenXR.Action _hapticRight;

    protected override void OnPreLoad()
    {
        Bluscream.ModuleUtils.RegisterNativeResolver();
        RegisterParameter<float>(OpenXRHapticParameter.Duration,          "VRCOSC/VR/Haptics/Duration",          ParameterMode.Read, "Duration",            "Duration of haptic in seconds");
        RegisterParameter<float>(OpenXRHapticParameter.Frequency,         "VRCOSC/VR/Haptics/Frequency",         ParameterMode.Read, "Frequency",           "Frequency of haptic (0-1 → 0-300 Hz)");
        RegisterParameter<float>(OpenXRHapticParameter.Amplitude,         "VRCOSC/VR/Haptics/Amplitude",         ParameterMode.Read, "Amplitude",           "Amplitude of haptic (0-1)");
        RegisterParameter<bool> (OpenXRHapticParameter.TriggerLeft,       "VRCOSC/VR/Haptics/TriggerLeft",       ParameterMode.Read, "Trigger Left",        "Trigger haptic on left controller");
        RegisterParameter<bool> (OpenXRHapticParameter.TriggerRight,      "VRCOSC/VR/Haptics/TriggerRight",      ParameterMode.Read, "Trigger Right",       "Trigger haptic on right controller");
        RegisterParameter<bool> (OpenXRHapticParameter.TriggerLeftDirect, "VRCOSC/VR/Haptics/TriggerLeft/*/*/*", ParameterMode.Read, "Trigger Left Direct",
            "Trigger haptic on left controller using wildcards: Duration / Frequency / Amplitude\nExample: VRCOSC/VR/Haptics/TriggerLeft/2/0.5/0.75");
        RegisterParameter<bool> (OpenXRHapticParameter.TriggerRightDirect,"VRCOSC/VR/Haptics/TriggerRight/*/*/*",ParameterMode.Read, "Trigger Right Direct",
            "Trigger haptic on right controller using wildcards: Duration / Frequency / Amplitude");
    }

    protected override Task<bool> OnModuleStart()
    {
        _duration = _frequency = _amplitude = 0f;
        _xrReady  = false;

        try
        {
            _xr = XR.GetApi();
            if (InitialiseOpenXR()) { _xrReady = true; Log("OpenXR haptics ready."); }
            else Log("OpenXR runtime not available — haptic triggers will be silently ignored.");
        }
        catch (Exception ex) { Log($"OpenXR haptics init error: {ex.Message}"); }

        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        TearDownOpenXR();
        return Task.CompletedTask;
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case OpenXRHapticParameter.Duration:
                _duration = parameter.GetValue<float>();
                break;
            case OpenXRHapticParameter.Frequency:
                _frequency = ConvertFrequency(parameter.GetValue<float>());
                break;
            case OpenXRHapticParameter.Amplitude:
                _amplitude = ConvertAmplitude(parameter.GetValue<float>());
                break;
            case OpenXRHapticParameter.TriggerLeft when parameter.GetValue<bool>():
                _ = TriggerHapticAsync(true, false);
                break;
            case OpenXRHapticParameter.TriggerRight when parameter.GetValue<bool>():
                _ = TriggerHapticAsync(false, true);
                break;
            case OpenXRHapticParameter.TriggerLeftDirect when parameter.GetValue<bool>():
                _ = TriggerHapticAsync(true, false,
                    parameter.GetWildcard<float>(0),
                    ConvertFrequency(parameter.GetWildcard<float>(1)),
                    ConvertAmplitude(parameter.GetWildcard<float>(2)));
                break;
            case OpenXRHapticParameter.TriggerRightDirect when parameter.GetValue<bool>():
                _ = TriggerHapticAsync(false, true,
                    parameter.GetWildcard<float>(0),
                    ConvertFrequency(parameter.GetWildcard<float>(1)),
                    ConvertAmplitude(parameter.GetWildcard<float>(2)));
                break;
        }
    }

    // ── Haptic triggering (non-unsafe wrapper so we can await) ────
    private async Task TriggerHapticAsync(bool left, bool right,
        float? localDuration  = null,
        float? localFrequency = null,
        float? localAmplitude = null)
    {
        if (!_xrReady || _xr is null) return;

        float dur  = localDuration  ?? _duration;
        float freq = localFrequency ?? _frequency;
        float amp  = localAmplitude ?? _amplitude;

        if (left)  { ApplyHaptic(_hapticLeft,  dur, freq, amp); await Task.Delay(10); }
        if (right)   ApplyHaptic(_hapticRight, dur, freq, amp);
    }

    // Unsafe code isolated to a non-async method (C# restriction)
    private unsafe void ApplyHaptic(Silk.NET.OpenXR.Action action, float dur, float freq, float amp)
    {
        if (_xr is null || action.Handle == 0 || _session.Handle == 0) return;

        var vib = new HapticVibration
        {
            Type      = StructureType.HapticVibration,
            Duration  = (long)(dur * 1_000_000_000L),
            Frequency = freq,
            Amplitude = amp
        };
        var info = new HapticActionInfo { Type = StructureType.HapticActionInfo, Action = action };
        _xr.ApplyHapticFeedback(_session, in info, (HapticBaseHeader*)&vib);
    }

    // ── OpenXR init/teardown ─────────────────────────────────────
    private unsafe bool InitialiseOpenXR()
    {
        if (_xr is null) return false;

        var appInfo = new ApplicationInfo();
        OpenXRHelper.FillApplicationInfo(ref appInfo, "VRCOSC OpenXR Haptics");
        appInfo.ApplicationVersion = 1;
        appInfo.ApiVersion = OpenXRHelper.XrVersion10;

        var instCI = new InstanceCreateInfo { Type = StructureType.InstanceCreateInfo, ApplicationInfo = appInfo };
        if (_xr.CreateInstance(in instCI, ref _instance) != Result.Success)
        { Log("xrCreateInstance failed — is an OpenXR runtime installed?"); return false; }

        var sysInfo = new SystemGetInfo { Type = StructureType.SystemGetInfo, FormFactor = FormFactor.HeadMountedDisplay };
        if (_xr.GetSystem(_instance, in sysInfo, ref _systemId) != Result.Success)
        { Log("xrGetSystem failed — no HMD connected?"); return false; }

        var sessionCI = new SessionCreateInfo { Type = StructureType.SessionCreateInfo, SystemId = _systemId };
        if (_xr.CreateSession(_instance, in sessionCI, ref _session) != Result.Success)
        { Log("Could not create OpenXR session — haptics need an active VR session."); return false; }

        var asCI = new ActionSetCreateInfo { Type = StructureType.ActionSetCreateInfo, Priority = 0 };
        OpenXRHelper.FillActionSetCreateInfo(ref asCI, "vrcosc_haptics", "VRCOSC Haptics");
        if (_xr.CreateActionSet(_instance, in asCI, ref _actionSet) != Result.Success) return false;

        _hapticLeft  = CreateHapticAction("haptic_left",  "Left Haptic");
        _hapticRight = CreateHapticAction("haptic_right", "Right Haptic");
        return true;
    }

    private Silk.NET.OpenXR.Action CreateHapticAction(string name, string localName)
    {
        var action = new Silk.NET.OpenXR.Action();
        if (_xr is null) return action;

        var aCI = new ActionCreateInfo { Type = StructureType.ActionCreateInfo, ActionType = ActionType.VibrationOutput };
        OpenXRHelper.FillActionCreateInfo(ref aCI, name, localName);
        _xr.CreateAction(_actionSet, in aCI, ref action);
        return action;
    }

    private void TearDownOpenXR()
    {
        if (_xr is null) return;
        if (_hapticLeft.Handle  != 0) { _xr.DestroyAction(_hapticLeft);   _hapticLeft  = default; }
        if (_hapticRight.Handle != 0) { _xr.DestroyAction(_hapticRight);  _hapticRight = default; }
        if (_actionSet.Handle   != 0) { _xr.DestroyActionSet(_actionSet); _actionSet   = default; }
        if (_session.Handle     != 0) { _xr.DestroySession(_session);     _session     = default; }
        if (_instance.Handle    != 0) { _xr.DestroyInstance(_instance);   _instance    = default; }
        _xr.Dispose(); _xr = null; _xrReady = false;
    }

    private static float ConvertFrequency(float v) => Math.Clamp(v, 0, 1) * 300f;
    private static float ConvertAmplitude (float v) => Math.Clamp(v, 0, 1);

    private enum OpenXRHapticParameter
    {
        Duration, Frequency, Amplitude,
        TriggerLeft, TriggerRight,
        TriggerLeftDirect, TriggerRightDirect
    }
}
