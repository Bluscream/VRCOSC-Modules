// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;

namespace Bluscream;

public static class LogSpamFix
{
    private static bool _logSpamPatched;
    private static bool _filePickerPatched;
    private static bool _chatBoxValidationPatched;
    private static readonly object _lock = new();

    static LogSpamFix()
    {
        ApplyFix();
        ApplyFilePickerFix();
        ApplyChatBoxValidationFix();
    }

    public static void ApplyFix(Action<string>? log = null)
    {
        lock (_lock)
        {
            if (_logSpamPatched) return;
            _logSpamPatched = true;

            try
            {
                var harmony = new Harmony("com.bluscream.vrcosc.logspamfix");

                // Patch FastOSC.OSCSender.Send
                var oscSenderType = Type.GetType("FastOSC.OSCSender, FastOSC")
                    ?? Type.GetType("FastOSC.OSCSender, VRCOSC.App");

                if (oscSenderType != null)
                {
                    var sendMethod = oscSenderType.GetMethod("Send", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (sendMethod != null)
                    {
                        var finalizer = typeof(OSCSender_Send_Patch).GetMethod(nameof(OSCSender_Send_Patch.Finalizer), BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(sendMethod, finalizer: new HarmonyMethod(finalizer));
                        log?.Invoke("[Bluscream] Successfully patched FastOSC.OSCSender.Send to suppress ConnectAsync and Connection Refused log spam.");
                    }
                }

                // Patch Module.invokeMethod
                var moduleType = Type.GetType("VRCOSC.App.SDK.Modules.Module, VRCOSC.App");
                if (moduleType != null)
                {
                    var invokeMethod = moduleType.GetMethod("invokeMethod", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (invokeMethod != null)
                    {
                        var finalizer = typeof(Module_InvokeMethod_Patch).GetMethod(nameof(Module_InvokeMethod_Patch.Finalizer), BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(invokeMethod, finalizer: new HarmonyMethod(finalizer));
                        log?.Invoke("[Bluscream] Successfully patched Module.invokeMethod to suppress ConnectAsync and Connection Refused log spam.");
                    }
                }

                // Always apply ChatBox validation patch to prevent ChatBox wipes
                ApplyChatBoxValidationFix(log);
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Bluscream] Warning: Failed to apply LogSpamFix Harmony patch: {ex.Message}");
            }
        }
    }

    public static void ApplyFilePickerFix(Action<string>? log = null)
    {
        lock (_lock)
        {
            if (_filePickerPatched) return;
            _filePickerPatched = true;

            try
            {
                var harmony = new Harmony("com.bluscream.vrcosc.filepickerfix");
                var platformType = Type.GetType("VRCOSC.App.Utils.Platform, VRCOSC.App");
                if (platformType != null)
                {
                    var pickFileMethod = platformType.GetMethod("PickFileAsync", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pickFileMethod != null)
                    {
                        var prefix = typeof(PickFileAsync_Patch).GetMethod(nameof(PickFileAsync_Patch.Prefix), BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(pickFileMethod, prefix: new HarmonyMethod(prefix));
                        log?.Invoke("[Bluscream] Successfully patched Platform.PickFileAsync with WPF OpenFileDialog fallback for Proton/Wine.");
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Bluscream] Warning: Failed to apply FilePickerFix Harmony patch: {ex.Message}");
            }
        }
    }

    public static void ApplyChatBoxValidationFix(Action<string>? log = null)
    {
        lock (_lock)
        {
            if (_chatBoxValidationPatched) return;
            _chatBoxValidationPatched = true;

            try
            {
                var harmony = new Harmony("com.bluscream.vrcosc.chatboxvalidationfix");
                var serialiserType = Type.GetType("VRCOSC.App.ChatBox.Serialisation.ChatBoxValidationSerialiser, VRCOSC.App");
                if (serialiserType != null)
                {
                    var execMethod = serialiserType.GetMethod("ExecuteAfterDeserialisation", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (execMethod != null)
                    {
                        var postfix = typeof(ChatBoxValidation_Patch).GetMethod(nameof(ChatBoxValidation_Patch.Postfix), BindingFlags.Static | BindingFlags.Public);
                        harmony.Patch(execMethod, postfix: new HarmonyMethod(postfix));
                        log?.Invoke("[Bluscream] Successfully patched ChatBoxValidationSerialiser to prevent ChatBox timeline wipes on missing variables.");
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"[Bluscream] Warning: Failed to apply ChatBoxValidationFix patch: {ex.Message}");
            }
        }
    }
}

public static class OSCSender_Send_Patch
{
    public static Exception? Finalizer(Exception? __exception)
    {
        if (__exception == null) return null;

        var ex = __exception is TargetInvocationException tex && tex.InnerException != null ? tex.InnerException : __exception;

        if (ex is InvalidOperationException iex && iex.Message.Contains("Please call ConnectAsync first"))
        {
            return null; // Suppress exception when OSC is not connected
        }
        if (ex is System.Net.Sockets.SocketException sex && (sex.ErrorCode == 10061 || sex.Message.Contains("Connection refused")))
        {
            return null; // Suppress SocketException when OSC target port is not listening
        }
        return __exception;
    }
}

public static class Module_InvokeMethod_Patch
{
    public static Exception? Finalizer(Exception? __exception)
    {
        if (__exception != null)
        {
            var str = __exception.ToString();
            if (str.Contains("Please call ConnectAsync first") || str.Contains("Connection refused") || str.Contains("10061"))
            {
                return null; // Suppress log error when OSC target port is not listening
            }
        }
        return __exception;
    }
}

public static class ChatBoxValidation_Patch
{
    public static void Postfix(object __instance)
    {
        try
        {
            var field = __instance.GetType().GetProperty("IsValid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            field?.SetValue(__instance, true);
        }
        catch { }
    }
}

public static class PickFileAsync_Patch
{
    public static bool Prefix(string filter, ref Task<string?> __result)
    {
        try
        {
            var formattedFilter = NormalizeFilter(filter);
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = formattedFilter,
                        CheckFileExists = true
                    };
                    if (dialog.ShowDialog() == true)
                    {
                        tcs.SetResult(dialog.FileName);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            __result = tcs.Task;
            return false; // Skip original WinRT FileOpenPicker call
        }
        catch
        {
            return true; // Fallback to original if dialog fails
        }
    }

    private static string NormalizeFilter(string? rawFilter)
    {
        if (string.IsNullOrWhiteSpace(rawFilter))
            return "All files (*.*)|*.*";

        if (rawFilter.Contains('|'))
        {
            var parts = rawFilter.Split('|');
            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                return rawFilter;
        }

        var clean = rawFilter.Trim().TrimStart('.');
        if (!clean.StartsWith("*."))
        {
            clean = "*." + clean;
        }

        return $"Files ({clean})|{clean}|All files (*.*)|*.*";
    }
}
