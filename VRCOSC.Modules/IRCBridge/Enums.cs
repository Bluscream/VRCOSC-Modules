// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

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
    LogMessages,
    RespondToCommands
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
    OnError,
    OnReady
}
