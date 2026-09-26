// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Action set, interaction-profile bindings, per-frame input sync and haptics for OpenXRRuntime.

using System.Collections.Concurrent;
using Silk.NET.OpenXR;
using XrAction = Silk.NET.OpenXR.Action;

namespace VRCOSC.Modules.OpenXR;

internal sealed unsafe partial class OpenXRRuntime
{
    private static readonly string[] HandPathStrings = { "/user/hand/left", "/user/hand/right" };

    private readonly record struct HapticRequest(XrHand Hand, float DurationSeconds, float FrequencyHz, float Amplitude);

    private readonly ConcurrentQueue<HapticRequest> _hapticQueue = new();
    private readonly HandInput[] _handInputs = { HandInput.Inactive, HandInput.Inactive };
    private readonly string[] _profiles = { string.Empty, string.Empty };
    private readonly ulong[] _handPaths = new ulong[2];

    private ActionSet _actionSet;
    private XrAction _trigger;
    private XrAction _squeeze;
    private XrAction _primaryTouch;
    private XrAction _secondaryTouch;
    private XrAction _stickTouch;
    private XrAction _padTouch;
    private XrAction _haptic;
    private bool _inputAttached;

    /// <summary>
    /// Queues a vibration on <paramref name="hand"/>. Applied on the OpenXR thread within one
    /// frame; dropped silently if no session is running (matches the official module, which
    /// ignores triggers when SteamVR is not up).
    /// </summary>
    public void RequestHaptic(XrHand hand, float durationSeconds, float frequencyHz, float amplitude)
        => _hapticQueue.Enqueue(new HapticRequest(hand, durationSeconds, frequencyHz, amplitude));

    private void InitialiseInput()
    {
        var xr = _xr!;
        _inputAttached = false;

        for (var i = 0; i < 2; i++) _handPaths[i] = OpenXRHelper.Path(xr, _instance, HandPathStrings[i]);

        var setInfo = new ActionSetCreateInfo { Type = StructureType.ActionSetCreateInfo, Priority = 0 };
        OpenXRHelper.FillActionSetCreateInfo(ref setInfo, "vrcosc", "VRCOSC");
        ActionSet set = default;
        var r = xr.CreateActionSet(_instance, &setInfo, &set);
        if (r != Result.Success) { Log($"xrCreateActionSet failed: {r}; controller input and haptics disabled."); return; }
        _actionSet = set;

        _trigger = CreateAction(ActionType.FloatInput, "trigger", "Trigger");
        _squeeze = CreateAction(ActionType.FloatInput, "squeeze", "Grip");
        _primaryTouch = CreateAction(ActionType.BooleanInput, "primary_touch", "Primary Button Touch");
        _secondaryTouch = CreateAction(ActionType.BooleanInput, "secondary_touch", "Secondary Button Touch");
        _stickTouch = CreateAction(ActionType.BooleanInput, "stick_touch", "Thumbstick Touch");
        _padTouch = CreateAction(ActionType.BooleanInput, "pad_touch", "Pad / Thumbrest Touch");
        _haptic = CreateAction(ActionType.VibrationOutput, "haptic", "Haptic");

        var accepted = new List<string>();
        foreach (var (profile, bindings) in ProfileBindings())
            if (SuggestBindings(profile, bindings)) accepted.Add(profile);
        Log($"Interaction profiles accepted: {string.Join(", ", accepted)}");

        var attachInfo = new SessionActionSetsAttachInfo { Type = StructureType.SessionActionSetsAttachInfo, CountActionSets = 1 };
        fixed (ActionSet* p = &_actionSet)
        {
            attachInfo.ActionSets = p;
            r = xr.AttachSessionActionSets(_session, &attachInfo);
        }
        if (r != Result.Success) { Log($"xrAttachSessionActionSets failed: {r}; controller input and haptics disabled."); return; }
        _inputAttached = true;
    }

    private XrAction CreateAction(ActionType type, string name, string localName)
    {
        var info = new ActionCreateInfo { Type = StructureType.ActionCreateInfo, ActionType = type, CountSubactionPaths = 2 };
        OpenXRHelper.FillActionCreateInfo(ref info, name, localName);

        XrAction action = default;
        fixed (ulong* paths = _handPaths)
        {
            info.SubactionPaths = paths;
            var r = _xr!.CreateAction(_actionSet, &info, &action);
            if (r != Result.Success) Log($"xrCreateAction({name}) failed: {r}");
        }
        return action;
    }

    /// <summary>
    /// Bindings per interaction profile. Each entry is (action, per-hand input suffix); a null
    /// suffix on one side means that profile has no such input on that hand.
    /// Boolean inputs bound to float actions (Vive squeeze/click, simple select) are legal per
    /// spec and read as 0/1.
    /// </summary>
    private IEnumerable<(string Profile, List<(XrAction Action, string? Left, string? Right)> Bindings)> ProfileBindings()
    {
        yield return ("/interaction_profiles/oculus/touch_controller", new()
        {
            (_trigger, "input/trigger/value", "input/trigger/value"),
            (_squeeze, "input/squeeze/value", "input/squeeze/value"),
            (_primaryTouch, "input/x/touch", "input/a/touch"),
            (_secondaryTouch, "input/y/touch", "input/b/touch"),
            (_stickTouch, "input/thumbstick/touch", "input/thumbstick/touch"),
            (_padTouch, "input/thumbrest/touch", "input/thumbrest/touch"),
            (_haptic, "output/haptic", "output/haptic")
        });

        yield return ("/interaction_profiles/valve/index_controller", new()
        {
            (_trigger, "input/trigger/value", "input/trigger/value"),
            (_squeeze, "input/squeeze/value", "input/squeeze/value"),
            (_primaryTouch, "input/a/touch", "input/a/touch"),
            (_secondaryTouch, "input/b/touch", "input/b/touch"),
            (_stickTouch, "input/thumbstick/touch", "input/thumbstick/touch"),
            (_padTouch, "input/trackpad/touch", "input/trackpad/touch"),
            (_haptic, "output/haptic", "output/haptic")
        });

        yield return ("/interaction_profiles/htc/vive_controller", new()
        {
            (_trigger, "input/trigger/value", "input/trigger/value"),
            (_squeeze, "input/squeeze/click", "input/squeeze/click"),
            (_padTouch, "input/trackpad/touch", "input/trackpad/touch"),
            (_haptic, "output/haptic", "output/haptic")
        });

        yield return ("/interaction_profiles/khr/simple_controller", new()
        {
            (_trigger, "input/select/click", "input/select/click"),
            (_haptic, "output/haptic", "output/haptic")
        });
    }

    private bool SuggestBindings(string profile, List<(XrAction Action, string? Left, string? Right)> bindings)
    {
        var xr = _xr!;
        var list = new List<ActionSuggestedBinding>();

        foreach (var (action, left, right) in bindings)
        {
            if (action.Handle == 0) continue;
            if (left is not null) list.Add(new ActionSuggestedBinding { Action = action, Binding = OpenXRHelper.Path(xr, _instance, $"{HandPathStrings[0]}/{left}") });
            if (right is not null) list.Add(new ActionSuggestedBinding { Action = action, Binding = OpenXRHelper.Path(xr, _instance, $"{HandPathStrings[1]}/{right}") });
        }

        list.RemoveAll(b => b.Binding == 0);
        if (list.Count == 0) return false;

        var arr = list.ToArray();
        fixed (ActionSuggestedBinding* p = arr)
        {
            var info = new InteractionProfileSuggestedBinding
            {
                Type = StructureType.InteractionProfileSuggestedBinding,
                InteractionProfile = OpenXRHelper.Path(xr, _instance, profile),
                CountSuggestedBindings = (uint)arr.Length,
                SuggestedBindings = p
            };
            var r = xr.SuggestInteractionProfileBinding(_instance, &info);
            if (r == Result.Success) return true;
            Log($"Bindings for {profile} rejected: {r}");
            return false;
        }
    }

    private void SyncInput()
    {
        if (!_inputAttached) return;
        var xr = _xr!;

        var active = new ActiveActionSet { ActionSet = _actionSet, SubactionPath = 0 };
        var syncInfo = new ActionsSyncInfo { Type = StructureType.ActionsSyncInfo, CountActiveActionSets = 1, ActiveActionSets = &active };
        var r = xr.SyncAction(_session, &syncInfo);
        if (r != Result.Success)
        {
            // SESSION_NOT_FOCUSED is the runtime saying "another app has the controllers";
            // the states below then read as inactive, which is what we want to publish.
            if (r != Result.SessionNotFocused) LogOnce($"xrSyncActions failed: {r}");
        }

        for (var i = 0; i < 2; i++)
        {
            var path = _handPaths[i];
            var trigger = ReadFloat(_trigger, path, out var triggerActive);
            var squeeze = ReadFloat(_squeeze, path, out var squeezeActive);

            if (!triggerActive && !squeezeActive)
            {
                // Keep hand-tracking data if it is what we have; SyncInput runs before LocateHands,
                // which overwrites the entry when joints are active.
                if (_handInputs[i].Source != HandDataSource.HandTracking) _handInputs[i] = HandInput.Inactive;
                continue;
            }

            _handInputs[i] = new HandInput(
                IsActive: true,
                Source: HandDataSource.Controller,
                Index: trigger,
                Middle: squeeze,
                Ring: squeeze,
                Pinky: squeeze,
                PrimaryTouch: ReadBool(_primaryTouch, path),
                SecondaryTouch: ReadBool(_secondaryTouch, path),
                StickTouch: ReadBool(_stickTouch, path),
                PadTouch: ReadBool(_padTouch, path));
        }
    }

    private float ReadFloat(XrAction action, ulong subaction, out bool isActive)
    {
        isActive = false;
        if (action.Handle == 0) return 0f;

        var getInfo = new ActionStateGetInfo { Type = StructureType.ActionStateGetInfo, Action = action, SubactionPath = subaction };
        var state = new ActionStateFloat { Type = StructureType.ActionStateFloat };
        if (_xr!.GetActionStateFloat(_session, &getInfo, &state) != Result.Success) return 0f;

        isActive = state.IsActive != 0;
        return isActive ? state.CurrentState : 0f;
    }

    private bool ReadBool(XrAction action, ulong subaction)
    {
        if (action.Handle == 0) return false;

        var getInfo = new ActionStateGetInfo { Type = StructureType.ActionStateGetInfo, Action = action, SubactionPath = subaction };
        var state = new ActionStateBoolean { Type = StructureType.ActionStateBoolean };
        if (_xr!.GetActionStateBoolean(_session, &getInfo, &state) != Result.Success) return false;

        return state.IsActive != 0 && state.CurrentState != 0;
    }

    private void RefreshInteractionProfiles()
    {
        if (!_inputAttached) return;

        for (var i = 0; i < 2; i++)
        {
            var state = new InteractionProfileState { Type = StructureType.InteractionProfileState };
            if (_xr!.GetCurrentInteractionProfile(_session, _handPaths[i], &state) != Result.Success) continue;
            _profiles[i] = OpenXRHelper.PathToString(_xr, _instance, state.InteractionProfile);
        }

        Log($"Interaction profiles now: left='{_profiles[0]}', right='{_profiles[1]}'");
    }

    private void ApplyQueuedHaptics()
    {
        while (_hapticQueue.TryDequeue(out var request))
        {
            if (!_inputAttached || _haptic.Handle == 0) continue;

            var vibration = new HapticVibration
            {
                Type = StructureType.HapticVibration,
                Duration = request.DurationSeconds <= 0f ? -1L : (long)(request.DurationSeconds * 1_000_000_000d),
                Frequency = request.FrequencyHz,
                Amplitude = Math.Clamp(request.Amplitude, 0f, 1f)
            };
            var info = new HapticActionInfo { Type = StructureType.HapticActionInfo, Action = _haptic, SubactionPath = _handPaths[(int)request.Hand] };
            var r = _xr!.ApplyHapticFeedback(_session, &info, (HapticBaseHeader*)&vibration);
            if (r != Result.Success) LogOnce($"xrApplyHapticFeedback failed: {r}");
        }
    }

    private void DestroyInput()
    {
        var xr = _xr!;
        _inputAttached = false;

        foreach (var action in new[] { _trigger, _squeeze, _primaryTouch, _secondaryTouch, _stickTouch, _padTouch, _haptic })
            if (action.Handle != 0) xr.DestroyAction(action);

        _trigger = _squeeze = _primaryTouch = _secondaryTouch = _stickTouch = _padTouch = _haptic = default;

        if (_actionSet.Handle != 0) { xr.DestroyActionSet(_actionSet); _actionSet = default; }
        _hapticQueue.Clear();
    }
}
