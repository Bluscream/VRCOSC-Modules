// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Threading.Tasks;
using IrcDotNet;
using VRCOSC.App.SDK.Parameters;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

public partial class IRCBridgeModule
{
    // Event handlers for IrcDotNet higher-level events (following IrcBot best practices)
    private void LocalUser_JoinedChannel(object? sender, IrcChannelEventArgs e)
    {
        var channel = e.Channel;
        _joinedChannel = channel;
        
        // Subscribe to channel events when we join (best practice from IrcBot)
        channel.UserJoined += Channel_UserJoined;
        channel.UserLeft += Channel_UserLeft;
        channel.MessageReceived += Channel_MessageReceived;
        
        ChangeState(IRCBridgeState.Joined);
        SetVariableValue(IRCBridgeVariable.ChannelName, channel.Name);
        TriggerEvent(IRCBridgeEvent.OnChannelJoined);
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        // Trigger pulse graph node with channel name and user count
        _ = TriggerModuleNodeAsync(typeof(OnIRCChannelJoinedNode), new object[] { channel.Name, userCount });
        
        // Send client data as ACTION message (fire-and-forget)
        _ = SendClientDataAnnouncementWithDelayAsync(channel.Name);
    }

    private void LocalUser_LeftChannel(object? sender, IrcChannelEventArgs e)
    {
        var channel = e.Channel;
        
        // Unsubscribe from channel events when we leave (best practice from IrcBot)
        channel.UserJoined -= Channel_UserJoined;
        channel.UserLeft -= Channel_UserLeft;
        channel.MessageReceived -= Channel_MessageReceived;
        
        ChangeState(IRCBridgeState.Connected);
        TriggerEvent(IRCBridgeEvent.OnChannelLeft);
        Log($"Left channel: {channel.Name}");
    }

    private void Channel_UserJoined(object? sender, IrcChannelUserEventArgs e)
    {
        if (sender is not IrcChannel channel)
        {
            return;
        }
        
        var user = e.ChannelUser.User;
        
        // Skip if it's us
        if (user is IrcLocalUser)
        {
            return;
        }
        
        var eventTime = DateTime.Now.ToString("HH:mm:ss");
        
        SetVariableValue(IRCBridgeVariable.LastJoinedUser, user.NickName);
        SetVariableValue(IRCBridgeVariable.LastEventTime, eventTime);
        TriggerEvent(IRCBridgeEvent.OnUserJoined);
        _ = TriggerModuleNodeAsync(typeof(OnIRCUserJoinedNode), new object[] { user.NickName, channel.Name, eventTime });
        
        _ = SendParameterPulseAsync(IRCBridgeParameter.UserJoined);
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogEvents))
        {
            Log($"User joined: {user.NickName}");
        }
    }

    private void Channel_UserLeft(object? sender, IrcChannelUserEventArgs e)
    {
        if (sender is not IrcChannel channel)
        {
            return;
        }
        
        var user = e.ChannelUser.User;
        
        // Skip if it's us
        if (user is IrcLocalUser)
        {
            return;
        }
        
        var eventTime = DateTime.Now.ToString("HH:mm:ss");
        
        SetVariableValue(IRCBridgeVariable.LastLeftUser, user.NickName);
        SetVariableValue(IRCBridgeVariable.LastEventTime, eventTime);
        TriggerEvent(IRCBridgeEvent.OnUserLeft);
        _ = TriggerModuleNodeAsync(typeof(OnIRCUserLeftNode), new object[] { user.NickName, channel.Name, eventTime });
        
        _ = SendParameterPulseAsync(IRCBridgeParameter.UserLeft);
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogEvents))
        {
            Log($"User left: {user.NickName}");
        }
    }

    private void Channel_MessageReceived(object? sender, IrcMessageEventArgs e)
    {
        if (sender is not IrcChannel channel)
        {
            return;
        }
        
        var user = e.Source as IrcUser;
        
        if (user == null)
        {
            return;
        }
        
        var message = e.Text;
        var eventTime = DateTime.Now.ToString("HH:mm:ss");
        
        SetVariableValue(IRCBridgeVariable.LastMessage, message);
        SetVariableValue(IRCBridgeVariable.LastMessageUser, user.NickName);
        SetVariableValue(IRCBridgeVariable.LastEventTime, eventTime);
        TriggerEvent(IRCBridgeEvent.OnMessageReceived);
        _ = TriggerModuleNodeAsync(typeof(OnIRCMessageReceivedNode), new object[] { message, user.NickName, channel.Name, eventTime });
        
        _ = SendParameterPulseAsync(IRCBridgeParameter.MessageReceived);
        
        // Handle commands: "@<botnickname> <command>" (only if enabled)
        if (GetSettingValue<bool>(IRCBridgeSetting.RespondToCommands) && _ircClient?.Client?.LocalUser != null)
        {
            var botNickname = _ircClient.Client.LocalUser.NickName;
            if (!string.IsNullOrEmpty(botNickname))
            {
                var messageLower = message.ToLowerInvariant();
                var botNickLower = botNickname.ToLowerInvariant();
                
                // Handle ping/pong command: "@<botnickname> ping" -> "@<username> pong"
                var pingPattern = $@"@{System.Text.RegularExpressions.Regex.Escape(botNickLower)}\s+ping\b";
                if (System.Text.RegularExpressions.Regex.IsMatch(messageLower, pingPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // Respond with pong
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SendMessageToChannelAsync(channel.Name, $"@{user.NickName} pong");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error sending pong response: {ex.Message}");
                        }
                    });
                }
                
                // Handle time command: "@<botnickname> time" -> "@<username> <RFC local time>"
                var timePattern = $@"@{System.Text.RegularExpressions.Regex.Escape(botNickLower)}\s+time\b";
                if (System.Text.RegularExpressions.Regex.IsMatch(messageLower, timePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // Respond with current local time in RFC 3339 format
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var localTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"); // RFC 3339 format
                            await SendMessageToChannelAsync(channel.Name, $"@{user.NickName} {localTime}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error sending time response: {ex.Message}");
                        }
                    });
                }
                
                // Handle version command: "@<botnickname> version" -> "@<username> <version info>"
                var versionPattern = $@"@{System.Text.RegularExpressions.Regex.Escape(botNickLower)}\s+version\b";
                if (System.Text.RegularExpressions.Regex.IsMatch(messageLower, versionPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    // Respond with version information
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var versionString = ClientDataBuilder.GetVersionString();
                            await SendMessageToChannelAsync(channel.Name, $"@{user.NickName} {versionString}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error sending version response: {ex.Message}");
                        }
                    });
                }
            }
        }
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogChatMessages))
        {
            Log($"<{user.NickName}> {message}");
        }
    }
    
    private async Task SendParameterPulseAsync(IRCBridgeParameter parameter, int durationMs = 1000)
    {
        if (_isStopping) return;
        
        try
        {
            SendParameterSafePublic(parameter, true);
            await Task.Delay(durationMs);
            SendParameterSafePublic(parameter, false);
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }
}
