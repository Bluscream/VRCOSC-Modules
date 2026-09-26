// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// OpenXR equivalent of the official Index Gesture Extensions Module. Finger curls come from
// XR_EXT_hand_tracking joints when the runtime provides them, otherwise from controller
// trigger/grip values (index = trigger, other fingers = grip), which is how Touch-style
// controllers express hand poses anyway.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OpenXR;

[ModuleTitle("OpenXR Gesture Extensions")]
[ModuleDescription("Detect a range of custom gestures from OpenXR hand tracking or controllers")]
[ModuleType(ModuleType.SteamVR)]
[ModuleInfo("https://vrcosc.com/docs/V2/Modules/gesture-extensions")]
public class OpenXRGestureExtensionsModule : Module
{
    private readonly OpenXRRuntime _runtime = OpenXRRuntime.Shared;

    protected override void OnPreLoad()
    {
        Bluscream.ModuleUtils.RegisterNativeResolver(Log);

        CreateSlider(GestureSetting.Threshold, "Threshold", "How far down a finger should be to be considered down\n0 being fully up. 1 being fully down", 0.5f, 0f, 1f, 0.01f);

        RegisterParameter<int>(GestureParameter.GestureLeft, "VRCOSC/VR/Gestures/Left", ParameterMode.Write, "Left Gestures", "Custom left hand gesture value");
        RegisterParameter<int>(GestureParameter.GestureRight, "VRCOSC/VR/Gestures/Right", ParameterMode.Write, "Right Gestures", "Custom right hand gesture value");
    }

    protected override Task<bool> OnModuleStart()
    {
        _runtime.Acquire(Log);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _runtime.Release(Log);
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000f / 60f)]
    private void SendParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;
        var xr = _runtime.Snapshot;

        // Like the official module: only send while the hand is present, so a missing
        // controller does not spam "None".
        if (xr.Left.IsActive) SendParameter(GestureParameter.GestureLeft, (int)GetGesture(xr.Left));
        if (xr.Right.IsActive) SendParameter(GestureParameter.GestureRight, (int)GetGesture(xr.Right));
    }

    private GestureName GetGesture(HandInput hand)
    {
        if (IsDoubleGun(hand)) return GestureName.DoubleGun;
        if (IsMiddleFinger(hand)) return GestureName.MiddleFinger;
        if (IsPinkyFinger(hand)) return GestureName.PinkyFinger;
        return GestureName.None;
    }

    private float Threshold => GetSettingValue<float>(GestureSetting.Threshold);

    private bool IsDoubleGun(HandInput h) =>
        h.Index <= Threshold
        && h.Middle <= Threshold
        && h.Ring > Threshold
        && h.Pinky > Threshold
        && !h.PrimaryTouch && !h.SecondaryTouch && !h.StickTouch && !h.PadTouch;

    private bool IsMiddleFinger(HandInput h) =>
        h.Index > Threshold
        && h.Middle <= Threshold
        && h.Ring > Threshold
        && h.Pinky > Threshold;

    private bool IsPinkyFinger(HandInput h) =>
        h.Index > Threshold
        && h.Middle > Threshold
        && h.Ring > Threshold
        && h.Pinky <= Threshold;

    private enum GestureSetting { Threshold }
    private enum GestureParameter { GestureLeft, GestureRight }
    private enum GestureName { None, DoubleGun, MiddleFinger, PinkyFinger }
}
