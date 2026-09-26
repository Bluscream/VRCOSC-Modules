// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0),
// RPCTools/DiscordAuth.cs.

using System.Net.Http;
using System.Text.Json;

namespace Bluscream.Modules.DiscordVoice.Rpc;

/// <summary>
/// Fetches an OAuth2 access token with the client-credentials grant. Discord's RPC docs
/// require <c>rpc</c> for the RPC connection itself, <c>rpc.voice.read</c> for
/// GET_VOICE_SETTINGS / VOICE_SETTINGS_UPDATE and <c>rpc.voice.write</c> for
/// SET_VOICE_SETTINGS. The voice channel events (VOICE_CHANNEL_SELECT, VOICE_STATE_*,
/// SPEAKING_*) only need <c>rpc</c>.
/// </summary>
public static class DiscordAuth
{
    public const string Scopes = "rpc rpc.voice.read rpc.voice.write";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<string> FetchAccessTokenAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = Scopes
        });

        using var response = await Http.PostAsync("https://discord.com/api/oauth2/token", content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Token request failed: {(int)response.StatusCode} {response.ReasonPhrase}: {body}");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("access_token", out var token) && token.GetString() is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException("Token response carried no access_token.");
    }
}
