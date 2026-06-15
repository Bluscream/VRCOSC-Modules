// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// OpenXR equivalent of the official Index Gesture Extensions Module.
// Uses XR_EXT_hand_tracking for finger curl values where available.

using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.EXT;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace VRCOSC.Modules.OpenXR;

[ModuleTitle("OpenXR Gesture Extensions")]
[ModuleDescription("Detect custom hand gestures via OpenXR hand tracking (XR_EXT_hand_tracking)")]
[ModuleType(ModuleType.SteamVR)]
[ModuleInfo("https://vrcosc.com/docs/V2/Modules/gesture-extensions")]
public class OpenXRGestureExtensionsModule : Module
{
    private XR?             _xr;
    private Instance        _instance;
    private Session         _session;
    private ulong           _systemId;
    private bool            _xrReady;
    private bool            _handTrackingSupported;

    private ExtHandTracking? _handTrackingExt;
    private HandTrackerEXT   _leftTracker;
    private HandTrackerEXT   _rightTracker;

    private readonly float[] _leftCurl  = new float[4]; // [Index, Middle, Ring, Pinky]
    private readonly float[] _rightCurl = new float[4];

    protected override void OnPreLoad()
    {
        CreateSlider(GestureSetting.Threshold,
            "Threshold",
            "How far down a finger must be to count as 'down' (0=fully up, 1=fully down)",
            0.5f, 0f, 1f, 0.01f);

        RegisterParameter<int>(GestureParameter.GestureLeft,  "VRCOSC/VR/Gestures/Left",  ParameterMode.Write, "Left Gestures",  "Custom left hand gesture value");
        RegisterParameter<int>(GestureParameter.GestureRight, "VRCOSC/VR/Gestures/Right", ParameterMode.Write, "Right Gestures", "Custom right hand gesture value");
    }

    protected override Task<bool> OnModuleStart()
    {
        _xrReady = _handTrackingSupported = false;
        Array.Clear(_leftCurl,  0, 4);
        Array.Clear(_rightCurl, 0, 4);

        try
        {
            _xr = XR.GetApi();
            if (InitialiseOpenXR())
            {
                _xrReady = true;
                Log(_handTrackingSupported
                    ? "OpenXR gestures ready (XR_EXT_hand_tracking enabled)."
                    : "OpenXR gestures ready (no hand-tracking extension — all curls will be zero).");
            }
            else Log("OpenXR runtime not available — gesture detection disabled.");
        }
        catch (Exception ex) { Log($"OpenXR gestures init error: {ex.Message}"); }

        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        TearDownOpenXR();
        return Task.CompletedTask;
    }

    // ── Update loop ──────────────────────────────────────────────
    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000f / 60f)]
    private void SendParameters()
    {
        if (!Bluscream.ModuleUtils.IsStarted()) return;

        if (_xrReady && _handTrackingSupported && _handTrackingExt is not null)
            UpdateHandCurls();

        SendParameter(GestureParameter.GestureLeft,  (int)GetGesture(_leftCurl));
        SendParameter(GestureParameter.GestureRight, (int)GetGesture(_rightCurl));
    }

    // ── Gesture recognition ──────────────────────────────────────
    private float Threshold => GetSettingValue<float>(GestureSetting.Threshold);

    private GestureName GetGesture(float[] curl)
    {
        float i = curl[0], m = curl[1], r = curl[2], p = curl[3], th = Threshold;
        if (i < th  && m < th  && r >= th && p >= th) return GestureName.DoubleGun;
        if (i >= th && m < th  && r >= th && p >= th) return GestureName.MiddleFinger;
        if (i >= th && m >= th && r >= th && p < th)  return GestureName.PinkyFinger;
        return GestureName.None;
    }

    // ── Hand-tracking polling ────────────────────────────────────
    private void UpdateHandCurls()
    {
        UpdateSingleHand(_handTrackingExt!, _leftTracker,  _leftCurl);
        UpdateSingleHand(_handTrackingExt!, _rightTracker, _rightCurl);
    }

    private static unsafe void UpdateSingleHand(ExtHandTracking ext, HandTrackerEXT tracker, float[] curl)
    {
        if (tracker.Handle == 0) return;

        var jointLocations = stackalloc HandJointLocationEXT[OpenXRHelper.HandJointCount];
        var locations = new HandJointLocationsEXT
        {
            Type           = StructureType.HandJointLocationsExt,
            JointCount     = (uint)OpenXRHelper.HandJointCount,
            JointLocations = jointLocations
        };
        var locateInfo = new HandJointsLocateInfoEXT { Type = StructureType.HandJointsLocateInfoExt, Time = 0 };

        if (ext.LocateHandJoints(tracker, in locateInfo, ref locations) != Result.Success) return;
        if (locations.IsActive == 0) return;

        // XR_EXT_hand_tracking joint indices: Index prox=7/tip=10, Middle prox=12/tip=15, Ring prox=17/tip=20, Pinky prox=22/tip=25
        curl[0] = EstimateCurl(jointLocations, 7,  10);
        curl[1] = EstimateCurl(jointLocations, 12, 15);
        curl[2] = EstimateCurl(jointLocations, 17, 20);
        curl[3] = EstimateCurl(jointLocations, 22, 25);
    }

    private static unsafe float EstimateCurl(HandJointLocationEXT* joints, int proxIdx, int tipIdx)
    {
        var validBits = SpaceLocationFlags.PositionValidBit | SpaceLocationFlags.OrientationValidBit;
        if ((joints[proxIdx].LocationFlags & validBits) == 0) return 0f;
        if ((joints[tipIdx].LocationFlags  & validBits) == 0) return 0f;

        float dy = joints[tipIdx].Pose.Position.Y - joints[proxIdx].Pose.Position.Y;
        return Math.Clamp(-dy / 0.05f, 0f, 1f);
    }

    // ── OpenXR init/teardown ─────────────────────────────────────
    private unsafe bool InitialiseOpenXR()
    {
        if (_xr is null) return false;

        bool htSupported = _xr.IsInstanceExtensionPresent(null, ExtHandTracking.ExtensionName);

        var appInfo = new ApplicationInfo();
        OpenXRHelper.FillApplicationInfo(ref appInfo, "VRCOSC OpenXR Gestures");
        appInfo.ApplicationVersion = 1;
        appInfo.ApiVersion = OpenXRHelper.XrVersion10;

        Result result;
        if (htSupported)
        {
            // Allocate extension name pointers as unmanaged memory
            var extPtrs = OpenXRHelper.AllocStringPointers(new[] { ExtHandTracking.ExtensionName });
            try
            {
                // Convert IntPtr[] to byte*[] on the stack
                var ptrArray = new byte*[extPtrs.Length];
                for (int i = 0; i < extPtrs.Length; i++) ptrArray[i] = (byte*)extPtrs[i];

                fixed (byte** ppExts = ptrArray)
                {
                    var instCI = new InstanceCreateInfo
                    {
                        Type = StructureType.InstanceCreateInfo,
                        ApplicationInfo = appInfo,
                        EnabledExtensionCount = (uint)ptrArray.Length,
                        EnabledExtensionNames = ppExts
                    };
                    result = _xr.CreateInstance(in instCI, ref _instance);
                }
            }
            finally { OpenXRHelper.FreeStringPointers(extPtrs); }
        }
        else
        {
            var instCI = new InstanceCreateInfo { Type = StructureType.InstanceCreateInfo, ApplicationInfo = appInfo };
            result = _xr.CreateInstance(in instCI, ref _instance);
        }

        if (result != Result.Success) { Log($"xrCreateInstance failed: {result}"); return false; }

        var sysInfo = new SystemGetInfo { Type = StructureType.SystemGetInfo, FormFactor = FormFactor.HeadMountedDisplay };
        if (_xr.GetSystem(_instance, in sysInfo, ref _systemId) != Result.Success)
        { Log("xrGetSystem failed — no HMD connected?"); return false; }

        _handTrackingSupported = htSupported;

        if (htSupported && _xr.TryGetInstanceExtension<ExtHandTracking>(null, _instance, out var ext))
        {
            _handTrackingExt = ext;
            CreateHandTrackers();
        }

        return true;
    }

    private void CreateHandTrackers()
    {
        if (_handTrackingExt is null || _session.Handle == 0) return;

#pragma warning disable CS0618 // Use non-deprecated aliases when available in future SDK versions
        var leftCI = new HandTrackerCreateInfoEXT
        {
            Type         = StructureType.HandTrackerCreateInfoExt,
            Hand         = HandEXT.LeftExt,
            HandJointSet = HandJointSetEXT.DefaultExt
        };
        _handTrackingExt.CreateHandTracker(_session, in leftCI, ref _leftTracker);

        var rightCI = new HandTrackerCreateInfoEXT
        {
            Type         = StructureType.HandTrackerCreateInfoExt,
            Hand         = HandEXT.RightExt,
            HandJointSet = HandJointSetEXT.DefaultExt
        };
        _handTrackingExt.CreateHandTracker(_session, in rightCI, ref _rightTracker);
#pragma warning restore CS0618
    }

    private void TearDownOpenXR()
    {
        if (_handTrackingExt is not null)
        {
            if (_leftTracker.Handle  != 0) { _handTrackingExt.DestroyHandTracker(_leftTracker);  _leftTracker  = default; }
            if (_rightTracker.Handle != 0) { _handTrackingExt.DestroyHandTracker(_rightTracker); _rightTracker = default; }
            _handTrackingExt.Dispose(); _handTrackingExt = null;
        }
        if (_xr is not null)
        {
            if (_session.Handle  != 0) { _xr.DestroySession(_session);   _session  = default; }
            if (_instance.Handle != 0) { _xr.DestroyInstance(_instance); _instance = default; }
            _xr.Dispose(); _xr = null;
        }
        _xrReady = false;
    }

    private enum GestureSetting   { Threshold }
    private enum GestureParameter { GestureLeft, GestureRight }
    private enum GestureName      { None = 0, DoubleGun = 1, MiddleFinger = 2, PinkyFinger = 3 }
}
