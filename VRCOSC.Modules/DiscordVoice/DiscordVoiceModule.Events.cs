// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0).
// RPC DISPATCH events -> OSC parameters, ChatBox variables and events. Runs on the IPC
// reader thread, so nothing here may throw.

using System.Text.Json;

namespace Bluscream.Modules.DiscordVoice;

public sealed partial class DiscordVoiceModule
{
    private void HandleRpcEvent(JsonElement evt)
    {
        try
        {
            if (!evt.TryGetProperty("evt", out var nameEl) || nameEl.GetString() is not { } name) return;
            var data = evt.TryGetProperty("data", out var d) ? d : default;

            var code = EventNameToInt(name);
            SendParameter(DiscordVoiceParameter.LastEventCode, code);
            SetVariableValue(DiscordVoiceVariable.LastEventCode, code);
            DispatchRpcEvent(name, data);
        }
        catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is KeyNotFoundException)
        {
            LogDebug($"RPC event handling failed: {ex.Message}");
        }
    }

    private void DispatchRpcEvent(string name, JsonElement data)
    {
        switch (name)
        {
            case "READY":
                SendParameter(DiscordVoiceParameter.Ready, true);
                SetVariableValue(DiscordVoiceVariable.Ready, true);
                TriggerEvent(DiscordVoiceEvent.ReadyEvent);
                break;
            case "ERROR":
                if (data.TryGetProperty("code", out var errCode) && errCode.TryGetInt32(out var errorCode))
                {
                    SendParameter(DiscordVoiceParameter.LastErrorCode, errorCode);
                    SetVariableValue(DiscordVoiceVariable.LastErrorCode, errorCode);
                    TriggerEvent(DiscordVoiceEvent.ErrorEvent);
                }
                break;
            case "VOICE_CHANNEL_SELECT":
                OnVoiceChannelSelect(data);
                break;
            case "VOICE_SETTINGS_UPDATE":
                ApplyVoiceSettings(data);
                OnVoiceSettings(data);
                TriggerEvent(DiscordVoiceEvent.VoiceSettingsEvent);
                break;
            case "VOICE_CONNECTION_STATUS":
                if (data.TryGetProperty("state", out var stateEl))
                {
                    var state = VoiceStateToInt(stateEl.GetString());
                    SendParameter(DiscordVoiceParameter.VoiceConnectionState, state);
                    SetVariableValue(DiscordVoiceVariable.VoiceConnectionState, state);
                }
                break;
            case "GUILD_STATUS":
                SetIdAndTrigger(DiscordVoiceVariable.EventGuildId, Nested(data, "guild", "id"), DiscordVoiceEvent.GuildStatusEvent);
                break;
            case "GUILD_CREATE":
                SetIdAndTrigger(DiscordVoiceVariable.EventGuildId, Nested(data, "id"), DiscordVoiceEvent.GuildCreateEvent);
                break;
            case "CHANNEL_CREATE":
                SetIdAndTrigger(DiscordVoiceVariable.EventChannelId, Nested(data, "id"), DiscordVoiceEvent.ChannelCreateEvent);
                break;
            case "VOICE_STATE_CREATE":
                OnVoiceStateEvent(name, data);
                SetIdAndTrigger(DiscordVoiceVariable.EventUserId, Nested(data, "user", "id"), DiscordVoiceEvent.VoiceStateCreateEvent);
                break;
            case "VOICE_STATE_UPDATE":
                OnVoiceStateEvent(name, data);
                SetIdAndTrigger(DiscordVoiceVariable.EventUserId, Nested(data, "user", "id"), DiscordVoiceEvent.VoiceStateUpdateEvent);
                break;
            case "VOICE_STATE_DELETE":
                OnVoiceStateEvent(name, data);
                SetIdAndTrigger(DiscordVoiceVariable.EventUserId, Nested(data, "user", "id"), DiscordVoiceEvent.VoiceStateDeleteEvent);
                break;
            case "SPEAKING_START":
                OnSpeakingEvent(name, data);
                SetIdAndTrigger(DiscordVoiceVariable.EventUserId, Nested(data, "user_id"), DiscordVoiceEvent.SpeakingStartEvent);
                break;
            case "SPEAKING_STOP":
                OnSpeakingEvent(name, data);
                SetIdAndTrigger(DiscordVoiceVariable.EventUserId, Nested(data, "user_id"), DiscordVoiceEvent.SpeakingStopEvent);
                break;
            case "MESSAGE_CREATE":
                SetIdAndTrigger(DiscordVoiceVariable.EventMessageId, Nested(data, "message", "id"), DiscordVoiceEvent.MessageCreateEvent);
                break;
            case "MESSAGE_UPDATE":
                SetIdAndTrigger(DiscordVoiceVariable.EventMessageId, Nested(data, "message", "id"), DiscordVoiceEvent.MessageUpdateEvent);
                break;
            case "MESSAGE_DELETE":
                SetIdAndTrigger(DiscordVoiceVariable.EventMessageId, Nested(data, "message", "id"), DiscordVoiceEvent.MessageDeleteEvent);
                break;
            case "NOTIFICATION_CREATE":
                SetIdAndTrigger(DiscordVoiceVariable.EventChannelId, Nested(data, "channel_id"), DiscordVoiceEvent.NotificationEvent);
                break;
            case "ACTIVITY_JOIN":
                TriggerEvent(DiscordVoiceEvent.ActivityJoinEvent);
                break;
            case "ACTIVITY_SPECTATE":
                TriggerEvent(DiscordVoiceEvent.ActivitySpectateEvent);
                break;
            case "ACTIVITY_JOIN_REQUEST":
                SetIdAndTrigger(DiscordVoiceVariable.EventUserId, Nested(data, "user", "id"), DiscordVoiceEvent.ActivityJoinRequestEvent);
                break;
        }
    }

    private void OnVoiceChannelSelect(JsonElement data)
    {
        var channelId = Nested(data, "channel_id") ?? string.Empty;
        _lastChannelId = channelId;
        SetCount(DiscordVoiceParameter.SelectedVoiceChannelId, DiscordVoiceVariable.SelectedVoiceChannelId, SnowflakeToInt(channelId));
        OnVoiceChannelChanged(channelId);

        if (!_autoUpdateDefaults || channelId.Length == 0) return;
        SetSettingValue(DiscordVoiceSetting.DefaultChannelId, channelId);
        if (Nested(data, "guild_id") is { Length: > 0 } guildId)
        {
            _lastGuildId = guildId;
            SetSettingValue(DiscordVoiceSetting.DefaultGuildId, guildId);
        }
    }

    private void SetIdAndTrigger(DiscordVoiceVariable variable, string? snowflake, DiscordVoiceEvent evt)
    {
        if (snowflake is null) return;
        SetVariableValue(variable, SnowflakeToInt(snowflake));
        TriggerEvent(evt);
    }

    /// <summary>Walks <paramref name="path"/> through nested objects; null when any step is missing or not a string.</summary>
    private static string? Nested(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var key in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(key, out current)) return null;
        }
        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static int VoiceStateToInt(string? state) => state switch
    {
        "DISCONNECTED" => 0,
        "AWAITING_ENDPOINT" => 1,
        "AUTHENTICATING" => 2,
        "CONNECTING" => 3,
        "CONNECTED" => 4,
        "VOICE_DISCONNECTED" => 5,
        "VOICE_CONNECTING" => 6,
        "VOICE_CONNECTED" => 7,
        "NO_ROUTE" => 8,
        "ICE_CHECKING" => 9,
        _ => -1
    };

    private static int EventNameToInt(string name) => name switch
    {
        "READY" => 0,
        "ERROR" => 1,
        "GUILD_STATUS" => 2,
        "GUILD_CREATE" => 3,
        "CHANNEL_CREATE" => 4,
        "VOICE_CHANNEL_SELECT" => 5,
        "VOICE_STATE_CREATE" => 6,
        "VOICE_STATE_UPDATE" => 7,
        "VOICE_STATE_DELETE" => 8,
        "VOICE_SETTINGS_UPDATE" => 9,
        "VOICE_CONNECTION_STATUS" => 10,
        "SPEAKING_START" => 11,
        "SPEAKING_STOP" => 12,
        "MESSAGE_CREATE" => 13,
        "MESSAGE_UPDATE" => 14,
        "MESSAGE_DELETE" => 15,
        "NOTIFICATION_CREATE" => 16,
        "ACTIVITY_JOIN" => 17,
        "ACTIVITY_SPECTATE" => 18,
        "ACTIVITY_JOIN_REQUEST" => 19,
        _ => -1
    };
}
