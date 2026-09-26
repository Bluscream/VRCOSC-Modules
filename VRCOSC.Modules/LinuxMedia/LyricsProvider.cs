// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Synced-lyrics lookup via LRCLIB (https://lrclib.net), a free, keyless lyrics database.

using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bluscream.Modules;

/// <summary>
/// Fetches synced lyrics for the current track once per track change and answers "which
/// line is current" for a playback position. Only artist, title and duration leave the
/// machine, and only when the user has enabled lyrics.
/// </summary>
internal sealed partial class LyricsProvider
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private readonly object _sync = new();
    private string _trackKey = string.Empty;
    private List<(TimeSpan At, string Text)> _lines = new();
    private bool _plainOnly;
    private CancellationTokenSource? _inflight;

    /// <summary>True when the current track has time-synced lines.</summary>
    public bool HasSyncedLyrics { get { lock (_sync) return _lines.Count > 0; } }

    /// <summary>True when LRCLIB only had unsynced lyrics for this track.</summary>
    public bool HasPlainLyricsOnly { get { lock (_sync) return _plainOnly; } }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = RequestTimeout };
        // LRCLIB asks clients to identify themselves.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VRCOSC-BluscreamModules/1.0 (https://github.com/Bluscream/VRCOSC-Modules)");
        return client;
    }

    /// <summary>
    /// Starts a lookup for a new track. Idempotent for the same artist/title/duration; cancels
    /// any lookup still running for a previous track. Completes in the background.
    /// </summary>
    public void TrackChanged(string artist, string title, TimeSpan duration, Action<string> log)
    {
        var key = $"{artist}|{title}|{(int)duration.TotalSeconds}";
        CancellationTokenSource cts;

        lock (_sync)
        {
            if (key == _trackKey) return;
            _trackKey = key;
            _lines = new List<(TimeSpan, string)>();
            _plainOnly = false;

            _inflight?.Cancel();
            _inflight = cts = new CancellationTokenSource();
        }

        if (string.IsNullOrWhiteSpace(title)) return;
        _ = FetchAsync(key, artist, title, duration, log, cts.Token);
    }

    public void Clear()
    {
        lock (_sync)
        {
            _trackKey = string.Empty;
            _lines = new List<(TimeSpan, string)>();
            _plainOnly = false;
            _inflight?.Cancel();
            _inflight = null;
        }
    }

    /// <summary>The lyric line that applies at <paramref name="position"/>, or empty before the first line.</summary>
    public string LineAt(TimeSpan position)
    {
        lock (_sync)
        {
            if (_lines.Count == 0) return string.Empty;

            // Lines are sorted; find the last one that has started.
            var lo = 0;
            var hi = _lines.Count - 1;
            var found = -1;
            while (lo <= hi)
            {
                var mid = (lo + hi) / 2;
                if (_lines[mid].At <= position) { found = mid; lo = mid + 1; }
                else hi = mid - 1;
            }

            return found < 0 ? string.Empty : _lines[found].Text;
        }
    }

    private async Task FetchAsync(string key, string artist, string title, TimeSpan duration, Action<string> log, CancellationToken ct)
    {
        try
        {
            var url = "https://lrclib.net/api/get?artist_name=" + Uri.EscapeDataString(artist)
                      + "&track_name=" + Uri.EscapeDataString(title);
            if (duration > TimeSpan.Zero)
                url += "&duration=" + ((int)Math.Round(duration.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                log($"No lyrics on LRCLIB for '{artist} - {title}'.");
                return;
            }
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            var synced = root.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
            var plain = root.TryGetProperty("plainLyrics", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

            var parsed = string.IsNullOrWhiteSpace(synced) ? new List<(TimeSpan, string)>() : ParseLrc(synced);

            lock (_sync)
            {
                if (_trackKey != key) return; // a newer track won the race
                _lines = parsed;
                _plainOnly = parsed.Count == 0 && !string.IsNullOrWhiteSpace(plain);
            }

            log(parsed.Count > 0
                ? $"Synced lyrics loaded for '{artist} - {title}' ({parsed.Count} lines)."
                : $"LRCLIB has only unsynced lyrics for '{artist} - {title}'; nothing to show per line.");
        }
        catch (OperationCanceledException) { /* superseded by the next track */ }
        catch (HttpRequestException ex) { log($"Lyrics lookup failed: {ex.Message}"); }
        catch (JsonException ex) { log($"Lyrics response was not valid JSON: {ex.Message}"); }
    }

    [GeneratedRegex(@"^\[(\d{1,2}):(\d{2})(?:[.:](\d{1,3}))?\](.*)$")]
    private static partial Regex LrcLine();

    /// <summary>Parses LRC text ("[mm:ss.xx] line") into sorted (time, text) pairs. Empty lines are kept so a gap clears the display.</summary>
    internal static List<(TimeSpan At, string Text)> ParseLrc(string lrc)
    {
        var result = new List<(TimeSpan, string)>();
        foreach (var raw in lrc.Split('\n'))
        {
            var m = LrcLine().Match(raw.TrimEnd('\r'));
            if (!m.Success) continue;

            var minutes = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var seconds = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var fraction = m.Groups[3].Success ? m.Groups[3].Value : "0";
            var millis = (int)Math.Round(double.Parse("0." + fraction, CultureInfo.InvariantCulture) * 1000);

            result.Add((new TimeSpan(0, 0, minutes, seconds, millis), m.Groups[4].Value.Trim()));
        }

        result.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return result;
    }
}
