// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Minimal Twitch OAuth (device code grant) and Helix client used by TwitchStatsModule.
// Deliberately dependency-free: the official module pulls in TwitchLib, which is far more
// than three GET requests need, and modules cannot share a token anyway.

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Bluscream.Modules.TwitchStats;

/// <summary>Outcome of a device-code token poll.</summary>
internal enum DevicePollStatus { Pending, SlowDown, Granted, Denied, Expired, Error }

internal sealed record DeviceCode(string Code, string UserCode, string VerificationUri, TimeSpan ExpiresIn, TimeSpan Interval);

internal sealed record TokenSet(string AccessToken, string? RefreshToken, TimeSpan ExpiresIn);

internal sealed record TokenInfo(string Login, string UserId, IReadOnlyList<string> Scopes, TimeSpan ExpiresIn);

internal sealed record HelixUser(string Id, string Login, string DisplayName);

internal sealed record HelixStream(string GameName, string Title, int ViewerCount, DateTime StartedAt);

internal sealed record HelixChannel(string DisplayName, string GameName, string Title);

/// <summary>Thrown for non-success Helix responses so the caller can branch on the status code.</summary>
internal sealed class HelixException : Exception
{
    public HttpStatusCode StatusCode { get; }

    /// <summary>When the response carried Ratelimit-Reset, the moment the bucket refills (UTC).</summary>
    public DateTime? RateLimitReset { get; }

    public HelixException(HttpStatusCode statusCode, string message, DateTime? rateLimitReset = null) : base(message)
    {
        StatusCode = statusCode;
        RateLimitReset = rateLimitReset;
    }
}

internal sealed class TwitchHelixClient
{
    private const string AuthBase = "https://id.twitch.tv/oauth2/";
    private const string HelixBase = "https://api.twitch.tv/helix/";
    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    private static readonly HttpClient Http = CreateClient();

    private readonly string _clientId;

    public TwitchHelixClient(string clientId)
    {
        _clientId = clientId;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VRCOSC-BluscreamModules/1.0 (https://github.com/Bluscream/VRCOSC-Modules)");
        return client;
    }

    // ─────────────────────────── OAuth ───────────────────────────

    /// <summary>Starts the device code grant flow. Returns null (with a reason) when Twitch rejects the request.</summary>
    public async Task<(DeviceCode? Code, string? Error)> RequestDeviceCodeAsync(string scopes, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["scopes"] = scopes
        });
        using var response = await Http.PostAsync(AuthBase + "device", content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return (null, $"{(int)response.StatusCode} {ErrorMessage(body)}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return (new DeviceCode(
            root.GetProperty("device_code").GetString() ?? string.Empty,
            root.GetProperty("user_code").GetString() ?? string.Empty,
            root.GetProperty("verification_uri").GetString() ?? string.Empty,
            TimeSpan.FromSeconds(root.GetProperty("expires_in").GetInt32()),
            TimeSpan.FromSeconds(root.TryGetProperty("interval", out var i) ? i.GetInt32() : 5)), null);
    }

    /// <summary>One poll of the token endpoint for a pending device code.</summary>
    public async Task<(DevicePollStatus Status, TokenSet? Token, string? Error)> PollDeviceTokenAsync(DeviceCode code, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["device_code"] = code.Code,
            ["grant_type"] = DeviceGrantType
        });
        using var response = await Http.PostAsync(AuthBase + "token", content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return (DevicePollStatus.Granted, ParseToken(body), null);

        var message = ErrorMessage(body);
        var status = message switch
        {
            var m when m.Contains("authorization_pending", StringComparison.OrdinalIgnoreCase) => DevicePollStatus.Pending,
            var m when m.Contains("slow_down", StringComparison.OrdinalIgnoreCase) => DevicePollStatus.SlowDown,
            var m when m.Contains("access_denied", StringComparison.OrdinalIgnoreCase) => DevicePollStatus.Denied,
            var m when m.Contains("expired", StringComparison.OrdinalIgnoreCase) => DevicePollStatus.Expired,
            _ => DevicePollStatus.Error
        };
        return (status, null, $"{(int)response.StatusCode} {message}");
    }

    /// <summary>Exchanges a refresh token for a fresh token pair. Public clients send no secret.</summary>
    public async Task<(TokenSet? Token, string? Error)> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });
        using var response = await Http.PostAsync(AuthBase + "token", content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode ? (ParseToken(body), null) : (null, $"{(int)response.StatusCode} {ErrorMessage(body)}");
    }

    /// <summary>Validates a token. Returns null when Twitch answers 401 (token expired or revoked).</summary>
    public async Task<TokenInfo?> ValidateAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthBase + "validate");
        request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new HelixException(response.StatusCode, ErrorMessage(body));

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var scopes = root.TryGetProperty("scopes", out var s) && s.ValueKind == JsonValueKind.Array
            ? s.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
            : new List<string>();
        return new TokenInfo(
            root.GetProperty("login").GetString() ?? string.Empty,
            root.GetProperty("user_id").GetString() ?? string.Empty,
            scopes,
            TimeSpan.FromSeconds(root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0));
    }

    private static TokenSet ParseToken(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        return new TokenSet(
            root.GetProperty("access_token").GetString() ?? string.Empty,
            root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null,
            TimeSpan.FromSeconds(root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 0));
    }

    private static string ErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return body;
            var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
            return string.Join(": ", new[] { error, message }.Where(x => !string.IsNullOrEmpty(x)));
        }
        catch (JsonException)
        {
            return body;
        }
    }

    // ─────────────────────────── Helix ───────────────────────────

    public async Task<HelixUser?> GetUserAsync(string accessToken, string? login, CancellationToken ct)
    {
        var query = string.IsNullOrEmpty(login) ? string.Empty : "?login=" + Uri.EscapeDataString(login);
        using var doc = await GetAsync(accessToken, "users" + query, ct).ConfigureAwait(false);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        var u = data[0];
        return new HelixUser(Str(u, "id"), Str(u, "login"), Str(u, "display_name"));
    }

    /// <summary>Null when the channel is offline.</summary>
    public async Task<HelixStream?> GetStreamAsync(string accessToken, string login, CancellationToken ct)
    {
        using var doc = await GetAsync(accessToken, "streams?user_login=" + Uri.EscapeDataString(login), ct).ConfigureAwait(false);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        var s = data[0];
        var startedAt = DateTime.TryParse(Str(s, "started_at"), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t
            : DateTime.UtcNow;
        return new HelixStream(Str(s, "game_name"), Str(s, "title"), s.TryGetProperty("viewer_count", out var v) ? v.GetInt32() : 0, startedAt);
    }

    public async Task<HelixChannel?> GetChannelAsync(string accessToken, string broadcasterId, CancellationToken ct)
    {
        using var doc = await GetAsync(accessToken, "channels?broadcaster_id=" + Uri.EscapeDataString(broadcasterId), ct).ConfigureAwait(false);
        var data = doc.RootElement.GetProperty("data");
        if (data.GetArrayLength() == 0) return null;
        var c = data[0];
        return new HelixChannel(Str(c, "broadcaster_name"), Str(c, "game_name"), Str(c, "title"));
    }

    /// <summary>Follower total. Needs moderator:read:followers, otherwise Helix answers 401.</summary>
    public async Task<int> GetFollowerTotalAsync(string accessToken, string broadcasterId, CancellationToken ct)
    {
        using var doc = await GetAsync(accessToken, "channels/followers?first=1&broadcaster_id=" + Uri.EscapeDataString(broadcasterId), ct).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("total", out var total) ? total.GetInt32() : 0;
    }

    private async Task<JsonDocument> GetAsync(string accessToken, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, HelixBase + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", _clientId);
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return JsonDocument.Parse(body);

        DateTime? reset = null;
        if (response.Headers.TryGetValues("Ratelimit-Reset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var epoch))
        {
            reset = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        throw new HelixException(response.StatusCode, $"{path.Split('?')[0]}: {(int)response.StatusCode} {ErrorMessage(body)}", reset);
    }

    private static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;
}
