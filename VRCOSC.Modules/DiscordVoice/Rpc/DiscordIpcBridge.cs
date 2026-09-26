// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Deploys and starts vrcosc_discord_ipc_bridge.sh on the Linux host (LinuxHardwareStats
// pattern): Wine's \\.\pipe\discord-ipc-N are Wine-internal and never reach the Unix socket
// the native Discord client listens on, so the script forwards a localhost TCP port to that
// socket with socat and the module connects over TCP instead.

using System.IO;
using Bluscream.Modules.Utilities;

namespace Bluscream.Modules.DiscordVoice.Rpc;

public static class DiscordIpcBridge
{
    public const int DefaultPort = 6890;
    private const string ScriptName = "vrcosc_discord_ipc_bridge.sh";
    private const string ResourceName = "Bluscream.Modules.DiscordVoice." + ScriptName;

    /// <summary>Writes the script to the host's ~/.local/bin (if missing or stale) and runs it. Never throws.</summary>
    public static async Task<bool> EnsureRunningAsync(int port, Action<string> log, CancellationToken ct)
    {
        try
        {
            if (!Deploy(log)) return false;
            LinuxUtils.RunHostScript(ScriptName, port.ToString(), ex => log($"IPC bridge script failed to launch: {ex.Message}"));
            // socat needs a moment to bind before the TCP connect below.
            await Task.Delay(750, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            log($"IPC bridge setup failed: {ex.Message}");
            return false;
        }
    }

    private static bool Deploy(Action<string> log)
    {
        using var stream = typeof(DiscordIpcBridge).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            log($"Embedded resource {ResourceName} is missing.");
            return false;
        }
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        var target = Path.Combine(LinuxUtils.GetWineHomeDir(), ".local", "bin", ScriptName);
        var dir = Path.GetDirectoryName(target);
        if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        if (!File.Exists(target) || File.ReadAllText(target) != content)
        {
            File.WriteAllText(target, content);
            LinuxUtils.ChmodPlusX($"$HOME/.local/bin/{ScriptName}", ex => log($"chmod +x failed: {ex.Message}"));
            log($"Deployed {ScriptName} to ~/.local/bin.");
        }
        return true;
    }
}
