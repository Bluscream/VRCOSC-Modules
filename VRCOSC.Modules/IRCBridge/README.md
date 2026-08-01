# IRC Bridge Module

Connect to IRC networks and Twitch IRC for chat integration, channel tracking, and pulse nodes.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- IRC server address (e.g. `irc.libera.chat`, `irc.chat.twitch.tv`).
- IRC nickname and optional server password / OAuth token.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Server** | `TextBox` | IRC server hostname or IP | `irc.libera.chat` |
| **Port** | `TextBox` | IRC server port | `6697` |
| **UseSSL** | `Toggle` | Use TLS/SSL connection | `true` |
| **Nickname** | `TextBox` | IRC nickname | `VRCOSC_User` |
| **Username** | `TextBox` | IRC username / ident | `vrcosc` |
| **RealName** | `TextBox` | IRC real name | `VRCOSC IRC Bridge` |
| **Password** | `TextBox` | Server password / OAuth token | `empty` |
| **Channels** | `TextBox` | Comma-separated channels to join on connect | `#vrcosc` |
| **AutoConnect** | `Toggle` | Automatically connect on module load | `true` |
| **LogDebug** | `Toggle` | Log detailed IRC messages to console | `false` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | True if connected to IRC server |
| **Server** | `server` | `string` | Current IRC server address |
| **Nickname** | `nickname` | `string` | Active IRC nickname |
| **Current Channel** | `currentchannel` | `string` | Most recently active channel |
| **Last Message** | `lastmessage` | `string` | Text of last received chat message |
| **Last Sender** | `lastsender` | `string` | Nickname of last message sender |
| **Message Count** | `messagecount` | `int` | Total IRC messages received |
| **Channel Count** | `channelcount` | `int` | Number of joined IRC channels |
| **User Count** | `usercount` | `int` | Tracked users in active channel |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `IRC Disconnected` | Disconnected from server |
| **Connecting** | `connecting` | `IRC Connecting to {0}...` | Connecting to IRC server |
| **Connected** | `connected` | `IRC Connected to {0}` | Connected to server |
| **JoinedChannel** | `joinedchannel` | `IRC Joined {0}` | Joined target channel |
| **Reconnecting** | `reconnecting` | `IRC Reconnecting...` | Attempting auto-reconnect |
| **Error** | `error` | `IRC Error: {0}` | Connection error |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Connected** | `onconnected` | `Connected to {0}` | Triggered on successful server connection |
| **On Disconnected** | `ondisconnected` | `Disconnected` | Triggered on disconnect |
| **On Message Received** | `onmessagereceived` | `<{0}> {1}` | Triggered on incoming IRC chat message |
| **On Channel Joined** | `onchanneljoined` | `Joined {0}` | Triggered when joining a channel |
| **On Channel Parted** | `onchannelparted` | `Parted {0}` | Triggered when leaving a channel |
| **On User Joined** | `onuserjoined` | `{0} joined {1}` | Triggered when a user joins channel |
| **On User Parted** | `onuserparted` | `{0} left {1}` | Triggered when a user leaves channel |
| **On Nick Changed** | `onnickchanged` | `Nick changed to {0}` | Triggered when nickname changes |
| **On Error** | `onerror` | `IRC Error: {0}` | Triggered on IRC protocol or socket error |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/IRC/Connected` | `bool` | `Write` | True if connected to IRC server |
| `VRCOSC/IRC/MessageReceived` | `bool` | `Write` | Flashes true on incoming chat message |
| `VRCOSC/IRC/MessageCount` | `int` | `Write` | Total IRC messages received |
| `VRCOSC/IRC/ChannelCount` | `int` | `Write` | Number of joined channels |
| `VRCOSC/IRC/Error` | `bool` | `Write` | True if IRC is in error state |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Join IRC Channel** | Channel (string) | Success (bool) | Joins specified IRC channel |
| **Part IRC Channel** | Channel (string), Reason (string) | Success (bool) | Leaves specified IRC channel |
| **Send IRC Message** | Target (string), Message (string) | Success (bool) | Sends chat message to channel or user |
| **Send Raw IRC Command** | Raw Command (string) | Success (bool) | Sends raw IRC command line |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Server** | `TextBox` | `IRC server hostname or IP` | `irc.libera.chat` |
| **Port** | `TextBox` | `IRC server port` | `6697` |
| **UseSSL** | `Toggle` | `Use TLS/SSL connection` | `true` |
| **Nickname** | `TextBox` | `IRC nickname` | `VRCOSC_User` |
| **Username** | `TextBox` | `IRC username / ident` | `vrcosc` |
| **RealName** | `TextBox` | `IRC real name` | `VRCOSC IRC Bridge` |
| **Password** | `TextBox` | `Server password / OAuth token` | `empty` |
| **Channels** | `TextBox` | `Comma-separated channels to join on connect` | `#vrcosc` |
| **AutoConnect** | `Toggle` | `Automatically connect on module load` | `true` |
| **LogDebug** | `Toggle` | `Log detailed IRC messages to console` | `false` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Connected** | `connected` | `bool` | `True if connected to IRC server` |
| **Server** | `server` | `string` | `Current IRC server address` |
| **Nickname** | `nickname` | `string` | `Active IRC nickname` |
| **Current Channel** | `currentchannel` | `string` | `Most recently active channel` |
| **Last Message** | `lastmessage` | `string` | `Text of last received chat message` |
| **Last Sender** | `lastsender` | `string` | `Nickname of last message sender` |
| **Message Count** | `messagecount` | `int` | `Total IRC messages received` |
| **Channel Count** | `channelcount` | `int` | `Number of joined IRC channels` |
| **User Count** | `usercount` | `int` | `Tracked users in active channel` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `IRC Disconnected` | `Disconnected from server` |
| **Connecting** | `connecting` | `IRC Connecting to {0}...` | `Connecting to IRC server` |
| **Connected** | `connected` | `IRC Connected to {0}` | `Connected to server` |
| **JoinedChannel** | `joinedchannel` | `IRC Joined {0}` | `Joined target channel` |
| **Reconnecting** | `reconnecting` | `IRC Reconnecting...` | `Attempting auto-reconnect` |
| **Error** | `error` | `IRC Error: {0}` | `Connection error` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Connected** | `onconnected` | `Connected to {0}` | `Triggered on successful server connection` |
| **On Disconnected** | `ondisconnected` | `Disconnected` | `Triggered on disconnect` |
| **On Message Received** | `onmessagereceived` | `<{0}> {1}` | `Triggered on incoming IRC chat message` |
| **On Channel Joined** | `onchanneljoined` | `Joined {0}` | `Triggered when joining a channel` |
| **On Channel Parted** | `onchannelparted` | `Parted {0}` | `Triggered when leaving a channel` |
| **On User Joined** | `onuserjoined` | `{0} joined {1}` | `Triggered when a user joins channel` |
| **On User Parted** | `onuserparted` | `{0} left {1}` | `Triggered when a user leaves channel` |
| **On Nick Changed** | `onnickchanged` | `Nick changed to {0}` | `Triggered when nickname changes` |
| **On Error** | `onerror` | `IRC Error: {0}` | `Triggered on IRC protocol or socket error` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/IRC/Connected** | `bool` | `Write` | `True if connected to IRC server` |
| **VRCOSC/IRC/MessageReceived** | `bool` | `Write` | `Flashes true on incoming chat message` |
| **VRCOSC/IRC/MessageCount** | `int` | `Write` | `Total IRC messages received` |
| **VRCOSC/IRC/ChannelCount** | `int` | `Write` | `Number of joined channels` |
| **VRCOSC/IRC/Error** | `bool` | `Write` | `True if IRC is in error state` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Join IRC Channel** | `Channel (string)` | `Success (bool)` | `Joins specified IRC channel` |
| **Part IRC Channel** | `Channel (string), Reason (string)` | `Success (bool)` | `Leaves specified IRC channel` |
| **Send IRC Message** | `Target (string), Message (string)` | `Success (bool)` | `Sends chat message to channel or user` |
| **Send Raw IRC Command** | `Raw Command (string)` | `Success (bool)` | `Sends raw IRC command line` |
<!-- AUTOGEN:NODES:END -->

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Server Address** | `TextBox` | `IRC server address (e.g., irc.example.com)` | `"irc.efnet.org"` |
| **Server Port** | `TextBox` | `IRC server port (typically 6667 for non-SSL, 6697 for SSL)` | `6667` |
| **Use SSL/TLS** | `Toggle` | `Enable SSL/TLS encryption` | `false` |
| **Channel** | `TextBox` | `IRC channel to join (include # prefix)` | `"#test"` |
| **Nickname** | `TextBox` | `Your IRC nickname (leave empty to use VRC display name)` | `empty` |
| **Username** | `TextBox` | `Your IRC username / ident (leave empty to use nickname)` | `empty` |
| **Server Password** | `TextBox` | `IRC server password (if required, leave empty if not)` | `empty` |
| **NickServ Name** | `TextBox` | `NickServ account name (for authentication)` | `empty` |
| **NickServ Password** | `TextBox` | `NickServ account password (for authentication)` | `empty` |
| **Auto Reconnect** | `Toggle` | `Automatically reconnect if connection is lost` | `true` |
| **Reconnect Delay (ms)** | `TextBox` | `Delay before reconnect attempt` | `5000` |
| **Message Cooldown (ms)** | `TextBox` | `Minimum time between processing same event type` | `100` |
| **Log Chat Messages** | `Toggle` | `Log incoming and outgoing channel/private messages from users` | `false` |
| **Log System Messages** | `Toggle` | `Log server responses and system messages (numeric codes, etc.)` | `false` |
| **Log Events** | `Toggle` | `Log IRC events (JOIN, NICK, MODE, etc.)` | `false` |
| **Respond To Commands** | `Toggle` | `Respond to chat commands (e.g., @bot ping, @bot time)` | `true` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Server Status** | `serverstatus` | `string` | `ChatBox variable Server Status` |
| **Channel Name** | `channelname` | `string` | `ChatBox variable Channel Name` |
| **Nickname** | `nickname` | `string` | `ChatBox variable Nickname` |
| **Last Message** | `lastmessage` | `string` | `ChatBox variable Last Message` |
| **Last Message User** | `lastmessageuser` | `string` | `ChatBox variable Last Message User` |
| **Last Joined User** | `lastjoineduser` | `string` | `ChatBox variable Last Joined User` |
| **Last Left User** | `lastleftuser` | `string` | `ChatBox variable Last Left User` |
| **User Count** | `usercount` | `int` | `ChatBox variable User Count` |
| **Last Event Time** | `lasteventtime` | `string` | `ChatBox variable Last Event Time` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Disconnected** | `disconnected` | `IRC Bridge: Disconnected` | `Disconnected state` |
| **Connecting** | `connecting` | `IRC Bridge: Connecting...` | `Connecting state` |
| **Connected** | `connected` | `IRC Bridge: Connected\nServer: {0}` | `Connected state` |
| **Joining** | `joining` | `IRC Bridge: Joining channel...` | `Joining state` |
| **Joined** | `joined` | `IRC Bridge: Joined\nChannel: {0}` | `Joined state` |
| **Error** | `error` | `IRC Bridge: Error\n{0}` | `Error state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Connected** | `onconnected` | `On Connected` | `Triggered on On Connected` |
| **On Disconnected** | `ondisconnected` | `On Disconnected` | `Triggered on On Disconnected` |
| **On Channel Joined** | `onchanneljoined` | `On Channel Joined` | `Triggered on On Channel Joined` |
| **On Channel Left** | `onchannelleft` | `On Channel Left` | `Triggered on On Channel Left` |
| **On User Joined** | `onuserjoined` | `On User Joined` | `Triggered on On User Joined` |
| **On User Left** | `onuserleft` | `On User Left` | `Triggered on On User Left` |
| **On Message Received** | `onmessagereceived` | `On Message Received` | `Triggered on On Message Received` |
| **On Error** | `onerror` | `On Error` | `Triggered on On Error` |
| **On Ready** | `onready` | `On Ready` | `Triggered on On Ready` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/IRCBridge/Connected** | `bool` | `Write` | `True when connected to IRC server` |
| **VRCOSC/IRCBridge/UserCount** | `int` | `Write` | `Number of users in channel` |
| **VRCOSC/IRCBridge/MessageReceived** | `bool` | `Write` | `True for 1 second when message is received` |
| **VRCOSC/IRCBridge/UserJoined** | `bool` | `Write` | `True for 1 second when user joins` |
| **VRCOSC/IRCBridge/UserLeft** | `bool` | `Write` | `True for 1 second when user leaves` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **I R C Connect** | `Flow trigger` | `Output` | `Node node for I R C Connect` |
| **I R C Disconnect** | `Flow trigger` | `Output` | `Node node for I R C Disconnect` |
| **I R C Send Message** | `Flow trigger` | `Output` | `Node node for I R C Send Message` |
| **I R C Join Channel** | `Flow trigger` | `Output` | `Node node for I R C Join Channel` |
| **I R C Leave Channel** | `Flow trigger` | `Output` | `Node node for I R C Leave Channel` |
| **I R C Connection Status** | `Flow trigger` | `Output` | `Node node for I R C Connection Status` |
| **I R C Get Last Message** | `Flow trigger` | `Output` | `Node node for I R C Get Last Message` |
| **I R C Get Last Joined User** | `Flow trigger` | `Output` | `Node node for I R C Get Last Joined User` |
| **I R C Get Last Left User** | `Flow trigger` | `Output` | `Node node for I R C Get Last Left User` |
| **I R C Get Channel User List** | `Flow trigger` | `Output` | `Node node for I R C Get Channel User List` |
| **I R C Change Nickname** | `Flow trigger` | `Output` | `Node node for I R C Change Nickname` |
| **On I R C User Joined** | `Flow trigger` | `Output` | `Node node for On I R C User Joined` |
| **On I R C User Left** | `Flow trigger` | `Output` | `Node node for On I R C User Left` |
| **On I R C Message Received** | `Flow trigger` | `Output` | `Node node for On I R C Message Received` |
| **On I R C Connected** | `Flow trigger` | `Output` | `Node node for On I R C Connected` |
| **On I R C Disconnected** | `Flow trigger` | `Output` | `Node node for On I R C Disconnected` |
| **On I R C Channel Joined** | `Flow trigger` | `Output` | `Node node for On I R C Channel Joined` |
| **On I R C Error** | `Flow trigger` | `Output` | `Node node for On I R C Error` |
<!-- NODES_TABLE_END -->
