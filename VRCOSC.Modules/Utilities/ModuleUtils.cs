// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Reflection;
using VRCOSC.App.SDK.Modules;

namespace Bluscream;

/// <summary>
/// Utility functions for VRCOSC modules
/// </summary>
public static class ModuleUtils
{
    private static MethodInfo? _sendParameterEnumMethod;
    private static MethodInfo? _sendParameterStringMethod;

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
}
