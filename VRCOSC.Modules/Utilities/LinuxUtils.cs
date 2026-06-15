// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Linux-specific shell and process utilities shared across all Bluscream modules.
//
// Two runtime contexts exist depending on where code runs:
//
//   WINE context  – VRCOSC itself runs under Wine/Proton on Linux.
//                   To reach the host OS you must go via Z:\bin\bash (Wine path)
//                   or via flatpak-spawn --host when inside a Flatpak sandbox.
//                   Output capture is unreliable here; use fire-and-forget style.
//
//   Native context – Code running on the actual Linux host (e.g. distrobox,
//                    background scripts). Plain /bin/bash with redirected stdout.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Bluscream.Modules.Utilities;

/// <summary>
/// Linux shell and process utilities for Bluscream VRCOSC modules.
/// </summary>
public static class LinuxUtils
{
    // ── Platform guard ────────────────────────────────────────────────
    /// <summary>Returns true when running on a Linux host.</summary>
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    // ═══════════════════════════════════════════════════════════════════
    //  WINE CONTEXT — called from inside VRCOSC (Wine/Proton process)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs a bash command via the Wine bash bridge (<c>Z:\bin\bash -c "…"</c>).
    /// Fire-and-forget: waits for exit but does not capture output.
    /// Safe to call from Wine-hosted VRCOSC modules.
    /// </summary>
    public static void RunWine(string command, Action<Exception>? onError = null)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName        = "Z:\\bin\\bash",
                Arguments       = $"-c \"{EscapeQuotes(command)}\"",
                UseShellExecute = true,
                CreateNoWindow  = true,
                WorkingDirectory = "C:\\"
            });

            if (process is null) return;

            try   { process.WaitForExit(); }
            catch (InvalidOperationException) { /* Wine may detach from child processes */ }
        }
        catch (Exception ex) { onError?.Invoke(ex); }
    }

    /// <summary>
    /// Runs a command on the Linux host via <c>flatpak-spawn --host</c> inside Wine.
    /// The command string should be the host-side command (e.g. "upower -e").
    /// Fire-and-forget: does not capture output.
    /// </summary>
    public static void RunHost(string command, Action<Exception>? onError = null)
        => RunWine($"flatpak-spawn --host {command}", onError);

    // ═══════════════════════════════════════════════════════════════════
    //  NATIVE CONTEXT — direct /bin/bash, captures stdout
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs a bash command natively (requires <c>/bin/bash</c> to exist).
    /// Captures and returns stdout. Blocks for up to <paramref name="timeoutMs"/> ms.
    /// </summary>
    public static string RunShell(string command, int timeoutMs = 5000)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName               = "/bin/bash",
                Arguments              = $"-c \"{EscapeQuotes(command)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = false,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(timeoutMs);
            return output.Trim();
        }
        catch { return string.Empty; }
    }

    /// <summary>
    /// Runs a command on the Linux host via <c>flatpak-spawn --host</c> natively,
    /// capturing stdout. Use from native/distrobox contexts.
    /// </summary>
    public static string RunShellHost(string command, int timeoutMs = 5000)
        => RunShell($"flatpak-spawn --host {command}", timeoutMs);

    // ═══════════════════════════════════════════════════════════════════
    //  UPower battery helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Represents a parsed UPower device entry.</summary>
    public sealed class UPowerDevice
    {
        public string Path          { get; init; } = string.Empty;
        public bool   IsPresent     { get; set; }
        public bool   IsCharging    { get; set; }
        public float  BatteryLevel  { get; set; }   // 0-1
        public string DeviceType    { get; set; } = string.Empty;
    }

    /// <summary>
    /// Enumerates UPower devices matching <paramref name="filter"/> via
    /// <c>flatpak-spawn --host upower</c> in the Wine bash context.
    /// Returns an empty list when UPower is not available or not on Linux.
    /// </summary>
    /// <param name="filter">
    /// Optional path substring filter, e.g. "headset", "controller", "input".
    /// Pass <see langword="null"/> to return all devices.
    /// </param>
    /// <param name="useNative">
    /// When <see langword="true"/>, uses the native <c>/bin/bash</c> path (distrobox).
    /// When <see langword="false"/> (default), uses the Wine bridge.
    /// </param>
    public static List<UPowerDevice> GetUPowerDevices(
        string? filter    = null,
        bool    useNative = false)
    {
        var results = new List<UPowerDevice>();
        if (!IsLinux) return results;

        try
        {
            var raw = useNative
                ? RunShell("flatpak-spawn --host upower -e 2>/dev/null")
                : RunShellViaWine("upower -e 2>/dev/null");

            foreach (var path in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (filter is not null &&
                    !path.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                var info = useNative
                    ? RunShell($"flatpak-spawn --host upower -i \"{path}\" 2>/dev/null")
                    : RunShellViaWine($"upower -i \"{path}\" 2>/dev/null");

                var device = ParseUPowerInfo(path, info);
                results.Add(device);
            }
        }
        catch { /* UPower not available */ }

        return results;
    }

    // ── UPower parse helper ───────────────────────────────────────────
    private static UPowerDevice ParseUPowerInfo(string path, string info)
    {
        var device = new UPowerDevice { Path = path };

        foreach (var raw in info.Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith("percentage:", StringComparison.OrdinalIgnoreCase))
            {
                var val = line.Split(':')[1].Trim().TrimEnd('%');
                if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f))
                    device.BatteryLevel = f / 100f;
            }
            else if (line.StartsWith("state:", StringComparison.OrdinalIgnoreCase))
            {
                device.IsCharging = line.Contains("charging", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("present:", StringComparison.OrdinalIgnoreCase))
            {
                device.IsPresent = line.Contains("yes", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("type:", StringComparison.OrdinalIgnoreCase))
            {
                device.DeviceType = line.Split(':')[1].Trim();
            }
        }

        return device;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Process management helpers (host-side)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts a process on the Linux host via <c>flatpak-spawn --host</c>
    /// in a fire-and-forget manner (redirected to /dev/null, backgrounded).
    /// </summary>
    public static void StartHostProcess(string processName, Action<Exception>? onError = null)
        => RunHost($"{processName} >/dev/null 2>&1 &", onError);

    /// <summary>
    /// Stops a process on the Linux host by name using <c>killall</c> / <c>pkill -f</c>.
    /// </summary>
    public static void StopHostProcess(string processName, Action<Exception>? onError = null)
        => RunHost($"killall {processName} || pkill -f {processName}", onError);

    /// <summary>
    /// Returns whether a named process is running on the Linux host.
    /// Uses <c>pgrep</c> via <c>flatpak-spawn --host</c>.
    /// </summary>
    public static bool IsHostProcessRunning(string processName)
    {
        var output = RunShellHost($"pgrep -x {processName}");
        return !string.IsNullOrWhiteSpace(output);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Internal helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs a command via the Wine bash bridge and captures stdout
    /// by writing to a temp file (since Wine's UseShellExecute=true
    /// does not support stdout redirection).
    /// </summary>
    private static string RunShellViaWine(string command, int timeoutMs = 5000)
    {
        // Write output to a temp file readable by both Wine and host
        var hostPath = $"/tmp/.bluscream_linuxutils_{Guid.NewGuid():N}.txt";
        var winePath = "Z:" + hostPath.Replace('/', '\\');

        RunWine($"flatpak-spawn --host {command} > \"{hostPath}\" 2>/dev/null");

        // Give it a moment to complete, then read the file from the Wine path
        System.Threading.Thread.Sleep(Math.Min(timeoutMs, 1000));

        try
        {
            if (System.IO.File.Exists(winePath))
            {
                var content = System.IO.File.ReadAllText(winePath).Trim();
                try { System.IO.File.Delete(winePath); } catch { }
                return content;
            }
        }
        catch { }

        return string.Empty;
    }

    /// <summary>Escapes double-quotes for embedding in a bash -c "…" argument.</summary>
    private static string EscapeQuotes(string s) => s.Replace("\"", "\\\"");

    // ═══════════════════════════════════════════════════════════════════
    //  Chmod / file helpers
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Makes a file executable on the Linux host via <c>chmod +x</c>.</summary>
    public static void ChmodPlusX(string hostPath, Action<Exception>? onError = null)
        => RunHost($"chmod +x \"{hostPath}\"", onError);
}
