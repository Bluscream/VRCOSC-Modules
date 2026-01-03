// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Threading.Tasks;
using IrcDotNet;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

public partial class IRCBridgeModule
{
    // Higher-level methods using IrcDotNet's API (best practices)
    public async Task SendMessageToChannelAsync(string channelName, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var client = _ircClient.Client; // Store to avoid null reference warnings
        var channel = IRCClientUtils.FindChannel(client, channelName);
        if (channel != null && client.LocalUser != null)
        {
            await Task.Run(() => client.LocalUser.SendMessage(channel, message));
        }
        else
        {
            // Channel not found, send as raw message (fallback)
            await SendIRCMessage($"PRIVMSG {channelName} :{message}");
        }
    }

    public async Task SendMessageToUserAsync(string userName, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var client = _ircClient.Client; // Store to avoid null reference warnings
        var user = IRCClientUtils.FindUser(client, userName);
        if (user != null && client.LocalUser != null)
        {
            await Task.Run(() => client.LocalUser.SendMessage(user, message));
        }
        else
        {
            // User not found, send as raw message (fallback)
            await SendIRCMessage($"PRIVMSG {userName} :{message}");
        }
    }

    public async Task SendNoticeAsync(string target, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var client = _ircClient.Client; // Store to avoid null reference warnings
        if (client.LocalUser == null)
        {
            throw new InvalidOperationException("Local user not available");
        }

        // Try to find as channel first
        var channel = IRCClientUtils.FindChannel(client, target);
        if (channel != null)
        {
            await Task.Run(() => client.LocalUser.SendNotice(channel, message));
            return;
        }

        // Try to find as user
        var user = IRCClientUtils.FindUser(client, target);
        if (user != null)
        {
            await Task.Run(() => client.LocalUser.SendNotice(user, message));
            return;
        }

        // Fallback to raw message
        await SendIRCMessage($"NOTICE {target} :{message}");
    }

    private async Task SendActionMessageAsync(string channelName, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            return;
        }
        
        try
        {
            // Use CtcpClient for proper ACTION message formatting
            // This ensures the message is correctly formatted as a CTCP ACTION command
            var channel = IRCClientUtils.FindChannel(_ircClient?.Client, channelName);
            if (channel != null && _ctcpClient != null)
            {
                await Task.Run(() => _ctcpClient.SendAction(channel, message));
            }
            else
            {
                // Fallback: send as regular message if channel or CTCP client not found
                await SendMessageToChannelAsync(channelName, message);
            }
        }
        catch (Exception ex)
        {
            Log($"Error sending ACTION message: {ex.Message}");
        }
    }
}
