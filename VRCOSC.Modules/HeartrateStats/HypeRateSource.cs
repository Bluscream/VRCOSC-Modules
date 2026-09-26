// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// HypeRate.io Phoenix channel websocket: join "hr:<id>" after connecting, send a Phoenix
// heartbeat every 10 s (the server drops idle sockets), and read {"event":"hr_update",
// "payload":{"hr":72}} frames. The API key is issued per application by HypeRate.

using System.Text.Json;

namespace Bluscream.Modules.HeartrateStats;

internal sealed class HypeRateSource : WebSocketHeartrateSource
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(10);

    private readonly Uri _uri;
    private readonly string _topic;

    public HypeRateSource(string sessionId, string apiKey, Action<string> log)
        : base(log)
    {
        _uri = new Uri("wss://app.hyperate.io/socket/websocket?token=" + Uri.EscapeDataString(apiKey));
        _topic = "hr:" + sessionId.Trim();
    }

    protected override string Name => "HypeRate";
    protected override Uri Uri => _uri;
    protected override TimeSpan? KeepAliveInterval => HeartbeatInterval;

    protected override Task OnConnectedAsync(CancellationToken ct) =>
        SendAsync(JsonSerializer.Serialize(new { topic = _topic, @event = "phx_join", payload = new { }, @ref = 0 }), ct);

    protected override Task OnKeepAliveAsync(CancellationToken ct) =>
        SendAsync(JsonSerializer.Serialize(new { topic = "phoenix", @event = "heartbeat", payload = new { }, @ref = 0 }), ct);

    protected override int? ParseHeartrate(string message)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;

        if (!root.TryGetProperty("event", out var evt) || evt.GetString() != "hr_update") return null;

        if (!root.TryGetProperty("payload", out var payload)
            || !payload.TryGetProperty("hr", out var hr)
            || hr.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return hr.GetInt32();
    }
}
