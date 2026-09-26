// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Shared helpers for the OpenXR module suite.

using System.Runtime.InteropServices;
using System.Text;
using Silk.NET.OpenXR;

namespace VRCOSC.Modules.OpenXR;

/// <summary>Small, allocation-light helpers shared by <see cref="OpenXRRuntime"/> and the modules.</summary>
internal static unsafe class OpenXRHelper
{
    /// <summary>XR_MAKE_VERSION(1, 0, 0). Requested API version for the instance.</summary>
    public const ulong XrVersion10 = 1UL << 48;

    /// <summary>Total hand joints per XR_EXT_hand_tracking (XR_HAND_JOINT_COUNT_EXT).</summary>
    public const int HandJointCount = 26;

    /// <summary>Copies <paramref name="value"/> as NUL-terminated UTF-8 into a fixed-size buffer.</summary>
    public static void WriteUtf8(byte* dst, int maxLen, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var len = Math.Min(bytes.Length, maxLen - 1);
        for (var i = 0; i < len; i++) dst[i] = bytes[i];
        dst[len] = 0;
    }

    /// <summary>Reads a NUL-terminated UTF-8 string out of a fixed-size buffer.</summary>
    public static string ReadUtf8(byte* src, int maxLen)
    {
        var len = 0;
        while (len < maxLen && src[len] != 0) len++;
        return Encoding.UTF8.GetString(src, len);
    }

    public static void FillApplicationInfo(ref ApplicationInfo info, string appName)
    {
        fixed (byte* p = info.ApplicationName) WriteUtf8(p, 128, appName);
        fixed (byte* p = info.EngineName) WriteUtf8(p, 128, "VRCOSC");
    }

    public static void FillActionSetCreateInfo(ref ActionSetCreateInfo info, string name, string localName)
    {
        fixed (byte* p = info.ActionSetName) WriteUtf8(p, 64, name);
        fixed (byte* p = info.LocalizedActionSetName) WriteUtf8(p, 128, localName);
    }

    public static void FillActionCreateInfo(ref ActionCreateInfo info, string name, string localName)
    {
        fixed (byte* p = info.ActionName) WriteUtf8(p, 64, name);
        fixed (byte* p = info.LocalizedActionName) WriteUtf8(p, 128, localName);
    }

    /// <summary>Allocates NUL-terminated UTF-8 copies of <paramref name="strings"/> on the unmanaged heap.</summary>
    public static IntPtr[] AllocStringPointers(IReadOnlyList<string> strings)
    {
        var ptrs = new IntPtr[strings.Count];
        for (var i = 0; i < strings.Count; i++)
        {
            var bytes = Encoding.UTF8.GetBytes(strings[i] + '\0');
            var ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            ptrs[i] = ptr;
        }
        return ptrs;
    }

    public static void FreeStringPointers(IntPtr[] ptrs)
    {
        foreach (var p in ptrs) Marshal.FreeHGlobal(p);
    }

    /// <summary>Resolves an OpenXR path string (e.g. <c>/user/hand/left</c>) to its atom.</summary>
    public static ulong Path(XR xr, Instance instance, string path)
    {
        var bytes = Encoding.UTF8.GetBytes(path + '\0');
        ulong atom = 0;
        fixed (byte* p = bytes)
        {
            var r = xr.StringToPath(instance, p, &atom);
            if (r != Result.Success) return 0;
        }
        return atom;
    }

    /// <summary>Converts a path atom back to its string for logging; empty on failure.</summary>
    public static string PathToString(XR xr, Instance instance, ulong path)
    {
        if (path == 0) return string.Empty;
        var buffer = new byte[256];
        uint written = 0;
        fixed (byte* p = buffer)
        {
            if (xr.PathToString(instance, path, (uint)buffer.Length, &written, p) != Result.Success) return string.Empty;
        }
        return written <= 1 ? string.Empty : Encoding.UTF8.GetString(buffer, 0, (int)written - 1);
    }

    /// <summary>Identity pose, for reference-space creation.</summary>
    public static Posef IdentityPose => new()
    {
        Orientation = new Quaternionf(0f, 0f, 0f, 1f),
        Position = new Vector3f(0f, 0f, 0f)
    };

    public static bool IsTracked(SpaceLocationFlags flags)
        => (flags & SpaceLocationFlags.OrientationTrackedBit) != 0;

    public static bool IsValid(SpaceLocationFlags flags)
        => (flags & (SpaceLocationFlags.OrientationValidBit | SpaceLocationFlags.PositionValidBit))
           == (SpaceLocationFlags.OrientationValidBit | SpaceLocationFlags.PositionValidBit);
}

/// <summary>Which hand an action or tracker refers to. Values double as array indices.</summary>
internal enum XrHand
{
    Left = 0,
    Right = 1
}

/// <summary>Where the finger-curl values of a hand came from.</summary>
internal enum HandDataSource
{
    None,
    Controller,
    HandTracking
}

/// <summary>Per-hand input snapshot. Curl values are 0 (straight) to 1 (fully bent).</summary>
internal sealed record HandInput(
    bool IsActive,
    HandDataSource Source,
    float Index,
    float Middle,
    float Ring,
    float Pinky,
    bool PrimaryTouch,
    bool SecondaryTouch,
    bool StickTouch,
    bool PadTouch)
{
    public static readonly HandInput Inactive = new(false, HandDataSource.None, 0f, 0f, 0f, 0f, false, false, false, false);
}

/// <summary>Immutable snapshot of everything the runtime knows; replaced atomically each frame.</summary>
internal sealed record OpenXRSnapshot(
    bool RuntimeAvailable,
    bool SessionRunning,
    SessionState SessionState,
    string RuntimeName,
    string SystemName,
    float DisplayRefreshRate,
    bool HeadTracked,
    HandInput Left,
    HandInput Right,
    string LeftProfile,
    string RightProfile)
{
    public static readonly OpenXRSnapshot Empty = new(false, false, SessionState.Unknown, string.Empty, string.Empty, 0f, false,
        HandInput.Inactive, HandInput.Inactive, string.Empty, string.Empty);

    public HandInput Hand(XrHand hand) => hand == XrHand.Left ? Left : Right;
}
