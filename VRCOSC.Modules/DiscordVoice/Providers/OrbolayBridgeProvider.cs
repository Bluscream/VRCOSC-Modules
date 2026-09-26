// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Fallback provider for Equicord's OrbolayBridge plugin (src/equicordplugins/orbolayBridge).
//
// The plugin is a WebSocket *client*: on start it connects to ws://127.0.0.1:<port> (default
// 6888, meant for the Orbolay overlay) and pushes voice state as JSON. We host that server
// here and speak the same protocol:
//
//   Discord -> us   REGISTER_CONFIG {userId}                 our own user id
//                   CHANNEL_JOINED {states:[{userId, username, channelId, mute, deaf, streaming}]}
//                   CHANNEL_LEFT
//                   VOICE_STATE_UPDATE {state:{userId, speaking}}          (from SPEAKING)
//                   VOICE_STATE_UPDATE {state:{userId, username, mute, deaf, ...}} (member change)
//                   STREAMER_MODE, MESSAGE_NOTIFICATION                       ignored
//   us -> Discord   TOGGLE_MUTE, TOGGLE_DEAF, DISCONNECT (toggle only, no absolute set)
//
// What it cannot give us: the channel *name* (only the id), and members of DM/group calls
// (the plugin only sends CHANNEL_JOINED for guild channels). The plugin also connects once
// at start and never retries, so it must be (re)enabled after VRCOSC is running.

using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bluscream.Modules.DiscordVoice.Voice;
using EmbedIO;
using EmbedIO.WebSockets;

namespace Bluscream.Modules.DiscordVoice.Providers;

public sealed class OrbolayBridgeProvider : IVoiceProvider
{
    public const int DefaultPort = 6888;

    private readonly int _port;
    private WebServer? _server;
    private BridgeSocket? _socket;
    private CancellationTokenSource? _cts;
    private Action<string> _log = _ => { };

    public OrbolayBridgeProvider(int port) => _port = port;

    public string Name => "OrbolayBridge";
    public bool IsAvailable => _socket?.HasClient == true;
    public VoiceStateTracker Tracker { get; } = new();

    public Task StartAsync(Action<string> log, CancellationToken ct)
    {
        _log = log;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            _socket = new BridgeSocket(this);
            // EmbedIO's own listener: System.Net.HttpListener needs http.sys, which Wine lacks.
            _server = new WebServer(o => o.WithUrlPrefix($"http://127.0.0.1:{_port}/").WithMode(HttpListenerMode.EmbedIO))
                .WithModule(_socket);
            _ = RunServerAsync(_server, _cts.Token);
            log($"OrbolayBridge server listening on ws://127.0.0.1:{_port} (enable Equicord's OrbolayBridge plugin with that port).");
        }
        catch (Exception ex) when (ex is SocketException || ex is System.Net.HttpListenerException || ex is InvalidOperationException)
        {
            log($"OrbolayBridge server could not start on port {_port}: {ex.Message}");
            _server = null;
        }
        return Task.CompletedTask;
    }

    /// <summary>Observes the listener task so a late bind failure is logged instead of becoming an unobserved exception.</summary>
    private async Task RunServerAsync(WebServer server, CancellationToken ct)
    {
        try
        {
            await server.RunAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is SocketException || ex is System.Net.HttpListenerException || ex is InvalidOperationException || ex is System.IO.IOException || ex is ObjectDisposedException)
        {
            _log($"OrbolayBridge server on port {_port} stopped: {ex.Message}");
        }
    }

    public async Task<bool> SetMuteAsync(bool mute, CancellationToken ct)
    {
        // The plugin only toggles; compare with what it last told us and flip when needed.
        if (!IsAvailable) return false;
        if (Tracker.Snapshot(DateTime.UtcNow).Muted == mute) return true;
        await Send(new { cmd = "TOGGLE_MUTE" }).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> SetDeafenAsync(bool deafen, CancellationToken ct)
    {
        if (!IsAvailable) return false;
        if (Tracker.Snapshot(DateTime.UtcNow).Deafened == deafen) return true;
        await Send(new { cmd = "TOGGLE_DEAF" }).ConfigureAwait(false);
        return true;
    }

    private async Task Send(object payload)
    {
        try
        {
            if (_socket is { } socket) await socket.SendToAllAsync(JsonSerializer.Serialize(payload)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.IO.IOException || ex is SocketException || ex is ObjectDisposedException || ex is InvalidOperationException)
        {
            _log($"OrbolayBridge send failed: {ex.Message}");
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var cmd = root.TryGetProperty("cmd", out var c) ? c.GetString() : null;
            switch (cmd)
            {
                case "CHANNEL_JOINED":
                    OnChannelJoined(root);
                    break;
                case "CHANNEL_LEFT":
                    Tracker.ClearChannel();
                    break;
                case "VOICE_STATE_UPDATE":
                    if (root.TryGetProperty("state", out var state)) OnVoiceStateUpdate(state);
                    break;
                case "REGISTER_CONFIG":
                    _selfId = root.TryGetProperty("userId", out var id) ? id.GetString() ?? string.Empty : string.Empty;
                    break;
            }
        }
        catch (JsonException ex)
        {
            _log($"OrbolayBridge sent malformed JSON: {ex.Message}");
        }
    }

    private string _selfId = string.Empty;

    private void OnChannelJoined(JsonElement root)
    {
        if (!root.TryGetProperty("states", out var states) || states.ValueKind != JsonValueKind.Array) return;
        var members = new List<(string, string)>();
        string channelId = string.Empty;
        foreach (var s in states.EnumerateArray())
        {
            var userId = Str(s, "userId");
            if (userId.Length == 0) continue;
            channelId = Str(s, "channelId");
            members.Add((userId, Str(s, "username") is { Length: > 0 } name ? name : userId));
            if (userId == _selfId) Tracker.SetSelf(Bool(s, "mute"), Bool(s, "deaf"));
        }
        // No channel name on this protocol; the id is all the plugin sends.
        Tracker.SetChannel(channelId, channelId.Length > 0 ? "Voice" : string.Empty);
        Tracker.ReplaceMembers(members);
    }

    private void OnVoiceStateUpdate(JsonElement state)
    {
        var userId = Str(state, "userId");
        if (userId.Length == 0) return;

        if (state.TryGetProperty("speaking", out var speaking) && !state.TryGetProperty("channelId", out _))
        {
            Tracker.SetSpeaking(userId, speaking.ValueKind == JsonValueKind.True, DateTime.UtcNow);
            return;
        }

        var channelId = Str(state, "channelId");
        if (channelId.Length == 0 || (Tracker.ChannelId.Length > 0 && channelId != Tracker.ChannelId))
        {
            Tracker.RemoveMember(userId);
            return;
        }

        Tracker.UpsertMember(userId, Str(state, "username") is { Length: > 0 } name ? name : userId);
        if (userId == _selfId) Tracker.SetSelf(Bool(state, "mute"), Bool(state, "deaf"));
    }

    private static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
    private static bool? Bool(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _server?.Dispose();
        _server = null;
        _socket = null;
        Tracker.ClearChannel();
    }

    private sealed class BridgeSocket : WebSocketModule
    {
        private readonly OrbolayBridgeProvider _owner;
        private int _clients;

        public BridgeSocket(OrbolayBridgeProvider owner) : base("/", true) => _owner = owner;

        public bool HasClient => Volatile.Read(ref _clients) > 0;

        public Task SendToAllAsync(string payload) => BroadcastAsync(payload);

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
        {
            Interlocked.Increment(ref _clients);
            _owner._log("OrbolayBridge: Discord connected.");
            return Task.CompletedTask;
        }

        protected override Task OnClientDisconnectedAsync(IWebSocketContext context)
        {
            if (Interlocked.Decrement(ref _clients) <= 0)
            {
                Volatile.Write(ref _clients, 0);
                _owner.Tracker.ClearChannel();
                _owner._log("OrbolayBridge: Discord disconnected.");
            }
            return Task.CompletedTask;
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] buffer, IWebSocketReceiveResult result)
        {
            _owner.HandleMessage(Encoding.UTF8.GetString(buffer));
            return Task.CompletedTask;
        }
    }
}
