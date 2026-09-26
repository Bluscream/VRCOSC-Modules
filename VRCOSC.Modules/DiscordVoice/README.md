# Discord Voice

Mute, deafen and voice-channel state for the running Discord client over its local RPC socket.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Attribution

This module is a fork of **DiscordOSC** by Yeusepe
(https://github.com/Yeusepe/Yeusepes-Modules, `YeusepesModules/DISCORDOSC/`), licensed under
the GNU General Public License v3.0. The OSC parameter surface (`VRCOSC/Discord/...`) is kept
compatible so avatars built for DiscordOSC keep working. This fork is distributed under the
same GPL-3.0 licence; see the LICENSE file in the repository root.

Changes from upstream: namespace and naming adapted to this repository, nullable-safe, a single
IPC reader loop that matches responses to requests by nonce (upstream had two readers competing
for the pipe), non-blocking ChatBox polling, and no built-in Discord application (bring your
own Client ID / Client Secret).

## Setup & Requirements

1. Create an application at https://discord.com/developers/applications, open its OAuth2 tab and
   copy the Client ID and Client Secret into the module settings.
2. Discord must be running on the same machine. The module connects to `discord-ipc-0` ..
   `discord-ipc-9`.
3. The RPC token is requested with the scopes `rpc rpc.voice.read rpc.voice.write`
   (`rpc` for the connection and channel events, `rpc.voice.read` for voice settings,
   `rpc.voice.write` for SET_VOICE_SETTINGS).

Note: Vesktop / Equibop implement RPC through arRPC, which only handles Rich Presence
(SET_ACTIVITY) and none of the voice commands. Use the official Discord client for RPC.

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Default Guild ID** | `TextBox` | `Guild ID used for guild-scoped subscriptions (GUILD_STATUS).` | `empty` |
| **Default Channel ID** | `TextBox` | `Channel ID used for channel-scoped subscriptions (VOICE_STATE_*, SPEAKING_*, MESSAGE_*).` | `empty` |
| **Auto Update Defaults** | `Toggle` | `Update the default guild and channel whenever you join a voice channel.` | `false` |
| **Max Speaking Names** | `Slider` | `Configure Max Speaking Names` | `"How many speakers the Speaking variable lists before collapsing the rest into \"+N\". 0 = unlimited.", 3, 0, 10` |
| **Speaking Hold (ms)** | `TextBox` | `How long a speaker stays listed after they stop talking, so short pauses do not flicker.` | `300` |
| **Client ID** | `TextBox` | `Client ID of your Discord application (Developer Portal, OAuth2 tab). Required for RPC.` | `empty` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

The `discord_*` variables use the MagicChatbox placeholder names so its layouts map 1:1
(`{bluscream.vrcosc.modules.discordvoicemodule_discord_channel}` and so on):

| Key | Meaning |
|---|---|
| `discord_channel` | Name of the voice channel you are in; empty when not in voice. |
| `discord_speaking` | Display names currently speaking, comma-separated, held for *Speaking Hold* ms after they stop, capped at *Max Speaking Names* with a `+N` tail. |
| `discord_mute_state` | `""`, `muted` or `deafened` (deafened implies muted). |
| `discord_muted` / `discord_deafened` | The same as separate booleans. |
| `discord_users` | Number of users in the voice channel (including you). |

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Guild Count** | `guildcount` | `int` | `ChatBox variable Guild Count` |
| **Channel Count** | `channelcount` | `int` | `ChatBox variable Channel Count` |
| **Voice Channel Id** | `selectedvoicechannelid` | `int` | `ChatBox variable Voice Channel Id` |
| **Input Volume** | `inputvolume` | `float` | `ChatBox variable Input Volume` |
| **Output Volume** | `outputvolume` | `float` | `ChatBox variable Output Volume` |
| **Channel User Count** | `channelusercount` | `int` | `ChatBox variable Channel User Count` |
| **Channel Type** | `channeltype` | `int` | `ChatBox variable Channel Type` |
| **Ready** | `ready` | `bool` | `ChatBox variable Ready` |
| **Error Code** | `lasterrorcode` | `int` | `ChatBox variable Error Code` |
| **Voice Connection State** | `voiceconnectionstate` | `int` | `ChatBox variable Voice Connection State` |
| **Event Guild Id** | `eventguildid` | `int` | `ChatBox variable Event Guild Id` |
| **Event Channel Id** | `eventchannelid` | `int` | `ChatBox variable Event Channel Id` |
| **Event User Id** | `eventuserid` | `int` | `ChatBox variable Event User Id` |
| **Event Message Id** | `eventmessageid` | `int` | `ChatBox variable Event Message Id` |
| **Last Event Code** | `lasteventcode` | `int` | `ChatBox variable Last Event Code` |
| **Voice Channel** | `VarChannel` | `string` | `ChatBox variable Voice Channel` |
| **Speaking** | `VarSpeaking` | `string` | `ChatBox variable Speaking` |
| **Mute State** | `VarMuteState` | `string` | `ChatBox variable Mute State` |
| **Muted** | `VarMuted` | `bool` | `ChatBox variable Muted` |
| **Deafened** | `VarDeafened` | `bool` | `ChatBox variable Deafened` |
| **Users In Channel** | `VarUsers` | `int` | `ChatBox variable Users In Channel` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Voice Connection State** | `voicestate` | `State: {0}` | `Voice Connection State state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Ready** | `readyevent` | `RPC Ready` | `Triggered on Ready` |
| **Error** | `errorevent` | `Error code {0}` | `Triggered on Error` |
| **Guild Status** | `guildstatusevent` | `Guild {0}` | `Triggered on Guild Status` |
| **Guild Created** | `guildcreateevent` | `Guild {0}` | `Triggered on Guild Created` |
| **Channel Created** | `channelcreateevent` | `Channel {0}` | `Triggered on Channel Created` |
| **Voice Join** | `voicestatecreateevent` | `User {0}` | `Triggered on Voice Join` |
| **Voice Update** | `voicestateupdateevent` | `User {0}` | `Triggered on Voice Update` |
| **Voice Leave** | `voicestatedeleteevent` | `User {0}` | `Triggered on Voice Leave` |
| **Voice Settings** | `voicesettingsevent` | `Input {0}%` | `Triggered on Voice Settings` |
| **Speaking Start** | `speakingstartevent` | `User {0}` | `Triggered on Speaking Start` |
| **Speaking Stop** | `speakingstopevent` | `User {0}` | `Triggered on Speaking Stop` |
| **Message Created** | `messagecreateevent` | `Msg {0}` | `Triggered on Message Created` |
| **Message Updated** | `messageupdateevent` | `Msg {0}` | `Triggered on Message Updated` |
| **Message Deleted** | `messagedeleteevent` | `Msg {0}` | `Triggered on Message Deleted` |
| **Notification** | `notificationevent` | `Channel {0}` | `Triggered on Notification` |
| **Activity Join** | `activityjoinevent` | `Join` | `Triggered on Activity Join` |
| **Activity Spectate** | `activityspectateevent` | `Spectate` | `Triggered on Activity Spectate` |
| **Join Request** | `activityjoinrequestevent` | `User {0}` | `Triggered on Join Request` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/Discord/Mic** | `bool` | `ReadWrite` | `Mute or unmute the Discord client. Also reflects the current mute state.` |
| **VRCOSC/Discord/Deafen** | `bool` | `ReadWrite` | `Deafen or undeafen the Discord client. Also reflects the current deafen state.` |
| **VRCOSC/Discord/GetGuilds** | `bool` | `ReadWrite` | `Trigger to fetch guilds and update GuildCount.` |
| **VRCOSC/Discord/GuildCount** | `int` | `Write` | `Number of guilds returned by GET_GUILDS.` |
| **VRCOSC/Discord/GetChannels/*** | `bool` | `ReadWrite` | `Send guild id as wildcard to fetch channels and update ChannelCount.` |
| **VRCOSC/Discord/ChannelCount** | `int` | `Write` | `Number of channels returned by GET_CHANNELS.` |
| **VRCOSC/Discord/SelectVoiceChannel/*** | `bool` | `ReadWrite` | `Send channel id as wildcard to join.` |
| **VRCOSC/Discord/GetCurrentVoiceChannel** | `bool` | `ReadWrite` | `Fetch the id of the currently joined voice channel.` |
| **VRCOSC/Discord/CurrentVoiceChannelId** | `int` | `Write` | `ID of the current voice channel (truncated to 32 bit), or 0 if none.` |
| **VRCOSC/Discord/GetVoiceSettings** | `bool` | `ReadWrite` | `Fetch input and output volumes.` |
| **VRCOSC/Discord/InputVolume** | `float` | `ReadWrite` | `Input volume percentage (0-100). Writing sets it.` |
| **VRCOSC/Discord/OutputVolume** | `float` | `ReadWrite` | `Output volume percentage (0-200). Writing sets it.` |
| **VRCOSC/Discord/SetInputVolume** | `float` | `ReadWrite` | `Set the input device volume.` |
| **VRCOSC/Discord/SetOutputVolume** | `float` | `ReadWrite` | `Set the output device volume.` |
| **VRCOSC/Discord/SetVoiceSettings/*/*** | `bool` | `ReadWrite` | `Usage: SetVoiceSettings/<field>/<value>. Fields: mute, deaf, input_volume, output_volume, agc, echo_cancellation, noise_suppression, qos, silence_warning.` |
| **VRCOSC/Discord/GetChannelUsers/*** | `bool` | `ReadWrite` | `Send channel id as wildcard to fetch user count.` |
| **VRCOSC/Discord/ChannelUserCount** | `int` | `Write` | `Number of users returned by GET_CHANNEL.` |
| **VRCOSC/Discord/GetChannelType/*** | `bool` | `ReadWrite` | `Send channel id as wildcard to fetch channel type.` |
| **VRCOSC/Discord/ChannelType** | `int` | `Write` | `Type of channel returned by GET_CHANNEL.` |
| **VRCOSC/Discord/Ready** | `bool` | `Write` | `True while the RPC connection is authenticated.` |
| **VRCOSC/Discord/LastErrorCode** | `int` | `Write` | `The most recent error code from the ERROR event.` |
| **VRCOSC/Discord/VoiceConnectionState** | `int` | `Write` | `State enum from VOICE_CONNECTION_STATUS (0 disconnected ... 7 voice connected).` |
| **VRCOSC/Discord/Subscribe/*/*** | `bool` | `ReadWrite` | `Wildcards: event name and optional id argument.` |
| **VRCOSC/Discord/Unsubscribe/*/*** | `bool` | `ReadWrite` | `Wildcards: event name and optional id argument.` |
| **VRCOSC/Discord/SelectTextChannel/*** | `bool` | `ReadWrite` | `Wildcard is channel id to join.` |
| **VRCOSC/Discord/UserVolume/*** | `float` | `ReadWrite` | `Wildcard: user id; value is volume 0-200.` |
| **VRCOSC/Discord/UserMute/*** | `bool` | `ReadWrite` | `Wildcard: user id to mute or unmute locally.` |
| **VRCOSC/Discord/SetActivity/*/*** | `bool` | `ReadWrite` | `Wildcards: state and details strings.` |
| **VRCOSC/Discord/SendJoinInvite/*** | `bool` | `ReadWrite` | `Wildcard: user id.` |
| **VRCOSC/Discord/CloseJoinRequest/*** | `bool` | `ReadWrite` | `Wildcard: user id.` |
| **VRCOSC/Discord/SendCertifiedDevices** | `bool` | `ReadWrite` | `Trigger to send example certified device info.` |
| **VRCOSC/Discord/GetGuild/*** | `bool` | `ReadWrite` | `Wildcard guild id to fetch; updates GuildHasIcon.` |
| **VRCOSC/Discord/GuildHasIcon** | `bool` | `Write` | `True if the requested guild has an icon.` |
| **VRCOSC/Discord/LastEvent** | `int` | `Write` | `Numeric code of the most recent RPC event.` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
