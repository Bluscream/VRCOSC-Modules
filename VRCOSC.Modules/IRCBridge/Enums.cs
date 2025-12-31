// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

namespace Bluscream.Modules;

public enum IRCBridgeSetting
{
    ServerAddress,
    ServerPort,
    UseSSL,
    Channel,
    Nickname,
    Username,
    Password,
    NickServName,
    NickServPassword,
    AutoReconnect,
    ReconnectDelay,
    MessageCooldown,
    LogMessages
}

public enum IRCBridgeParameter
{
    Connected,
    UserCount,
    MessageReceived,
    UserJoined,
    UserLeft
}

public enum IRCBridgeVariable
{
    ServerStatus,
    ChannelName,
    Nickname,
    LastMessage,
    LastMessageUser,
    LastJoinedUser,
    LastLeftUser,
    UserCount,
    LastEventTime
}

public enum IRCBridgeState
{
    Disconnected,
    Connecting,
    Connected,
    Joining,
    Joined,
    Error
}

public enum IRCBridgeEvent
{
    OnConnected,
    OnDisconnected,
    OnChannelJoined,
    OnChannelLeft,
    OnUserJoined,
    OnUserLeft,
    OnMessageReceived,
    OnError
}
