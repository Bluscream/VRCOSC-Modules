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

    /// <summary>
    /// The user's home directory expressed as a path Wine can open — Wine maps drive Z: to
    /// the filesystem root, so <c>/home/blu</c> becomes <c>Z:\home\blu</c>.
    /// </summary>
    /// <remarks>
    /// Do NOT write <c>"Z:" + homeDir</c>. When HOME is unset, which it is under Wine,
    /// the fallback (<see cref="Environment.SpecialFolder.UserProfile"/>) already returns a
    /// Windows path such as <c>C:\users\steamuser</c>. Concatenating yields
    /// <c>Z:C:\users\steamuser</c>, and <c>Z:foo</c> in Windows path syntax means "foo
    /// relative to the current directory on drive Z" — so instead of failing, it silently
    /// creates a literal <c>C:\users\...</c> tree inside whatever the process's working
    /// directory happens to be. That produced a stray `C:` folder in the repo, which
    /// `git add -A` would then have committed.
    /// </remarks>
    public static string GetWineHomeDir()
    {
        var home = Environment.GetEnvironmentVariable("HOME");

        // Only a POSIX path needs translating; anything else is already Windows-shaped.
        if (!string.IsNullOrEmpty(home) && home.StartsWith('/'))
            return "Z:" + home.Replace('/', '\\');

        if (System.IO.Directory.Exists(@"Z:\home"))
        {
            try
            {
                var dirs = System.IO.Directory.GetDirectories(@"Z:\home");
                if (dirs.Length > 0) return dirs[0];
            }
            catch { }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

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

    private static bool? isFlatpak;
    public static bool IsFlatpakSandbox
        => isFlatpak ??= System.IO.File.Exists("/.flatpak-info") || System.IO.File.Exists("Z:\\.flatpak-info");

    public static string WrapHostCommand(string command)
        => IsFlatpakSandbox ? $"flatpak-spawn --host {command}" : command;

    /// <summary>
    /// Runs a command on the Linux host via <c>flatpak-spawn --host</c> (if in Flatpak) or directly inside Wine.
    /// The command string should be the host-side command (e.g. "upower -e").
    /// Fire-and-forget: does not capture output.
    /// </summary>
    public static void RunHost(string command, Action<Exception>? onError = null)
        => RunWine(WrapHostCommand(command), onError);

    /// <summary>
    /// Runs one of this package's helper scripts from the host's <c>~/.local/bin</c>.
    ///
    /// The path is resolved by the *host* shell expanding <c>$HOME</c>, not by this
    /// process: we run inside Wine, so our own HOME is the Wine user's home and would
    /// point somewhere else entirely. Letting the host expand it keeps the call correct
    /// for any user on any machine — no username is baked in.
    /// </summary>
    public static void RunHostScript(string scriptName, string? arguments = null, Action<Exception>? onError = null)
    {
        var argumentSuffix = string.IsNullOrWhiteSpace(arguments) ? string.Empty : $" {arguments}";
        RunHost($"sh -c '\"$HOME/.local/bin/{scriptName}\"{argumentSuffix}'", onError);
    }

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
        => RunShell(WrapHostCommand(command), timeoutMs);

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
    /// <param name="processName">Process name, or a command-line pattern when
    /// <paramref name="matchFullCommandLine"/> is set.</param>
    /// <param name="matchFullCommandLine">
    /// Use <c>pgrep -f</c> instead of <c>pgrep -x</c>. Required for anything launched
    /// through Wine or Proton: a Windows game shows up as a wine loader process, so an
    /// exact-name match on e.g. "VRChat" never hits, while <c>-f "VRChat.exe"</c> does.
    /// </param>
    public static bool IsHostProcessRunning(string processName, bool matchFullCommandLine = false)
        => !string.IsNullOrWhiteSpace(GetHostProcessId(processName, matchFullCommandLine));

    /// <summary>
    /// PID of the first host process matching <paramref name="processName"/>, or null.
    /// See <see cref="IsHostProcessRunning"/> for when to set
    /// <paramref name="matchFullCommandLine"/>.
    /// </summary>
    public static string? GetHostProcessId(string processName, bool matchFullCommandLine = false)
    {
        var flag = matchFullCommandLine ? "-f" : "-x";
        var output = RunShellHost($"pgrep {flag} \"{processName}\"");
        if (string.IsNullOrWhiteSpace(output)) return null;

        // pgrep prints one pid per line; take the first.
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
    }

    /// <summary>
    /// Reads a file from the Linux host — <c>/proc</c>, <c>/sys</c>, or anything else the
    /// Wine side cannot see directly. Returns an empty string on failure.
    /// </summary>
    public static string ReadHostFile(string path, int timeoutMs = 5000)
        => RunShellHost($"cat \"{path}\"", timeoutMs);

    /// <summary>
    /// Sends a desktop notification through <c>notify-send</c> on the host.
    /// </summary>
    /// <remarks>
    /// Windows toast notifications raised from inside the prefix surface into Wine rather
    /// than the real desktop, so on Linux this is the path that actually notifies the user.
    /// </remarks>
    public static void NotifySend(string title, string body, string urgency = "normal",
                                  Action<Exception>? onError = null)
    {
        static string Q(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        RunHost($"notify-send -u {urgency} \"{Q(title)}\" \"{Q(body)}\"", onError);
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

        RunWine($"{WrapHostCommand(command)} > \"{hostPath}\" 2>/dev/null");

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
