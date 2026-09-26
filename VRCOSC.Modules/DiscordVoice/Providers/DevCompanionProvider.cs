// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Fallback provider for the devcompanionExtended userplugin
// (Equicord src/userplugins/devcompanionExtended). With "Host an in-app MCP HTTP server"
// enabled it serves MCP JSON-RPC over plain HTTP POST on 127.0.0.1:8486, and its `store`
// tool calls any Flux store method. We poll:
//
//   VoiceStateStore.getCurrentClientVoiceChannelId()   -> channel id or null
//   ChannelStore.getChannel(id).name                    -> channel name
//   VoiceStateStore.getVoiceStatesForChannel(id)        -> members + our selfMute/selfDeaf
//   UserStore.getUser(id).globalName/username           -> display names (cached)
//   SpeakingStore.getSpeakers()                          -> user ids speaking right now
//
// and toggle mute/deafen with the `flux` tool (AUDIO_TOGGLE_SELF_MUTE / _DEAF). Polling
// means speaking changes are seen at the poll interval, not instantly, and mute/deafen are
// only known while in a voice channel (MediaEngineStore's state is too large for the tool's
// inline response). Only user ids and names are read from the store responses.

using System.Net.Http;
using System.Text;
using System.Text.Json;
using Bluscream.Modules.DiscordVoice.Voice;

namespace Bluscream.Modules.DiscordVoice.Providers;

public sealed class DevCompanionProvider : IVoiceProvider
{
    public const int DefaultPort = 8486;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _url;
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private CancellationTokenSource? _cts;
    private Action<string> _log = _ => { };
    private string _selfId = string.Empty;
    private bool _available;
    private bool _loggedFailure;
    private int _rpcId;

    public DevCompanionProvider(int port) => _url = $"http://127.0.0.1:{port}/";

    public string Name => "DevCompanion";
    public bool IsAvailable => _available;
    public VoiceStateTracker Tracker { get; } = new();

    public Task StartAsync(Action<string> log, CancellationToken ct)
    {
        _log = log;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = PollLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task<bool> SetMuteAsync(bool mute, CancellationToken ct)
    {
        if (!_available) return false;
        if (Tracker.Snapshot(DateTime.UtcNow).Muted == mute) return true;
        return await DispatchAsync("AUDIO_TOGGLE_SELF_MUTE", ct).ConfigureAwait(false);
    }

    public async Task<bool> SetDeafenAsync(bool deafen, CancellationToken ct)
    {
        if (!_available) return false;
        if (Tracker.Snapshot(DateTime.UtcNow).Deafened == deafen) return true;
        return await DispatchAsync("AUDIO_TOGGLE_SELF_DEAF", ct).ConfigureAwait(false);
    }

    private async Task<bool> DispatchAsync(string type, CancellationToken ct)
    {
        try
        {
            using var doc = await CallToolAsync("flux", new { action = "dispatch", type, payload = new { syncRemote = true, playSoundEffect = true, context = "default" } }, ct).ConfigureAwait(false);
            return doc is not null;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is InvalidOperationException || ex is ObjectDisposedException)
        {
            _log($"DevCompanion dispatch {type} failed: {ex.Message}");
            return false;
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            do
            {
                await PollOnceAsync(ct).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // Safety net for a background loop: anything unexpected is logged, never left unobserved.
            _log($"DevCompanion poll loop stopped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        try
        {
            if (_selfId.Length == 0)
            {
                var me = await StoreCallAsync("UserStore", "getCurrentUser", null, ct).ConfigureAwait(false);
                _selfId = me.ValueKind == JsonValueKind.Object && me.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
            }

            var channel = await StoreCallAsync("VoiceStateStore", "getCurrentClientVoiceChannelId", null, ct).ConfigureAwait(false);
            var channelId = channel.ValueKind == JsonValueKind.String ? channel.GetString() ?? string.Empty : string.Empty;

            if (channelId.Length == 0)
            {
                Tracker.ClearChannel();
            }
            else
            {
                await RefreshChannelAsync(channelId, ct).ConfigureAwait(false);
            }

            SetAvailable(true);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException || ex is InvalidOperationException || ex is ObjectDisposedException)
        {
            if (!ct.IsCancellationRequested) SetAvailable(false, ex.Message);
        }
    }

    private async Task RefreshChannelAsync(string channelId, CancellationToken ct)
    {
        if (channelId != Tracker.ChannelId)
        {
            var ch = await StoreCallAsync("ChannelStore", "getChannel", [channelId], ct).ConfigureAwait(false);
            var name = ch.ValueKind == JsonValueKind.Object && ch.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            Tracker.SetChannel(channelId, name.Length > 0 ? name : "Voice");
        }

        var states = await StoreCallAsync("VoiceStateStore", "getVoiceStatesForChannel", [channelId], ct).ConfigureAwait(false);
        var members = new List<(string, string)>();
        if (states.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in states.EnumerateObject())
            {
                var userId = entry.Name;
                members.Add((userId, await DisplayNameAsync(userId, ct).ConfigureAwait(false)));
                if (userId == _selfId)
                {
                    var s = entry.Value;
                    Tracker.SetSelf(Flag(s, "selfMute") || Flag(s, "mute"), Flag(s, "selfDeaf") || Flag(s, "deaf"));
                }
            }
        }
        Tracker.ReplaceMembers(members);

        var speakers = await StoreCallAsync("SpeakingStore", "getSpeakers", null, ct).ConfigureAwait(false);
        var speaking = new HashSet<string>(StringComparer.Ordinal);
        if (speakers.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in speakers.EnumerateArray())
            {
                if (s.ValueKind == JsonValueKind.String && s.GetString() is { Length: > 0 } uid) speaking.Add(uid);
            }
        }
        var now = DateTime.UtcNow;
        foreach (var (userId, _) in members) Tracker.SetSpeaking(userId, speaking.Contains(userId), now);
    }

    private async Task<string> DisplayNameAsync(string userId, CancellationToken ct)
    {
        if (_names.TryGetValue(userId, out var cached)) return cached;
        var user = await StoreCallAsync("UserStore", "getUser", [userId], ct).ConfigureAwait(false);
        var name = user.ValueKind == JsonValueKind.Object
            ? (user.TryGetProperty("globalName", out var g) && g.ValueKind == JsonValueKind.String && g.GetString() is { Length: > 0 } global ? global
                : user.TryGetProperty("username", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? userId : userId)
            : userId;
        _names[userId] = name;
        return name;
    }

    private static bool Flag(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>Calls the `store` tool and returns a clone of its methodResult (throws when the plugin reports an error).</summary>
    private async Task<JsonElement> StoreCallAsync(string store, string method, object[]? args, CancellationToken ct)
    {
        using var doc = await CallToolAsync("store", new { action = "call", name = store, method, args = args ?? [] }, ct).ConfigureAwait(false)
                        ?? throw new InvalidOperationException($"{store}.{method}: empty response");
        var root = doc.RootElement;
        if (root.TryGetProperty("methodError", out var err) && err.ValueKind == JsonValueKind.String)
            throw new InvalidOperationException($"{store}.{method}: {err.GetString()}");
        if (root.TryGetProperty("resourceId", out _))
            throw new InvalidOperationException($"{store}.{method}: response too large, returned as resource");
        return root.TryGetProperty("methodResult", out var result) ? result.Clone() : default;
    }

    /// <summary>MCP tools/call over HTTP; the tool's text content is parsed as JSON.</summary>
    private async Task<JsonDocument?> CallToolAsync(string tool, object arguments, CancellationToken ct)
    {
        var request = new { jsonrpc = "2.0", id = Interlocked.Increment(ref _rpcId), method = "tools/call", @params = new { name = tool, arguments } };
        using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(_url, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        using var envelope = JsonDocument.Parse(body);
        var root = envelope.RootElement;
        if (root.TryGetProperty("error", out var error))
            throw new InvalidOperationException(error.TryGetProperty("message", out var m) ? m.GetString() ?? "MCP error" : "MCP error");
        if (!root.TryGetProperty("result", out var result) || !result.TryGetProperty("content", out var items) || items.GetArrayLength() == 0) return null;
        var text = items[0].TryGetProperty("text", out var t) ? t.GetString() : null;
        return string.IsNullOrEmpty(text) ? null : JsonDocument.Parse(text);
    }

    private void SetAvailable(bool available, string? reason = null)
    {
        if (available == _available) return;
        _available = available;
        if (available)
        {
            _log($"DevCompanion MCP reachable at {_url}; polling voice state.");
            _loggedFailure = false;
        }
        else
        {
            Tracker.ClearChannel();
            if (!_loggedFailure) _log($"DevCompanion MCP not reachable at {_url}: {reason}");
            _loggedFailure = true;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _http.Dispose();
        Tracker.ClearChannel();
    }
}
