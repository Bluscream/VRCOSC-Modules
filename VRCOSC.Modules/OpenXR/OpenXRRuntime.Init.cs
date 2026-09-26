// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Instance, system, session and space creation for OpenXRRuntime.

using Silk.NET.Core;
using Silk.NET.OpenXR;

namespace VRCOSC.Modules.OpenXR;

internal sealed unsafe partial class OpenXRRuntime
{
    // ─────────────────────────── Initialisation ───────────────────────────

    private bool TryInitialise()
    {
        _xr ??= XR.GetApi();

        var available = EnumerateExtensions();
        if (available is null) return false;

        if (!available.Contains(ExtHeadless))
        {
            LogOnce($"Runtime does not support {ExtHeadless}; a session without a graphics binding is impossible. " +
                    $"Available: {string.Join(", ", available)}");
            return false;
        }

        var wanted = new List<string> { ExtHeadless };
        if (UseOverlaySession && available.Contains(ExtOverlay)) wanted.Add(ExtOverlay);
        if (available.Contains(ExtHandTrackingName)) wanted.Add(ExtHandTrackingName);
        if (available.Contains(ExtHandTrackingDataSource) && available.Contains(ExtHandTrackingName)) wanted.Add(ExtHandTrackingDataSource);
        if (available.Contains(ExtDisplayRefreshRate)) wanted.Add(ExtDisplayRefreshRate);
        if (available.Contains(ExtWin32Time)) wanted.Add(ExtWin32Time);

        if (!CreateInstance(wanted)) return false;
        LogOnce($"OpenXR instance created with: {string.Join(", ", wanted)}");

        if (!QuerySystem()) return false;
        if (!CreateSession(wanted.Contains(ExtOverlay))) return false;
        if (!CreateSpaces()) return false;

        ResolveExtensionFunctions(wanted);
        InitialiseInput();
        InitialiseHands(wanted.Contains(ExtHandTrackingName), wanted.Contains(ExtHandTrackingDataSource));

        _sinceLastFrame.Restart();
        _lastPredictedTime = 0;
        // Monado's headless sessions have no frame loop: xrWaitFrame answers once, then
        // xrBeginFrame/xrEndFrame return CALL_ORDER_INVALID and the next xrWaitFrame blocks
        // forever. When the runtime can convert QPC to XrTime we never touch the frame calls.
        _waitFrameWorks = _convertWin32Time is null;
        _probedWaitFrame = false;
        if (!_waitFrameWorks) Log("Using xrConvertWin32PerformanceCounterToTimeKHR for timing; frame calls are skipped (headless).");
        Log($"OpenXR ready: runtime '{_runtimeName}', system '{_systemName}', overlay={_overlaySession}. Waiting for the session to become ready.");
        return true;
    }

    private HashSet<string>? EnumerateExtensions()
    {
        uint count = 0;
        var r = _xr!.EnumerateInstanceExtensionProperties((byte*)null, 0, &count, null);
        if (r != Result.Success)
        {
            LogThrottled("enum", $"xrEnumerateInstanceExtensionProperties failed: {r} (no OpenXR runtime registered, or the headset is not connected yet). Retrying every 5 s.", TimeSpan.FromSeconds(60));
            return null;
        }

        var props = new ExtensionProperties[count];
        for (var i = 0; i < props.Length; i++) props[i].Type = StructureType.ExtensionProperties;

        fixed (ExtensionProperties* p = props)
            r = _xr.EnumerateInstanceExtensionProperties((byte*)null, count, &count, p);

        if (r != Result.Success)
        {
            LogThrottled("enum-fill", $"xrEnumerateInstanceExtensionProperties (fill) failed: {r}", TimeSpan.FromSeconds(60));
            return null;
        }

        var set = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            fixed (byte* name = props[i].ExtensionName)
                set.Add(OpenXRHelper.ReadUtf8(name, 128));
        }

        LogOnce($"OpenXR runtime extensions: {string.Join(", ", set.Order())}");
        return set;
    }

    private bool CreateInstance(IReadOnlyList<string> extensions)
    {
        var appInfo = new ApplicationInfo { ApplicationVersion = 1, EngineVersion = 1, ApiVersion = OpenXRHelper.XrVersion10 };
        OpenXRHelper.FillApplicationInfo(ref appInfo, AppName);

        var extPtrs = OpenXRHelper.AllocStringPointers(extensions);
        try
        {
            fixed (IntPtr* pp = extPtrs)
            {
                var createInfo = new InstanceCreateInfo
                {
                    Type = StructureType.InstanceCreateInfo,
                    ApplicationInfo = appInfo,
                    EnabledExtensionCount = (uint)extensions.Count,
                    EnabledExtensionNames = (byte**)pp
                };

                Instance instance = default;
                var r = _xr!.CreateInstance(&createInfo, &instance);
                if (r != Result.Success)
                {
                    LogThrottled("create-instance", $"xrCreateInstance failed: {r}", TimeSpan.FromSeconds(60));
                    return false;
                }
                _instance = instance;
            }
        }
        finally
        {
            OpenXRHelper.FreeStringPointers(extPtrs);
        }

        var props = new InstanceProperties { Type = StructureType.InstanceProperties };
        if (_xr!.GetInstanceProperties(_instance, &props) == Result.Success)
            _runtimeName = OpenXRHelper.ReadUtf8(props.RuntimeName, 128);

        return true;
    }

    private bool QuerySystem()
    {
        var getInfo = new SystemGetInfo { Type = StructureType.SystemGetInfo, FormFactor = FormFactor.HeadMountedDisplay };
        ulong systemId = 0;
        var r = _xr!.GetSystem(_instance, &getInfo, &systemId);
        if (r != Result.Success)
        {
            LogThrottled("get-system", $"xrGetSystem failed: {r} (is the headset connected?)", TimeSpan.FromSeconds(60));
            return false;
        }
        _systemId = systemId;

        var props = new SystemProperties { Type = StructureType.SystemProperties };
        if (_xr.GetSystemProperties(_instance, _systemId, &props) == Result.Success)
            _systemName = OpenXRHelper.ReadUtf8(props.SystemName, 256);

        return true;
    }

    private bool CreateSession(bool overlay)
    {
        var overlayInfo = new SessionCreateInfoOverlayEXTX
        {
            Type = StructureType.SessionCreateInfoOverlayExtx,
            CreateFlags = 0,
            SessionLayersPlacement = 0
        };

        var createInfo = new SessionCreateInfo
        {
            Type = StructureType.SessionCreateInfo,
            SystemId = _systemId,
            Next = overlay ? &overlayInfo : null
        };

        Session session = default;
        var r = _xr!.CreateSession(_instance, &createInfo, &session);
        if (r != Result.Success && overlay)
        {
            Log($"xrCreateSession as overlay failed: {r}; retrying as a plain headless session.");
            createInfo.Next = null;
            r = _xr.CreateSession(_instance, &createInfo, &session);
            overlay = false;
        }

        if (r != Result.Success)
        {
            LogThrottled("create-session", $"xrCreateSession failed: {r}. The runtime accepted {ExtHeadless} but refused a session without a graphics binding.", TimeSpan.FromSeconds(60));
            return false;
        }

        _session = session;
        _overlaySession = overlay;
        return true;
    }

    private bool CreateSpaces()
    {
        var local = new ReferenceSpaceCreateInfo
        {
            Type = StructureType.ReferenceSpaceCreateInfo,
            ReferenceSpaceType = ReferenceSpaceType.Local,
            PoseInReferenceSpace = OpenXRHelper.IdentityPose
        };
        var view = local;
        view.ReferenceSpaceType = ReferenceSpaceType.View;

        Space localSpace = default, viewSpace = default;
        var r = _xr!.CreateReferenceSpace(_session, &local, &localSpace);
        if (r != Result.Success) { LogOnce($"xrCreateReferenceSpace(LOCAL) failed: {r}"); return false; }
        r = _xr.CreateReferenceSpace(_session, &view, &viewSpace);
        if (r != Result.Success) { LogOnce($"xrCreateReferenceSpace(VIEW) failed: {r}"); return false; }

        _localSpace = localSpace;
        _viewSpace = viewSpace;
        return true;
    }

    private void ResolveExtensionFunctions(IReadOnlyCollection<string> enabled)
    {
        _getDisplayRefreshRate = null;
        _convertWin32Time = null;

        if (enabled.Contains(ExtDisplayRefreshRate))
        {
            var fn = GetProc("xrGetDisplayRefreshRateFB");
            if (fn != 0) _getDisplayRefreshRate = (delegate* unmanaged[Cdecl]<Session, float*, Result>)fn;
        }

        if (enabled.Contains(ExtWin32Time))
        {
            var fn = GetProc("xrConvertWin32PerformanceCounterToTimeKHR");
            if (fn != 0) _convertWin32Time = (delegate* unmanaged[Cdecl]<Instance, long*, long*, Result>)fn;
        }
    }

    private nint GetProc(string name)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(name + '\0');
        PfnVoidFunction fn = default;
        fixed (byte* p = bytes)
        {
            if (_xr!.GetInstanceProcAddr(_instance, p, &fn) != Result.Success)
            {
                LogOnce($"xrGetInstanceProcAddr({name}) failed");
                return 0;
            }
        }
        return (nint)fn.Handle;
    }
}
