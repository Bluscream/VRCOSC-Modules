// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// VR part: MagicChatbox reads reprojection ratio and dropped frames from OpenVR's compositor
// (IVRCompositor::GetFrameTiming). OpenXR has no equivalent: there is no compositor statistics
// API in the core spec or in any extension the Linux runtimes (Monado, WiVRn) implement, and a
// headless client cannot see VRChat's own frame timing at all. What the runtime does reveal is
// its display-period schedule: every xrWaitFrame returns a predicted display time, and when the
// runtime skips periods between two consecutive answers this client missed those frames. The
// shared OpenXRRuntime counts those; this file turns the counters into
//   vr_reprojection    percent of frames in the last second the runtime had to bridge (missed / scheduled)
//   vr_dropped_frames  frames missed in the last 60 seconds
// Both describe VRCOSC's own headless session, not VRChat's render loop, so they are a proxy
// for how busy the runtime/compositor is rather than a per-game measurement.

using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Modules;
using VRCOSC.Modules.OpenXR;

namespace Bluscream.Modules.MCBParity;

public sealed partial class MCBParityModule
{
    private const int DroppedWindowSeconds = 60;

    private readonly OpenXRRuntime _xr = OpenXRRuntime.Shared;
    private readonly long[] _missedPerSecond = new long[DroppedWindowSeconds];
    private int _missedRing;
    private bool _xrAcquired;
    private bool _xrLoggedNoStats;
    private (long Frames, long Missed) _lastCounters;

    private void CreateVrSettings()
    {
        CreateToggle(MCBParitySetting.VrFrameStats, "VR frame estimates",
            "Keep the shared OpenXR session open to estimate reprojection and dropped frames from the runtime's display-period schedule. Off leaves both variables at 0.", true);
    }

    private (ClipVariableReference Reprojection, ClipVariableReference Dropped) CreateVrVariables()
    {
        var reprojection = CreateVariable<int>(MCBParityVariable.vr_reprojection, "VR Reprojection (% of frames, last second)")!;
        var dropped = CreateVariable<int>(MCBParityVariable.vr_dropped_frames, "VR Dropped Frames (per minute)")!;
        return (reprojection, dropped);
    }

    private void StartVr()
    {
        Array.Clear(_missedPerSecond);
        _missedRing = 0;
        _lastCounters = default;
        _xrLoggedNoStats = false;

        if (!GetSettingValue<bool>(MCBParitySetting.VrFrameStats)) return;

        _xr.Acquire(Log);
        _xrAcquired = true;
        Log("OpenXR exposes no compositor statistics; reprojection and dropped frames are estimated from display periods the runtime skips on VRCOSC's own headless session.");
    }

    private void StopVr()
    {
        if (!_xrAcquired) return;
        _xr.Release(Log);
        _xrAcquired = false;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateVr()
    {
        if (!_xrAcquired)
        {
            SetVariableValue(MCBParityVariable.vr_reprojection, 0);
            SetVariableValue(MCBParityVariable.vr_dropped_frames, 0);
            return;
        }

        var snapshot = _xr.Snapshot;
        var counters = _xr.FrameCounters;
        var frames = counters.Frames - _lastCounters.Frames;
        var missed = counters.Missed - _lastCounters.Missed;
        _lastCounters = counters;

        if (!snapshot.SessionRunning || frames <= 0)
        {
            if (!_xrLoggedNoStats && snapshot.RuntimeAvailable && snapshot.SessionRunning)
            {
                _xrLoggedNoStats = true;
                Log("OpenXR session is running but xrWaitFrame is not pacing this client; the runtime provides no frame statistics on this path, so reprojection and dropped frames stay 0.");
            }

            missed = 0;
            frames = 0;
        }

        _missedPerSecond[_missedRing] = missed;
        _missedRing = (_missedRing + 1) % DroppedWindowSeconds;

        var scheduled = frames + missed;
        var reprojection = scheduled > 0 ? (int)Math.Round(missed * 100d / scheduled) : 0;

        SetVariableValue(MCBParityVariable.vr_reprojection, reprojection);
        SetVariableValue(MCBParityVariable.vr_dropped_frames, (int)Math.Min(int.MaxValue, _missedPerSecond.Sum()));
    }
}
