// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Tails the newest VRChat output_log_*.txt for the two facts the SDK's own log reader does not
// surface: whether we are the instance master, and the world id of the current instance
// (which the module turns into a capacity via the public VRChat world endpoint).
//
// Lines matched, quoted from a real log (output_log_2026-09-26_07-11-24.txt):
//   2026.09.26 07:11:37 Debug      -  [Behaviour] Joining wrld_19af8d89-f5ce-41b1-a06c-94a0e18e2e51:75008~hidden(usr_...)~region(eu)
//   2026.09.26 07:11:53 Debug      -  [Behaviour] Finished entering world.
//   2026.09.26 07:11:53 Debug      -  [Behaviour] I am MASTER
//   Actor Nr: 1
//   2026.09.26 07:18:59 Debug      -  [Behaviour] I am *NOT* MASTER
//   Actor Nr: 20
//   2026.09.26 07:18:58 Debug      -  [Behaviour] Configuring remote player VRCPlayer[Remote] 68387808 3
//   2026.09.26 07:18:59 Debug      -  [Behaviour] OnPlayerJoined ameisne231 (usr_39eef5b6-6d42-4e35-9acf-0c955f9bd465)
//   2026.09.26 07:21:45 Debug      -  [Behaviour] OnPlayerLeft ameisne231 (usr_39eef5b6-6d42-4e35-9acf-0c955f9bd465)
//   2026.09.26 09:27:29 Debug      -  [Behaviour] OnMasterClientSwitched
//   2026.09.26 07:18:42 Debug      -  [Behaviour] OnLeftRoom
//
// "I am MASTER" / "I am *NOT* MASTER" is authoritative on join. "OnMasterClientSwitched"
// names nobody, so after a switch the master is recomputed the Photon way: the lowest actor
// number still in the room. Own actor number comes from the "Actor Nr:" line; remote ones
// from "Configuring remote player ... <actor>" which precede the matching "OnPlayerJoined"
// lines in the same order, giving a name -> actor map that "OnPlayerLeft" then shrinks.

using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Bluscream.Modules.MCBParity;

internal sealed partial class VRChatLogTail
{
    private const string LogFilePattern = "output_log_*.txt";
    private const int VRChatSteamAppId = 438100;

    [GeneratedRegex(@"\[Behaviour\] Joining (wrld_[0-9a-fA-F-]+):")]
    private static partial Regex JoiningRegex();

    [GeneratedRegex(@"\[Behaviour\] Configuring remote player VRCPlayer\[Remote\] \d+ (\d+)\s*$")]
    private static partial Regex RemotePlayerRegex();

    [GeneratedRegex(@"\[Behaviour\] OnPlayerJoined (.+?) \(usr_[0-9a-fA-F-]+\)\s*$")]
    private static partial Regex PlayerJoinedRegex();

    [GeneratedRegex(@"\[Behaviour\] OnPlayerLeft (.+?) \(usr_[0-9a-fA-F-]+\)\s*$")]
    private static partial Regex PlayerLeftRegex();

    [GeneratedRegex(@"\[Behaviour\] Initialized PlayerAPI ""(.+?)"" is local")]
    private static partial Regex LocalPlayerRegex();

    [GeneratedRegex(@"^Actor Nr: (\d+)\s*$")]
    private static partial Regex ActorNrRegex();

    private readonly object _sync = new();
    private readonly Action<string> _log;
    private readonly Queue<int> _pendingRemoteActors = new();
    private readonly Dictionary<string, int> _remoteActors = new(StringComparer.Ordinal);

    private string? _directory;
    private string? _file;
    private long _offset;
    private bool _expectActorNr;
    private bool _masterSwitchPending;
    private int _ownActor = -1;
    private string _ownName = string.Empty;

    public bool InInstance { get; private set; }
    public bool IsMaster { get; private set; }
    public string WorldId { get; private set; } = string.Empty;
    public string? LogDirectory => _directory;

    public VRChatLogTail(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Picks the log directory (override first, then auto-detection) and resets state.</summary>
    public void Start(string? directoryOverride)
    {
        lock (_sync)
        {
            Reset();
            _directory = ResolveDirectory(directoryOverride);
            _log(_directory is null
                ? "VRChat log directory not found; set it in the module settings (Windows path as VRCOSC sees it, e.g. Z:\\...\\LocalLow\\VRChat\\VRChat)."
                : $"Tailing VRChat logs in {_directory}");
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            Reset();
            _directory = null;
        }
    }

    /// <summary>Reads any new lines. Cheap when nothing changed; call from a timer.</summary>
    public void Poll()
    {
        lock (_sync)
        {
            if (_directory is null || !System.IO.Directory.Exists(_directory)) return;

            try
            {
                var newest = System.IO.Directory.GetFiles(_directory, LogFilePattern)
                    .Select(f => new FileInfo(f))
                    .MaxBy(f => f.LastWriteTimeUtc)?.FullName;
                if (newest is null) return;

                if (newest != _file)
                {
                    Reset();
                    _file = newest;
                    _log($"Reading VRChat log {Path.GetFileName(newest)}");
                }

                using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < _offset) _offset = 0;
                stream.Seek(_offset, SeekOrigin.Begin);

                using var reader = new StreamReader(stream, Encoding.UTF8);
                while (reader.ReadLine() is { } line) HandleLine(line);
                _offset = stream.Position;
            }
            catch (IOException)
            {
                // VRChat holds the file open for writing; a transient share violation is fine.
            }
        }
    }

    private void Reset()
    {
        _file = null;
        _offset = 0;
        _expectActorNr = false;
        _masterSwitchPending = false;
        _ownActor = -1;
        _ownName = string.Empty;
        _pendingRemoteActors.Clear();
        _remoteActors.Clear();
        InInstance = false;
        IsMaster = false;
        WorldId = string.Empty;
    }

    private void HandleLine(string line)
    {
        if (_expectActorNr)
        {
            _expectActorNr = false;
            var actor = ActorNrRegex().Match(line);
            if (actor.Success) _ownActor = int.Parse(actor.Groups[1].Value);
        }

        if (line.EndsWith("[Behaviour] I am MASTER", StringComparison.Ordinal))
        {
            IsMaster = true;
            _masterSwitchPending = false;
            _expectActorNr = true;
            return;
        }

        if (line.EndsWith("[Behaviour] I am *NOT* MASTER", StringComparison.Ordinal))
        {
            IsMaster = false;
            _masterSwitchPending = false;
            _expectActorNr = true;
            return;
        }

        if (line.EndsWith("[Behaviour] OnMasterClientSwitched", StringComparison.Ordinal))
        {
            _masterSwitchPending = true; // resolved once the leaving player's OnPlayerLeft arrives
            return;
        }

        if (line.EndsWith("[Behaviour] Finished entering world.", StringComparison.Ordinal))
        {
            InInstance = true;
            return;
        }

        if (line.EndsWith("[Behaviour] OnLeftRoom", StringComparison.Ordinal))
        {
            InInstance = false;
            IsMaster = false;
            WorldId = string.Empty;
            _remoteActors.Clear();
            _pendingRemoteActors.Clear();
            return;
        }

        var joining = JoiningRegex().Match(line);
        if (joining.Success)
        {
            WorldId = joining.Groups[1].Value;
            _remoteActors.Clear();
            _pendingRemoteActors.Clear();
            return;
        }

        var remote = RemotePlayerRegex().Match(line);
        if (remote.Success)
        {
            _pendingRemoteActors.Enqueue(int.Parse(remote.Groups[1].Value));
            return;
        }

        var local = LocalPlayerRegex().Match(line);
        if (local.Success)
        {
            _ownName = local.Groups[1].Value;
            return;
        }

        var joined = PlayerJoinedRegex().Match(line);
        if (joined.Success)
        {
            var name = joined.Groups[1].Value;
            if (name != _ownName && _pendingRemoteActors.TryDequeue(out var actorNr)) _remoteActors[name] = actorNr;
            return;
        }

        var left = PlayerLeftRegex().Match(line);
        if (left.Success)
        {
            _remoteActors.Remove(left.Groups[1].Value);
            if (_masterSwitchPending) ResolveMasterAfterSwitch();
        }
    }

    private void ResolveMasterAfterSwitch()
    {
        _masterSwitchPending = false;

        if (_ownActor < 0)
        {
            _log("Master switched but own actor number is unknown; master status may be stale until the next instance join.");
            return;
        }

        // Photon hands the master role to the lowest actor number still in the room.
        var lowestRemote = _remoteActors.Count > 0 ? _remoteActors.Values.Min() : int.MaxValue;
        IsMaster = _ownActor < lowestRemote;
    }

    // ─────────────────────────── Directory discovery ───────────────────────────

    private static string? ResolveDirectory(string? directoryOverride)
    {
        foreach (var candidate in Candidates(directoryOverride))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && System.IO.Directory.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string> Candidates(string? directoryOverride)
    {
        if (!string.IsNullOrWhiteSpace(directoryOverride)) yield return directoryOverride.Trim();

        // Same location the SDK's VRChatLogReader uses: LocalLow of the prefix VRCOSC runs in.
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).Replace("Local", "LocalLow"), "VRChat", "VRChat");

        // VRCOSC in its own Wine prefix (or on a Linux host): VRChat's Proton prefix inside any Steam library.
        var compatSuffix = Path.Combine("steamapps", "compatdata", VRChatSteamAppId.ToString(), "pfx", "drive_c", "users", "steamuser", "AppData", "LocalLow", "VRChat", "VRChat");
        foreach (var library in SteamLibraries())
            yield return Path.Combine(library, compatSuffix);
    }

    /// <summary>Steam library roots as paths this process can open (Z:\... under Wine).</summary>
    private static IEnumerable<string> SteamLibraries()
    {
        var wine = Utilities.LinuxUtils.IsWineOnLinux;
        var home = wine ? Utilities.LinuxUtils.GetWineHomeDir() : Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
        if (string.IsNullOrEmpty(home)) yield break;

        var roots = new[] { Path.Combine(home, ".local", "share", "Steam"), Path.Combine(home, ".steam", "steam") };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            if (seen.Add(root)) yield return root;

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            foreach (var hostPath in ParseLibraryPaths(vdf))
            {
                var path = wine ? "Z:" + hostPath.Replace('/', '\\') : hostPath;
                if (seen.Add(path)) yield return path;
            }
        }
    }

    [GeneratedRegex(@"^\s*""path""\s+""(.+?)""\s*$")]
    private static partial Regex LibraryPathRegex();

    private static IEnumerable<string> ParseLibraryPaths(string vdf)
    {
        string[] lines;
        try { lines = File.ReadAllLines(vdf); }
        catch (IOException) { yield break; }
        catch (UnauthorizedAccessException) { yield break; }

        foreach (var line in lines)
        {
            var m = LibraryPathRegex().Match(line);
            if (m.Success) yield return m.Groups[1].Value.Replace("\\\\", "\\");
        }
    }
}
