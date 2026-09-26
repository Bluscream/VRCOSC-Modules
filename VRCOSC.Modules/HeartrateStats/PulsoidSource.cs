// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Pulsoid real-time websocket. Frames look like {"measured_at":<ms>,"data":{"heart_rate":72}}.

using System.Text.Json;

namespace Bluscream.Modules.HeartrateStats;

internal sealed class PulsoidSource : WebSocketHeartrateSource
{
    private readonly Uri _uri;

    public PulsoidSource(string accessToken, Action<string> log)
        : base(log)
    {
        _uri = new Uri("wss://dev.pulsoid.net/api/v1/data/real_time?access_token=" + Uri.EscapeDataString(accessToken));
    }

    protected override string Name => "Pulsoid";
    protected override Uri Uri => _uri;

    protected override int? ParseHeartrate(string message)
    {
        using var doc = JsonDocument.Parse(message);

        if (!doc.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("heart_rate", out var hr)
            || hr.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return hr.GetInt32();
    }
}
