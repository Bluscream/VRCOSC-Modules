// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Host-side device probe: battery and application focus via libmonado (WiVRn / Monado).

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluscream.Modules.Utilities;

namespace VRCOSC.Modules.OpenXR;

/// <summary>Battery state of one tracked device as reported by libmonado.</summary>
internal sealed record DeviceBattery(bool Present, bool Charging, float Charge)
{
    public static readonly DeviceBattery None = new(false, false, 0f);
}

/// <summary>One OpenXR client (application) known to the runtime.</summary>
internal sealed record RuntimeClient(string Name, bool IsPrimary, bool IsActive, bool IsVisible, bool IsFocused, bool IsOverlay);

/// <summary>Parsed output of <c>vrcosc_xr_query.sh</c>.</summary>
internal sealed record DeviceProbeResult(
    bool Ok,
    string Error,
    DateTime Timestamp,
    DeviceBattery Head,
    DeviceBattery Left,
    DeviceBattery Right,
    IReadOnlyList<RuntimeClient> Clients)
{
    public static readonly DeviceProbeResult Empty = new(false, "not polled yet", DateTime.MinValue,
        DeviceBattery.None, DeviceBattery.None, DeviceBattery.None, Array.Empty<RuntimeClient>());

    /// <summary>The primary (non-overlay, focused-capable) application, typically the game.</summary>
    public RuntimeClient? PrimaryApp => Clients.FirstOrDefault(c => c.IsPrimary) ?? Clients.FirstOrDefault(c => !c.IsOverlay && c.IsActive);
}

/// <summary>
/// Runs the libmonado probe script on the Linux host and parses its JSON. OpenXR has no
/// battery API and a session only learns about its own focus, so both come from the
/// runtime's admin interface, which lives in a host shared library that Wine cannot load.
/// Uses the same deploy-and-read pattern as the Linux hardware-stats module.
/// </summary>
internal sealed class OpenXRDeviceProbe
{
    public static OpenXRDeviceProbe Shared { get; } = new();

    private const string ScriptName = "vrcosc_xr_query.sh";
    private const string ResourceName = "Bluscream.Modules.OpenXR.vrcosc_xr_query.sh";
    private const string OutputName = ".vrcosc_openxr.json";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly object _sync = new();
    private bool _deployed;
    private bool _launchInFlight;
    private DateTime _lastLaunch = DateTime.MinValue;
    private DateTime _lastFileWrite = DateTime.MinValue;
    private string _lastError = string.Empty;

    public DeviceProbeResult Latest { get; private set; } = DeviceProbeResult.Empty;

    /// <summary>True when running inside Wine on a Linux host, where the probe can work at all.</summary>
    public static bool IsSupported => LinuxUtils.IsLinux || LinuxUtils.IsWineOnLinux;

    /// <summary>Extracts the script to the host's <c>~/.local/bin</c>. Idempotent.</summary>
    public void Deploy(Action<string> log)
    {
        lock (_sync)
        {
            if (_deployed || !IsSupported) return;

            try
            {
                using var stream = typeof(OpenXRDeviceProbe).Assembly.GetManifestResourceStream(ResourceName);
                if (stream is null) { log($"Embedded resource {ResourceName} is missing; battery/focus probe disabled."); return; }

                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();

                var target = Path.Combine(LinuxUtils.GetWineHomeDir(), ".local", "bin", ScriptName);
                var dir = Path.GetDirectoryName(target);
                if (dir is not null) Directory.CreateDirectory(dir);

                if (!File.Exists(target) || File.ReadAllText(target) != content)
                {
                    File.WriteAllText(target, content);
                    log($"Deployed {ScriptName} to ~/.local/bin on the host.");
                }

                LinuxUtils.ChmodPlusX($"$HOME/.local/bin/{ScriptName}", ex => log($"chmod +x {ScriptName} failed: {ex.Message}"));
                _deployed = true;
            }
            catch (IOException ex) { log($"Could not deploy {ScriptName}: {ex.Message}"); }
            catch (UnauthorizedAccessException ex) { log($"Could not deploy {ScriptName}: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Launches the probe if the poll interval has elapsed, then reads whatever result file is
    /// present. The launch runs on the thread pool: the Wine bridge waits for the script to
    /// exit, and the script legitimately takes up to its timeout while no headset is
    /// connected, which must not stall the module's update loop.
    /// </summary>
    public void Poll(Action<string> log)
    {
        if (!_deployed) return;

        lock (_sync)
        {
            var now = DateTime.UtcNow;
            if (!_launchInFlight && now - _lastLaunch >= PollInterval)
            {
                _lastLaunch = now;
                _launchInFlight = true;
                _ = Task.Run(() =>
                {
                    try { LinuxUtils.RunHostScript(ScriptName, null, ex => log($"Launching {ScriptName} failed: {ex.Message}")); }
                    finally { lock (_sync) _launchInFlight = false; }
                });
            }

            ReadResult(log);
        }
    }

    private void ReadResult(Action<string> log)
    {
        var path = Path.Combine(LinuxUtils.GetWineHomeDir(), OutputName);
        if (!File.Exists(path)) return;

        try
        {
            var written = File.GetLastWriteTimeUtc(path);
            if (written == _lastFileWrite) return;
            _lastFileWrite = written;

            var json = File.ReadAllText(path);
            var raw = JsonSerializer.Deserialize<RawResult>(json);
            if (raw is null) return;

            Latest = Convert(raw, written);

            if (!Latest.Ok && Latest.Error != _lastError)
            {
                _lastError = Latest.Error;
                log($"Device probe: {Latest.Error}");
            }
            else if (Latest.Ok && _lastError.Length > 0)
            {
                _lastError = string.Empty;
                log("Device probe: runtime reachable again.");
            }
        }
        catch (IOException) { /* file being replaced by mv; next poll will read it */ }
        catch (JsonException ex) { log($"Device probe output was not valid JSON: {ex.Message}"); }
    }

    private static DeviceProbeResult Convert(RawResult raw, DateTime written)
    {
        static DeviceBattery Battery(RawDevice? d)
            => d is null ? DeviceBattery.None : new DeviceBattery(d.Present ?? false, d.Charging ?? false, Math.Clamp(d.Charge ?? 0f, 0f, 1f));

        var clients = (raw.Clients ?? new List<RawClient>())
            .Select(c => new RuntimeClient(
                c.Name ?? string.Empty,
                (c.Flags & 1) != 0,
                (c.Flags & 2) != 0,
                (c.Flags & 4) != 0,
                (c.Flags & 8) != 0,
                (c.Flags & 16) != 0))
            .ToList();

        raw.Devices ??= new Dictionary<string, RawDevice>();
        raw.Devices.TryGetValue("head", out var head);
        raw.Devices.TryGetValue("left", out var left);
        raw.Devices.TryGetValue("right", out var right);

        return new DeviceProbeResult(raw.Ok, raw.Error ?? string.Empty, written, Battery(head), Battery(left), Battery(right), clients);
    }

    private sealed class RawResult
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
        [JsonPropertyName("devices")] public Dictionary<string, RawDevice>? Devices { get; set; }
        [JsonPropertyName("clients")] public List<RawClient>? Clients { get; set; }
    }

    private sealed class RawDevice
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("present")] public bool? Present { get; set; }
        [JsonPropertyName("charging")] public bool? Charging { get; set; }
        [JsonPropertyName("charge")] public float? Charge { get; set; }
    }

    private sealed class RawClient
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("flags")] public uint Flags { get; set; }
    }
}
