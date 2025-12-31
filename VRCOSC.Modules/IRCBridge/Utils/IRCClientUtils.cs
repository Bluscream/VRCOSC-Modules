// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Linq;
using IrcDotNet;

namespace Bluscream.Modules.IRCBridge.Utils;

public static class IRCClientUtils
{
    /// <summary>
    /// Finds a channel by name (case-insensitive)
    /// </summary>
    public static IrcChannel? FindChannel(IrcClient? client, string channelName)
    {
        return client?.Channels.FirstOrDefault(c => c.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Finds a user by nickname (case-insensitive)
    /// </summary>
    public static IrcUser? FindUser(IrcClient? client, string userName)
    {
        return client?.Users.FirstOrDefault(u => u.NickName.Equals(userName, StringComparison.OrdinalIgnoreCase));
    }
}
