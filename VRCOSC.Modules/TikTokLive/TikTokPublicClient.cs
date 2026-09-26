// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Read-only access to TikTok's public, unauthenticated endpoints: the profile page (follower
// count), the /@host/live page (current room id) and the webcast room-info JSON (live status,
// viewers, likes). No websocket, no signing, no login - see README.md for what that costs.

using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bluscream.Modules.TikTokLive;

/// <summary>Snapshot of a TikTok LIVE room from the public room-info endpoint.</summary>
public readonly record struct TikTokRoomInfo(string RoomId, bool IsLive, int Viewers, int Likes, int? Followers, string Title);

/// <summary>Where a follower count came from, so the module can report it in the log.</summary>
public enum FollowerSource { None, RoomInfo, ProfilePage }

public sealed partial class TikTokPublicClient
{
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";
    private const int RoomStatusLive = 2;

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/json;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    [GeneratedRegex("\"roomId\":\"(\\d+)\"")]
    private static partial Regex RoomIdRegex();

    [GeneratedRegex("\"followerCount\":(\\d+)")]
    private static partial Regex FollowerCountRegex();

    /// <summary>Normalises "@name", " name " or a profile URL to the bare unique id.</summary>
    public static string NormaliseHost(string? raw)
    {
        var host = (raw ?? string.Empty).Trim();
        var at = host.LastIndexOf('@');
        if (at >= 0) host = host[(at + 1)..];
        var slash = host.IndexOf('/');
        if (slash >= 0) host = host[..slash];
        return host.Trim();
    }

    /// <summary>
    /// Resolves the host's current room id from the public live page. Returns null when the
    /// page carries no room id (host has never gone live, or TikTok served a challenge page).
    /// </summary>
    public async Task<string?> GetRoomIdAsync(string host, CancellationToken token)
    {
        var html = await Http.GetStringAsync($"https://www.tiktok.com/@{Uri.EscapeDataString(host)}/live", token).ConfigureAwait(false);
        var match = RoomIdRegex().Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Reads the current state of a room. Returns null when TikTok refuses or the payload is unusable.</summary>
    public async Task<TikTokRoomInfo?> GetRoomInfoAsync(string roomId, CancellationToken token)
    {
        var json = await Http.GetStringAsync($"https://webcast.tiktok.com/webcast/room/info/?aid=1988&room_id={Uri.EscapeDataString(roomId)}", token).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("status_code", out var code) && code.ValueKind == JsonValueKind.Number && code.GetInt32() != 0) return null;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object) return null;

        var status = ReadInt(data, "status");
        int? followers = data.TryGetProperty("owner", out var owner) && owner.TryGetProperty("follow_info", out var followInfo)
            ? ReadInt(followInfo, "follower_count")
            : null;
        var title = data.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? string.Empty : string.Empty;

        return new TikTokRoomInfo(
            roomId,
            status == RoomStatusLive,
            ReadInt(data, "user_count") ?? 0,
            ReadInt(data, "like_count") ?? 0,
            followers,
            title);
    }

    /// <summary>
    /// Scrapes the follower count from the public profile page. TikTok rounds this value in the
    /// page (e.g. 95900000), so the room-info count is preferred when available.
    /// </summary>
    public async Task<int?> GetProfileFollowersAsync(string host, CancellationToken token)
    {
        var html = await Http.GetStringAsync($"https://www.tiktok.com/@{Uri.EscapeDataString(host)}", token).ConfigureAwait(false);
        var match = FollowerCountRegex().Match(html);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) ? count : null;
    }

    private static int? ReadInt(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var l) => (int)Math.Clamp(l, int.MinValue, int.MaxValue),
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var l) => (int)Math.Clamp(l, int.MinValue, int.MaxValue),
            _ => null
        };
    }
}
