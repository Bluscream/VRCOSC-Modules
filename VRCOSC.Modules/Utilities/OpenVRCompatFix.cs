// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Harmony patch that keeps VRCOSC's built-in OpenVR manager from crashing OpenVR shims that
// do not implement the whole IVRApplications interface (xrizer on WiVRn/Monado).

using System.Reflection;
using HarmonyLib;

namespace Bluscream.Modules.Utilities;

/// <summary>
/// VRCOSC's OpenVRManager calls IVRApplications.SetApplicationAutoLaunch on every slow update
/// once OpenVR is initialised. xrizer answers that call with a Rust panic
/// ("panicked at src/applications.rs: not yet implemented"), which takes the whole VRCOSC
/// process down with it. The call only registers VRCOSC for SteamVR's auto-launch, which
/// does not exist on those runtimes anyway, so it is skipped and reported as success.
/// </summary>
public static class OpenVRCompatFix
{
    private static readonly object Lock = new();
    private static bool _patched;

    public static void ApplySkipAutoLaunch(Action<string>? log = null)
    {
        lock (Lock)
        {
            if (_patched) return;
            _patched = true;

            try
            {
                var target = typeof(Valve.VR.CVRApplications).GetMethod(
                    nameof(Valve.VR.CVRApplications.SetApplicationAutoLaunch),
                    BindingFlags.Instance | BindingFlags.Public);

                if (target is null)
                {
                    log?.Invoke("[Bluscream] OpenVR compat: CVRApplications.SetApplicationAutoLaunch not found; nothing patched.");
                    return;
                }

                var harmony = new Harmony("com.bluscream.vrcosc.openvrcompat");
                var prefix = typeof(SetApplicationAutoLaunchPatch).GetMethod(nameof(SetApplicationAutoLaunchPatch.Prefix), BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                log?.Invoke("[Bluscream] OpenVR compat: SetApplicationAutoLaunch is skipped (xrizer does not implement it and panics).");
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Bluscream] Warning: failed to apply the OpenVR compat patch: {ex.Message}");
            }
        }
    }

    private static bool _initPatched;

    /// <summary>
    /// Stops VRCOSC's built-in OpenVR manager from initialising at all. Through xrizer that
    /// initialisation opens a second, full Vulkan OpenXR session on the runtime (VRCOSC is
    /// treated like a game) and then trips over IVRApplications calls xrizer has not
    /// implemented, which kills the process. The OpenXR modules cover what the built-in
    /// manager would have provided.
    /// </summary>
    public static void ApplyDisableOpenVR(Action<string>? log = null)
    {
        lock (Lock)
        {
            if (_initPatched) return;
            _initPatched = true;

            try
            {
                var helperType = Type.GetType("VRCOSC.App.OpenVR.OpenVRHelper, VRCOSC.App");
                var target = helperType?.GetMethod("InitialiseOpenVR", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (target is null)
                {
                    log?.Invoke("[Bluscream] OpenVR compat: OpenVRHelper.InitialiseOpenVR not found; built-in OpenVR left enabled.");
                    return;
                }

                var harmony = new Harmony("com.bluscream.vrcosc.openvrcompat.init");
                var prefix = typeof(InitialiseOpenVRPatch).GetMethod(nameof(InitialiseOpenVRPatch.Prefix), BindingFlags.Static | BindingFlags.Public);
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                log?.Invoke("[Bluscream] OpenVR compat: built-in OpenVR initialisation disabled (xrizer/WiVRn); the OpenXR modules provide VR data instead.");
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Bluscream] Warning: failed to disable built-in OpenVR: {ex.Message}");
            }
        }
    }

    public static class InitialiseOpenVRPatch
    {
        public static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    public static class SetApplicationAutoLaunchPatch
    {
        // Returning false skips the original; the caller sees "no error".
        public static bool Prefix(ref Valve.VR.EVRApplicationError __result)
        {
            __result = Valve.VR.EVRApplicationError.None;
            return false;
        }
    }
}
