// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// OpenXR equivalent of the official SteamVR Haptic Control Module. Same OSC contract:
// Duration / Frequency / Amplitude followed by a TriggerLeft/TriggerRight pulse, or the
// wildcard form TriggerLeft/<duration>/<frequency>/<amplitude>.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OpenXR;

[ModuleTitle("OpenXR Haptic Control")]
[ModuleDescription("Lets you trigger haptics on OpenXR controllers (WiVRn, Monado, SteamVR)")]
[ModuleType(ModuleType.SteamVR)]
public class OpenXRHapticControlModule : Module
{
    private readonly OpenXRRuntime _runtime = OpenXRRuntime.Shared;

    private float _duration;
    private float _frequency;
    private float _amplitude;

    protected override void OnPreLoad()
    {
        Bluscream.ModuleUtils.RegisterNativeResolver(Log);

        RegisterParameter<float>(HapticParameter.Duration, "VRCOSC/VR/Haptics/Duration", ParameterMode.Read, "Duration", "The duration of the haptic trigger in seconds");
        RegisterParameter<float>(HapticParameter.Frequency, "VRCOSC/VR/Haptics/Frequency", ParameterMode.Read, "Frequency", "The frequency of the haptic trigger (0-1, mapped to 0-100 Hz; 0 lets the runtime choose)");
        RegisterParameter<float>(HapticParameter.Amplitude, "VRCOSC/VR/Haptics/Amplitude", ParameterMode.Read, "Amplitude", "The amplitude of the haptic trigger (0-1)");
        RegisterParameter<bool>(HapticParameter.TriggerLeft, "VRCOSC/VR/Haptics/TriggerLeft", ParameterMode.Read, "Trigger Left", "Becoming true causes a haptic trigger in the left controller using the above parameters");
        RegisterParameter<bool>(HapticParameter.TriggerRight, "VRCOSC/VR/Haptics/TriggerRight", ParameterMode.Read, "Trigger Right", "Becoming true causes a haptic trigger in the right controller using the above parameters");

        RegisterParameter<bool>(HapticParameter.TriggerLeftDirect, "VRCOSC/VR/Haptics/TriggerLeft/*/*/*", ParameterMode.Read, "Trigger Left Direct",
            "Becoming true causes a haptic trigger in the left controller using the wildcards\nFor example:\n Writing 'VRCOSC/VR/Haptics/TriggerLeft/2/0.5/0.75' will trigger haptics in the left controller with a 2 second duration, 0.5 frequency, and 0.75 amplitude");
        RegisterParameter<bool>(HapticParameter.TriggerRightDirect, "VRCOSC/VR/Haptics/TriggerRight/*/*/*", ParameterMode.Read, "Trigger Right Direct",
            "Becoming true causes a haptic trigger in the right controller using the wildcards\nFor example:\n Writing 'VRCOSC/VR/Haptics/TriggerRight/2/0.5/0.75' will trigger haptics in the right controller with a 2 second duration, 0.5 frequency, and 0.75 amplitude");
    }

    protected override Task<bool> OnModuleStart()
    {
        _duration = 0f;
        _frequency = 0f;
        _amplitude = 0f;
        _runtime.Acquire(Log);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _runtime.Release(Log);
        return Task.CompletedTask;
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case HapticParameter.Duration:
                _duration = parameter.GetValue<float>();
                break;

            case HapticParameter.Frequency:
                _frequency = ConvertFrequency(parameter.GetValue<float>());
                break;

            case HapticParameter.Amplitude:
                _amplitude = ConvertAmplitude(parameter.GetValue<float>());
                break;

            case HapticParameter.TriggerLeft when parameter.GetValue<bool>():
                Trigger(XrHand.Left, _duration, _frequency, _amplitude);
                break;

            case HapticParameter.TriggerRight when parameter.GetValue<bool>():
                Trigger(XrHand.Right, _duration, _frequency, _amplitude);
                break;

            case HapticParameter.TriggerLeftDirect when parameter.GetValue<bool>():
                Trigger(XrHand.Left, parameter.GetWildcard<float>(0), ConvertFrequency(parameter.GetWildcard<float>(1)), ConvertAmplitude(parameter.GetWildcard<float>(2)));
                break;

            case HapticParameter.TriggerRightDirect when parameter.GetValue<bool>():
                Trigger(XrHand.Right, parameter.GetWildcard<float>(0), ConvertFrequency(parameter.GetWildcard<float>(1)), ConvertAmplitude(parameter.GetWildcard<float>(2)));
                break;
        }
    }

    private void Trigger(XrHand hand, float duration, float frequency, float amplitude)
    {
        if (!_runtime.Snapshot.SessionRunning)
        {
            Log($"Haptic on {hand} ignored: no running OpenXR session.");
            return;
        }

        _runtime.RequestHaptic(hand, duration, frequency, amplitude);
    }

    // Same mapping as the official SteamVR module, so existing avatar setups feel the same.
    private static float ConvertFrequency(float frequency) => Math.Clamp(frequency, 0f, 1f) * 100f;
    private static float ConvertAmplitude(float amplitude) => Math.Clamp(amplitude, 0f, 1f);

    private enum HapticParameter
    {
        Duration, Frequency, Amplitude,
        TriggerLeft, TriggerRight,
        TriggerLeftDirect, TriggerRightDirect
    }
}
