// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using VRCOSC.App.SDK.Modules;

namespace Bluscream;

/// <summary>
/// Utility functions for VRCOSC modules
/// </summary>
public static class ModuleUtils
{
    private static MethodInfo? _sendParameterEnumMethod;
    private static MethodInfo? _sendParameterStringMethod;
    private static bool _resolverRegistered = false;
    private static readonly object _lockObj = new object();

    /// <summary>
    /// Registers a custom ALC native DLL resolver to load openxr_loader.dll from embedded resources.
    /// </summary>
    public static void RegisterNativeResolver()
    {
        lock (_lockObj)
        {
            if (_resolverRegistered) return;
            _resolverRegistered = true;

            try
            {
                var alc = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
                if (alc != null)
                {
                    alc.ResolvingUnmanagedDll += ResolveUnmanagedDll;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bluscream] Failed to register ALC native resolver: {ex}");
            }
        }
    }

    private static IntPtr ResolveUnmanagedDll(Assembly assembly, string dllName)
    {
        if (dllName.Equals("openxr_loader.dll", StringComparison.OrdinalIgnoreCase) || 
            dllName.Equals("openxr_loader", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var executingAssembly = Assembly.GetExecutingAssembly();
                string resourceName = "costura-win-x64.openxr_loader.dll";

                var resources = executingAssembly.GetManifestResourceNames();
                foreach (var res in resources)
                {
                    if (res.EndsWith("openxr_loader.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = res;
                        break;
                    }
                }

                using (var stream = executingAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return IntPtr.Zero;

                    string tempDir = Path.Combine(Path.GetTempPath(), "BluscreamVRCOSCModules");
                    Directory.CreateDirectory(tempDir);
                    string tempPath = Path.Combine(tempDir, "openxr_loader.dll");

                    if (!File.Exists(tempPath) || new FileInfo(tempPath).Length != stream.Length)
                    {
                        using (var fileStream = File.Create(tempPath))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }

                    if (NativeLibrary.TryLoad(tempPath, out var handle))
                    {
                        return handle;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Bluscream] Error resolving openxr_loader.dll: {ex}");
            }
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Safely send OSC parameter, ignoring InvalidOperationException when OSC client not connected
    /// Use this during module initialization when OSC might not be connected yet (e.g., auto-start)
    /// </summary>
    /// <param name="module">The module instance</param>
    /// <param name="lookup">Parameter enum lookup</param>
    /// <param name="value">Parameter value</param>
    /// <returns>True if parameter was sent, false if OSC not connected</returns>
    public static bool SendParameterSafe(VRCOSC.App.SDK.Modules.Module module, Enum lookup, object value)
    {
        try
        {
            // Use reflection to call protected SendParameter(Enum, object)
            _sendParameterEnumMethod ??= typeof(VRCOSC.App.SDK.Modules.Module).GetMethod(
                "SendParameter",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Enum), typeof(object) },
                null
            );

            _sendParameterEnumMethod?.Invoke(module, new object[] { lookup, value });
            return true;
        }
        catch (InvalidOperationException)
        {
            // OSC client not connected yet (e.g., during auto-start)
            return false;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        {
            // OSC client not connected yet (wrapped in TargetInvocationException)
            return false;
        }
    }

    /// <summary>
    /// Safely send OSC parameter by name, ignoring InvalidOperationException when OSC client not connected
    /// </summary>
    /// <param name="module">The module instance</param>
    /// <param name="name">Parameter name</param>
    /// <param name="value">Parameter value</param>
    /// <returns>True if parameter was sent, false if OSC not connected</returns>
    public static bool SendParameterSafe(VRCOSC.App.SDK.Modules.Module module, string name, object value)
    {
        try
        {
            // Use reflection to call protected SendParameter(string, object)
            _sendParameterStringMethod ??= typeof(VRCOSC.App.SDK.Modules.Module).GetMethod(
                "SendParameter",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(object) },
                null
            );

            _sendParameterStringMethod?.Invoke(module, new object[] { name, value });
            return true;
        }
        catch (InvalidOperationException)
        {
            // OSC client not connected yet (e.g., during auto-start)
            return false;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
        {
            // OSC client not connected yet (wrapped in TargetInvocationException)
            return false;
        }
    }

    /// <summary>
    /// Checks if the host VRCOSC application is in the 'Started' state (connected to VRChat).
    /// Uses reflection since AppManager is internal to VRCOSC.App.
    /// </summary>
    public static bool IsStarted()
    {
        try
        {
            var appManagerType = Type.GetType("VRCOSC.App.AppManager, VRCOSC.App");
            if (appManagerType is null)
            {
                return false;
            }
            var getInstance = appManagerType.GetMethod("GetInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (getInstance is null)
            {
                return false;
            }
            var appManager = getInstance.Invoke(null, null);
            if (appManager is null)
            {
                return false;
            }
            var stateProp = appManagerType.GetProperty("State");
            if (stateProp is null)
            {
                return false;
            }
            var stateObj = stateProp.GetValue(appManager);
            if (stateObj is null)
            {
                return false;
            }
            var valProp = stateObj.GetType().GetProperty("Value");
            if (valProp is null)
            {
                return false;
            }
            var val = valProp.GetValue(stateObj);
            var stateStr = val?.ToString() ?? "null";
            return stateStr == "Started";
        }
        catch (Exception)
        {
            return false;
        }
    }
}
