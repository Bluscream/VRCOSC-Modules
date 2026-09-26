// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0),
// RPCTools/Payload.cs. Payload builders became a typed command record so the client can
// match responses to requests by nonce.

using System.Text.Json.Serialization;

namespace Bluscream.Modules.DiscordVoice.Rpc;

/// <summary>One Discord RPC command frame (opcode 1), serialised with Discord's lowercase keys.</summary>
public sealed class RpcCommand
{
    [JsonPropertyName("cmd")] public string Cmd { get; }
    [JsonPropertyName("args")] public object Args { get; }
    [JsonPropertyName("evt")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Evt { get; }
    [JsonPropertyName("nonce")] public string Nonce { get; } = Guid.NewGuid().ToString("N");

    public RpcCommand(string cmd, object? args = null, string? evt = null)
    {
        Cmd = cmd;
        Args = args ?? new { };
        Evt = evt;
    }
}

/// <summary>Builders for every RPC command the module uses. Names follow the Discord RPC docs.</summary>
public static class Payload
{
    public static RpcCommand Authenticate(string accessToken) => new("AUTHENTICATE", new { access_token = accessToken });

    public static RpcCommand GetVoiceSettings() => new("GET_VOICE_SETTINGS");
    public static RpcCommand SetVoiceSettings(Dictionary<string, object> args) => new("SET_VOICE_SETTINGS", args);
    public static RpcCommand SetMuteOnly(bool mute) => new("SET_VOICE_SETTINGS", new { mute });
    public static RpcCommand SetDeafenOnly(bool deaf) => new("SET_VOICE_SETTINGS", new { deaf });

    public static RpcCommand GetGuilds() => new("GET_GUILDS");
    public static RpcCommand GetGuild(string guildId) => new("GET_GUILD", new { guild_id = guildId });
    public static RpcCommand GetChannels(string guildId) => new("GET_CHANNELS", new { guild_id = guildId });
    public static RpcCommand GetChannel(string channelId) => new("GET_CHANNEL", new { channel_id = channelId });
    public static RpcCommand GetSelectedVoiceChannel() => new("GET_SELECTED_VOICE_CHANNEL");
    public static RpcCommand SelectVoiceChannel(string? channelId) => new("SELECT_VOICE_CHANNEL", new { channel_id = channelId });
    public static RpcCommand SelectTextChannel(string channelId) => new("SELECT_TEXT_CHANNEL", new { channel_id = channelId });

    public static RpcCommand Subscribe(string eventName, object? args = null) => new("SUBSCRIBE", args, eventName);
    public static RpcCommand Unsubscribe(string eventName, object? args = null) => new("UNSUBSCRIBE", args, eventName);

    public static RpcCommand SetActivity(string state, string details, string largeImageKey, string smallImageKey) => new("SET_ACTIVITY", new
    {
        pid = Environment.ProcessId,
        activity = new
        {
            state,
            details,
            assets = new { large_image = largeImageKey, small_image = smallImageKey }
        }
    });

    public static RpcCommand SetUserVoiceSettings(string userId, float? left = null, float? right = null, int? volume = null, bool? mute = null)
    {
        object? pan = left.HasValue && right.HasValue ? new { left = left.Value, right = right.Value } : null;
        return new RpcCommand("SET_USER_VOICE_SETTINGS", new { user_id = userId, pan, volume, mute });
    }

    public static RpcCommand SendActivityJoinInvite(string userId) => new("SEND_ACTIVITY_JOIN_INVITE", new { user_id = userId });
    public static RpcCommand CloseActivityRequest(string userId) => new("CLOSE_ACTIVITY_REQUEST", new { user_id = userId });
    public static RpcCommand SetCertifiedDevices(object[] devices) => new("SET_CERTIFIED_DEVICES", new { devices });
}
