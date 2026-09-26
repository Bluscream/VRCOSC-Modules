// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// XR_EXT_hand_tracking: trackers, joint location and finger-curl estimation for OpenXRRuntime.

using System.Numerics;
using Silk.NET.OpenXR;
using Silk.NET.OpenXR.Extensions.EXT;

namespace VRCOSC.Modules.OpenXR;

internal sealed unsafe partial class OpenXRRuntime
{
    // XR_EXT_hand_tracking joint indices: metacarpal, proximal, intermediate, distal, tip.
    private static readonly int[][] FingerJoints =
    {
        new[] { 6, 7, 8, 9, 10 },     // index
        new[] { 11, 12, 13, 14, 15 }, // middle
        new[] { 16, 17, 18, 19, 20 }, // ring
        new[] { 21, 22, 23, 24, 25 }  // little
    };

    // A straight finger still reads a few degrees of total bend; a closed fist is roughly
    // 90 + 90 + 60 degrees across the three joints. Map that range onto 0..1.
    private const float CurlMinDegrees = 15f;
    private const float CurlRangeDegrees = 200f;

    private ExtHandTracking? _handTracking;
    private readonly HandTrackerEXT[] _trackers = new HandTrackerEXT[2];
    private readonly HandJointLocationEXT[] _jointBuffer = new HandJointLocationEXT[OpenXRHelper.HandJointCount];

    private void InitialiseHands(bool extensionEnabled, bool dataSourceEnabled)
    {
        if (!extensionEnabled) { Log("XR_EXT_hand_tracking not available; finger curls come from controller inputs only."); return; }

        if (!_xr!.TryGetInstanceExtension<ExtHandTracking>(null, _instance, out var ext) || ext is null)
        {
            Log("XR_EXT_hand_tracking enabled but its functions could not be loaded.");
            return;
        }
        _handTracking = ext;

        // With XR_EXT_hand_tracking_data_source we ask for both camera-tracked hands and
        // controller-synthesised joints, so curls keep flowing while controllers are held.
        var sources = stackalloc HandTrackingDataSourceEXT[2];
        sources[0] = HandTrackingDataSourceEXT.UnobstructedExt;
        sources[1] = HandTrackingDataSourceEXT.ControllerExt;
        var sourceInfo = new HandTrackingDataSourceInfoEXT
        {
            Type = StructureType.HandTrackingDataSourceInfoExt,
            RequestedDataSourceCount = 2,
            RequestedDataSources = sources
        };

        for (var i = 0; i < 2; i++)
        {
            var createInfo = new HandTrackerCreateInfoEXT
            {
                Type = StructureType.HandTrackerCreateInfoExt,
                Hand = i == 0 ? HandEXT.LeftExt : HandEXT.RightExt,
                HandJointSet = HandJointSetEXT.DefaultExt,
                Next = dataSourceEnabled ? &sourceInfo : null
            };

            HandTrackerEXT tracker = default;
            var r = ext.CreateHandTracker(_session, &createInfo, &tracker);
            if (r != Result.Success) { Log($"xrCreateHandTrackerEXT({(XrHand)i}) failed: {r}"); continue; }
            _trackers[i] = tracker;
        }

        Log($"Hand tracking ready (data-source extension: {dataSourceEnabled}).");
    }

    private void LocateHands(long time)
    {
        if (_handTracking is null || time == 0) return;

        for (var i = 0; i < 2; i++)
        {
            if (_trackers[i].Handle == 0) continue;

            var curls = LocateHand(_trackers[i], time);
            if (curls is null)
            {
                // Joints inactive: keep whatever SyncInput derived from the controller.
                if (_handInputs[i].Source == HandDataSource.HandTracking) _handInputs[i] = HandInput.Inactive;
                continue;
            }

            var previous = _handInputs[i];
            _handInputs[i] = new HandInput(
                IsActive: true,
                Source: HandDataSource.HandTracking,
                Index: curls.Value.Index,
                Middle: curls.Value.Middle,
                Ring: curls.Value.Ring,
                Pinky: curls.Value.Pinky,
                PrimaryTouch: previous.PrimaryTouch,
                SecondaryTouch: previous.SecondaryTouch,
                StickTouch: previous.StickTouch,
                PadTouch: previous.PadTouch);
        }
    }

    private (float Index, float Middle, float Ring, float Pinky)? LocateHand(HandTrackerEXT tracker, long time)
    {
        fixed (HandJointLocationEXT* joints = _jointBuffer)
        {
            var locations = new HandJointLocationsEXT
            {
                Type = StructureType.HandJointLocationsExt,
                JointCount = (uint)OpenXRHelper.HandJointCount,
                JointLocations = joints
            };
            var locateInfo = new HandJointsLocateInfoEXT
            {
                Type = StructureType.HandJointsLocateInfoExt,
                BaseSpace = _localSpace,
                Time = time
            };

            Phase("xrLocateHandJointsEXT");
            var r = _handTracking!.LocateHandJoints(tracker, &locateInfo, &locations);
            if (r != Result.Success) { LogOnce($"xrLocateHandJointsEXT failed: {r}"); return null; }
            if (locations.IsActive == 0) return null;

            return (
                FingerCurl(joints, FingerJoints[0]),
                FingerCurl(joints, FingerJoints[1]),
                FingerCurl(joints, FingerJoints[2]),
                FingerCurl(joints, FingerJoints[3]));
        }
    }

    /// <summary>Sum of the bend angles at the proximal, intermediate and distal joints, mapped to 0..1.</summary>
    private static float FingerCurl(HandJointLocationEXT* joints, int[] chain)
    {
        var total = 0f;
        for (var k = 0; k + 2 < chain.Length; k++)
        {
            if (!OpenXRHelper.IsValid(joints[chain[k]].LocationFlags)
                || !OpenXRHelper.IsValid(joints[chain[k + 1]].LocationFlags)
                || !OpenXRHelper.IsValid(joints[chain[k + 2]].LocationFlags))
                return 0f;

            var a = Direction(joints[chain[k]].Pose.Position, joints[chain[k + 1]].Pose.Position);
            var b = Direction(joints[chain[k + 1]].Pose.Position, joints[chain[k + 2]].Pose.Position);
            total += AngleDegrees(a, b);
        }

        return Math.Clamp((total - CurlMinDegrees) / CurlRangeDegrees, 0f, 1f);
    }

    private static Vector3 Direction(Vector3f from, Vector3f to)
        => Vector3.Normalize(new Vector3(to.X - from.X, to.Y - from.Y, to.Z - from.Z));

    private static float AngleDegrees(Vector3 a, Vector3 b)
    {
        if (float.IsNaN(a.X) || float.IsNaN(b.X)) return 0f;
        var dot = Math.Clamp(Vector3.Dot(a, b), -1f, 1f);
        return MathF.Acos(dot) * (180f / MathF.PI);
    }

    private void DestroyHands()
    {
        if (_handTracking is null) return;

        for (var i = 0; i < 2; i++)
        {
            if (_trackers[i].Handle == 0) continue;
            _handTracking.DestroyHandTracker(_trackers[i]);
            _trackers[i] = default;
        }

        _handTracking.Dispose();
        _handTracking = null;
    }
}
