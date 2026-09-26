// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0).

namespace Bluscream.Modules.DiscordVoice;

public enum DiscordVoiceParameter
{
    Mute,
    Deafen,
    RequestGuildCount,
    GuildCount,
    RequestChannelCount,
    ChannelCount,
    SelectVoiceChannel,
    RequestSelectedVoiceChannel,
    SelectedVoiceChannelId,
    RequestVoiceSettings,
    InputVolume,
    OutputVolume,
    SetInputVolume,
    SetOutputVolume,
    SetVoiceSettings,
    RequestChannelUserCount,
    ChannelUserCount,
    RequestChannelType,
    ChannelType,
    Ready,
    LastErrorCode,
    VoiceConnectionState,
    SubscribeEvent,
    UnsubscribeEvent,
    SelectTextChannel,
    SetUserVolume,
    SetUserMute,
    SetActivity,
    SendActivityJoinInvite,
    CloseActivityRequest,
    SendCertifiedDevices,
    RequestGuildInfo,
    GuildHasIcon,
    LastEventCode
}

public enum DiscordVoiceSetting
{
    ClientId,
    ClientSecret,
    DefaultGuildId,
    DefaultChannelId,
    AutoUpdateDefaults
}

public enum DiscordVoiceVariable
{
    GuildCount,
    ChannelCount,
    SelectedVoiceChannelId,
    InputVolume,
    OutputVolume,
    ChannelUserCount,
    ChannelType,
    Ready,
    LastErrorCode,
    VoiceConnectionState,
    EventGuildId,
    EventChannelId,
    EventUserId,
    EventMessageId,
    LastEventCode
}

public enum DiscordVoiceState
{
    VoiceState
}

public enum DiscordVoiceEvent
{
    ReadyEvent,
    ErrorEvent,
    GuildStatusEvent,
    GuildCreateEvent,
    ChannelCreateEvent,
    VoiceStateCreateEvent,
    VoiceStateUpdateEvent,
    VoiceStateDeleteEvent,
    VoiceSettingsEvent,
    SpeakingStartEvent,
    SpeakingStopEvent,
    MessageCreateEvent,
    MessageUpdateEvent,
    MessageDeleteEvent,
    NotificationEvent,
    ActivityJoinEvent,
    ActivitySpectateEvent,
    ActivityJoinRequestEvent
}
