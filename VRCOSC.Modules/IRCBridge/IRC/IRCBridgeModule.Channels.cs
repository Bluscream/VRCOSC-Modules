// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

public partial class IRCBridgeModule
{
    public async Task JoinChannelAsync(string channelName)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        await Task.Run(() => _ircClient.Client.Channels.Join(channelName));
    }

    public async Task LeaveChannelAsync(string channelName, string? reason = null)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var channel = IRCClientUtils.FindChannel(_ircClient?.Client, channelName);
        if (channel != null)
        {
            await Task.Run(() => channel.Leave(reason));
        }
    }

    public List<string> GetChannelUserList(string? channelName = null)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            return new List<string>();
        }

        // Use provided channel name or default to current channel
        if (string.IsNullOrEmpty(channelName))
        {
            channelName = GetChannelName();
        }

        if (string.IsNullOrEmpty(channelName))
        {
            return new List<string>();
        }

        // Ensure channel starts with #
        if (!channelName.StartsWith("#"))
        {
            channelName = "#" + channelName;
        }

        var channel = IRCClientUtils.FindChannel(_ircClient?.Client, channelName);
        if (channel == null)
        {
            return new List<string>();
        }

        // Return list of user nicknames
        return channel.Users.Select(cu => cu.User.NickName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task ChangeNicknameAsync(string newNickname)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        if (string.IsNullOrWhiteSpace(newNickname))
        {
            throw new ArgumentException("Nickname cannot be empty", nameof(newNickname));
        }

        await Task.Run(() => _ircClient.Client.LocalUser.SetNickName(newNickname));
        
        // Update variable
        SetVariableValue(IRCBridgeVariable.Nickname, newNickname);
    }
}
