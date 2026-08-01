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
