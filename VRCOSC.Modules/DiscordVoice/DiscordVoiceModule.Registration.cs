// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Forked from Yeusepe's DiscordOSC (https://github.com/Yeusepe/Yeusepes-Modules, GPL-3.0).
// Parameter, variable, state and event registration - the OSC surface mirrors DiscordOSC's
// so avatars built for it keep working.

using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.DiscordVoice;

public sealed partial class DiscordVoiceModule
{
    private void RegisterParameters()
    {
        RegisterParameter<bool>(DiscordVoiceParameter.Mute, "VRCOSC/Discord/Mic", ParameterMode.ReadWrite, "Mute", "Mute or unmute the Discord client. Also reflects the current mute state.");
        RegisterParameter<bool>(DiscordVoiceParameter.Deafen, "VRCOSC/Discord/Deafen", ParameterMode.ReadWrite, "Deafen", "Deafen or undeafen the Discord client. Also reflects the current deafen state.");
        RegisterParameter<bool>(DiscordVoiceParameter.Muted, "VRCOSC/Discord/Muted", ParameterMode.ReadWrite, "Muted", "Current mute state; write to mute or unmute (same as VRCOSC/Discord/Mic).");
        RegisterParameter<bool>(DiscordVoiceParameter.Deafened, "VRCOSC/Discord/Deafened", ParameterMode.ReadWrite, "Deafened", "Current deafen state; write to deafen or undeafen (same as VRCOSC/Discord/Deafen).");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestGuildCount, "VRCOSC/Discord/GetGuilds", ParameterMode.ReadWrite, "Request guild list", "Trigger to fetch guilds and update GuildCount.");
        RegisterParameter<int>(DiscordVoiceParameter.GuildCount, "VRCOSC/Discord/GuildCount", ParameterMode.Write, "Guild count", "Number of guilds returned by GET_GUILDS.");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestChannelCount, "VRCOSC/Discord/GetChannels/*", ParameterMode.ReadWrite, "Request channel list", "Send guild id as wildcard to fetch channels and update ChannelCount.");
        RegisterParameter<int>(DiscordVoiceParameter.ChannelCount, "VRCOSC/Discord/ChannelCount", ParameterMode.Write, "Channel count", "Number of channels returned by GET_CHANNELS.");
        RegisterParameter<bool>(DiscordVoiceParameter.SelectVoiceChannel, "VRCOSC/Discord/SelectVoiceChannel/*", ParameterMode.ReadWrite, "Join voice channel", "Send channel id as wildcard to join.");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestSelectedVoiceChannel, "VRCOSC/Discord/GetCurrentVoiceChannel", ParameterMode.ReadWrite, "Request current voice channel", "Fetch the id of the currently joined voice channel.");
        RegisterParameter<int>(DiscordVoiceParameter.SelectedVoiceChannelId, "VRCOSC/Discord/CurrentVoiceChannelId", ParameterMode.Write, "Current voice channel id", "ID of the current voice channel (truncated to 32 bit), or 0 if none.");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestVoiceSettings, "VRCOSC/Discord/GetVoiceSettings", ParameterMode.ReadWrite, "Request voice settings", "Fetch input and output volumes.");
        RegisterParameter<float>(DiscordVoiceParameter.InputVolume, "VRCOSC/Discord/InputVolume", ParameterMode.ReadWrite, "Input volume", "Input volume percentage (0-100). Writing sets it.");
        RegisterParameter<float>(DiscordVoiceParameter.OutputVolume, "VRCOSC/Discord/OutputVolume", ParameterMode.ReadWrite, "Output volume", "Output volume percentage (0-200). Writing sets it.");
        RegisterParameter<float>(DiscordVoiceParameter.SetInputVolume, "VRCOSC/Discord/SetInputVolume", ParameterMode.ReadWrite, "Set input volume", "Set the input device volume.");
        RegisterParameter<float>(DiscordVoiceParameter.SetOutputVolume, "VRCOSC/Discord/SetOutputVolume", ParameterMode.ReadWrite, "Set output volume", "Set the output device volume.");
        RegisterParameter<bool>(DiscordVoiceParameter.SetVoiceSettings, "VRCOSC/Discord/SetVoiceSettings/*/*", ParameterMode.ReadWrite, "Set voice settings wildcard", "Usage: SetVoiceSettings/<field>/<value>. Fields: mute, deaf, input_volume, output_volume, agc, echo_cancellation, noise_suppression, qos, silence_warning.");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestChannelUserCount, "VRCOSC/Discord/GetChannelUsers/*", ParameterMode.ReadWrite, "Request channel user count", "Send channel id as wildcard to fetch user count.");
        RegisterParameter<int>(DiscordVoiceParameter.ChannelUserCount, "VRCOSC/Discord/ChannelUserCount", ParameterMode.Write, "Channel user count", "Number of users returned by GET_CHANNEL.");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestChannelType, "VRCOSC/Discord/GetChannelType/*", ParameterMode.ReadWrite, "Request channel type", "Send channel id as wildcard to fetch channel type.");
        RegisterParameter<int>(DiscordVoiceParameter.ChannelType, "VRCOSC/Discord/ChannelType", ParameterMode.Write, "Channel type", "Type of channel returned by GET_CHANNEL.");
        RegisterParameter<bool>(DiscordVoiceParameter.Ready, "VRCOSC/Discord/Ready", ParameterMode.Write, "RPC ready", "True while the RPC connection is authenticated.");
        RegisterParameter<int>(DiscordVoiceParameter.LastErrorCode, "VRCOSC/Discord/LastErrorCode", ParameterMode.Write, "Last error code", "The most recent error code from the ERROR event.");
        RegisterParameter<int>(DiscordVoiceParameter.VoiceConnectionState, "VRCOSC/Discord/VoiceConnectionState", ParameterMode.Write, "Voice connection state", "State enum from VOICE_CONNECTION_STATUS (0 disconnected ... 7 voice connected).");
        RegisterParameter<bool>(DiscordVoiceParameter.SubscribeEvent, "VRCOSC/Discord/Subscribe/*/*", ParameterMode.ReadWrite, "Subscribe to event", "Wildcards: event name and optional id argument.");
        RegisterParameter<bool>(DiscordVoiceParameter.UnsubscribeEvent, "VRCOSC/Discord/Unsubscribe/*/*", ParameterMode.ReadWrite, "Unsubscribe from event", "Wildcards: event name and optional id argument.");
        RegisterParameter<bool>(DiscordVoiceParameter.SelectTextChannel, "VRCOSC/Discord/SelectTextChannel/*", ParameterMode.ReadWrite, "Join text channel", "Wildcard is channel id to join.");
        RegisterParameter<float>(DiscordVoiceParameter.SetUserVolume, "VRCOSC/Discord/UserVolume/*", ParameterMode.ReadWrite, "Set user volume", "Wildcard: user id; value is volume 0-200.");
        RegisterParameter<bool>(DiscordVoiceParameter.SetUserMute, "VRCOSC/Discord/UserMute/*", ParameterMode.ReadWrite, "Mute user", "Wildcard: user id to mute or unmute locally.");
        RegisterParameter<bool>(DiscordVoiceParameter.SetActivity, "VRCOSC/Discord/SetActivity/*/*", ParameterMode.ReadWrite, "Set Rich Presence", "Wildcards: state and details strings.");
        RegisterParameter<bool>(DiscordVoiceParameter.SendActivityJoinInvite, "VRCOSC/Discord/SendJoinInvite/*", ParameterMode.ReadWrite, "Send join invite", "Wildcard: user id.");
        RegisterParameter<bool>(DiscordVoiceParameter.CloseActivityRequest, "VRCOSC/Discord/CloseJoinRequest/*", ParameterMode.ReadWrite, "Close join request", "Wildcard: user id.");
        RegisterParameter<bool>(DiscordVoiceParameter.SendCertifiedDevices, "VRCOSC/Discord/SendCertifiedDevices", ParameterMode.ReadWrite, "Send certified devices", "Trigger to send example certified device info.");
        RegisterParameter<bool>(DiscordVoiceParameter.RequestGuildInfo, "VRCOSC/Discord/GetGuild/*", ParameterMode.ReadWrite, "Request guild info", "Wildcard guild id to fetch; updates GuildHasIcon.");
        RegisterParameter<bool>(DiscordVoiceParameter.GuildHasIcon, "VRCOSC/Discord/GuildHasIcon", ParameterMode.Write, "Guild has icon", "True if the requested guild has an icon.");
        RegisterParameter<int>(DiscordVoiceParameter.LastEventCode, "VRCOSC/Discord/LastEvent", ParameterMode.Write, "Last event code", "Numeric code of the most recent RPC event.");
    }

    private void RegisterChatBox()
    {
        CreateVariable<int>(DiscordVoiceVariable.GuildCount, "Guild Count");
        CreateVariable<int>(DiscordVoiceVariable.ChannelCount, "Channel Count");
        CreateVariable<int>(DiscordVoiceVariable.SelectedVoiceChannelId, "Voice Channel Id");
        var inputVar = CreateVariable<float>(DiscordVoiceVariable.InputVolume, "Input Volume");
        CreateVariable<float>(DiscordVoiceVariable.OutputVolume, "Output Volume");
        CreateVariable<int>(DiscordVoiceVariable.ChannelUserCount, "Channel User Count");
        CreateVariable<int>(DiscordVoiceVariable.ChannelType, "Channel Type");
        var readyVar = CreateVariable<bool>(DiscordVoiceVariable.Ready, "Ready");
        var errorVar = CreateVariable<int>(DiscordVoiceVariable.LastErrorCode, "Error Code");
        CreateVariable<int>(DiscordVoiceVariable.VoiceConnectionState, "Voice Connection State");
        var eventGuildVar = CreateVariable<int>(DiscordVoiceVariable.EventGuildId, "Event Guild Id");
        var eventChannelVar = CreateVariable<int>(DiscordVoiceVariable.EventChannelId, "Event Channel Id");
        var eventUserVar = CreateVariable<int>(DiscordVoiceVariable.EventUserId, "Event User Id");
        var eventMessageVar = CreateVariable<int>(DiscordVoiceVariable.EventMessageId, "Event Message Id");
        CreateVariable<int>(DiscordVoiceVariable.LastEventCode, "Last Event Code");


        CreateEvent(DiscordVoiceEvent.ReadyEvent, "Ready", "RPC Ready", Vars(readyVar));
        CreateEvent(DiscordVoiceEvent.ErrorEvent, "Error", "Error code {0}", Vars(errorVar));
        CreateEvent(DiscordVoiceEvent.GuildStatusEvent, "Guild Status", "Guild {0}", Vars(eventGuildVar));
        CreateEvent(DiscordVoiceEvent.GuildCreateEvent, "Guild Created", "Guild {0}", Vars(eventGuildVar));
        CreateEvent(DiscordVoiceEvent.ChannelCreateEvent, "Channel Created", "Channel {0}", Vars(eventChannelVar));
        CreateEvent(DiscordVoiceEvent.VoiceStateCreateEvent, "Voice Join", "User {0}", Vars(eventUserVar));
        CreateEvent(DiscordVoiceEvent.VoiceStateUpdateEvent, "Voice Update", "User {0}", Vars(eventUserVar));
        CreateEvent(DiscordVoiceEvent.VoiceStateDeleteEvent, "Voice Leave", "User {0}", Vars(eventUserVar));
        CreateEvent(DiscordVoiceEvent.VoiceSettingsEvent, "Voice Settings", "Input {0}%", Vars(inputVar));
        CreateEvent(DiscordVoiceEvent.SpeakingStartEvent, "Speaking Start", "User {0}", Vars(eventUserVar));
        CreateEvent(DiscordVoiceEvent.SpeakingStopEvent, "Speaking Stop", "User {0}", Vars(eventUserVar));
        CreateEvent(DiscordVoiceEvent.MessageCreateEvent, "Message Created", "Msg {0}", Vars(eventMessageVar));
        CreateEvent(DiscordVoiceEvent.MessageUpdateEvent, "Message Updated", "Msg {0}", Vars(eventMessageVar));
        CreateEvent(DiscordVoiceEvent.MessageDeleteEvent, "Message Deleted", "Msg {0}", Vars(eventMessageVar));
        CreateEvent(DiscordVoiceEvent.NotificationEvent, "Notification", "Channel {0}", Vars(eventChannelVar));
        CreateEvent(DiscordVoiceEvent.ActivityJoinEvent, "Activity Join", "Join");
        CreateEvent(DiscordVoiceEvent.ActivitySpectateEvent, "Activity Spectate", "Spectate");
        CreateEvent(DiscordVoiceEvent.ActivityJoinRequestEvent, "Join Request", "User {0}", Vars(eventUserVar));
    }

    private static IEnumerable<ClipVariableReference> Vars(params ClipVariableReference?[] refs) => refs.OfType<ClipVariableReference>();
}
