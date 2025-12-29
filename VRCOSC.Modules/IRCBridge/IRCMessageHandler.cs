// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bluscream.Modules;

public class IRCMessageHandler
{
    private readonly IRCBridgeModule _module;
    private readonly Dictionary<string, DateTime> _lastEventTimes = new();
    private readonly object _eventLock = new();
    private bool _hasJoinedChannel = false; // Track if we've already joined to avoid duplicate joins
    private readonly Dictionary<string, int> _rateLimits = new(); // Store server rate limits from ISUPPORT

    public IRCMessageHandler(IRCBridgeModule module)
    {
        _module = module;
    }

    public Task ProcessMessageAsync(string message, IRCClient client)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.CompletedTask;
        }

        // PING/PONG is handled automatically by IrcDotNet
        if (message.StartsWith("PING "))
        {
            return Task.CompletedTask;
        }

        // Parse IRC message format: :prefix COMMAND params...
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return Task.CompletedTask;
        }

        var prefix = parts[0];
        var command = parts[1];
        var parameters = parts.Skip(2).ToList();

        // Handle numeric responses
        if (int.TryParse(command, out var numeric))
        {
            return HandleNumericResponseAsync(numeric, parameters, client);
        }

        // Handle command responses
        return command.ToUpperInvariant() switch
        {
            "JOIN" => HandleJoinAsync(prefix, parameters),
            "PART" => HandlePartAsync(prefix, parameters),
            "QUIT" => HandleQuitAsync(prefix, parameters),
            "PRIVMSG" => HandlePrivMsgAsync(prefix, parameters),
            "NICK" => HandleNickChangeAsync(prefix, parameters),
            "MODE" => HandleModeAsync(prefix, parameters),
            "NOTICE" => HandleNoticeAsync(prefix, parameters),
            _ => Task.CompletedTask
        };
    }

    private async Task HandleNumericResponseAsync(int numeric, List<string> parameters, IRCClient client)
    {
        switch (numeric)
        {
            case 001: // RPL_WELCOME - Only handle once
                if (!_hasJoinedChannel)
                {
                    _hasJoinedChannel = true;
                    if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                    {
                        _module.Log($"IRC: {string.Join(" ", parameters.Skip(1))}");
                    }
                    
                    // Authenticate with NickServ if configured
                    var nickServName = _module.GetSettingValue<string>(IRCBridgeSetting.NickServName);
                    var nickServPassword = _module.GetSettingValue<string>(IRCBridgeSetting.NickServPassword);
                    
                    if (!string.IsNullOrEmpty(nickServName) && !string.IsNullOrEmpty(nickServPassword))
                    {
                        await Task.Delay(2000); // Wait a bit before authenticating
                        await client.SendMessageAsync($"PRIVMSG NickServ :IDENTIFY {nickServName} {nickServPassword}");
                    }
                    
                    // Join channel
                    var channel = _module.GetSettingValue<string>(IRCBridgeSetting.Channel);
                    if (!string.IsNullOrEmpty(channel))
                    {
                        _module.ChangeStatePublic(IRCBridgeState.Joining);
                        _module.SetVariableValuePublic(IRCBridgeVariable.ChannelName, channel);
                        await client.SendMessageAsync($"JOIN {channel}");
                    }
                }
                break;
            case 005: // RPL_ISUPPORT - Parse server capabilities and limits
                ParseISupport(parameters);
                break;
            case 353: // RPL_NAMREPLY - Channel user list (may be split across multiple lines)
                // Note: User count should be accumulated across all 353 responses, then finalized on 366
                // For simplicity, we'll update on each 353, but the final count comes on 366
                if (parameters.Count >= 4)
                {
                    var users = parameters[3].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var userCount = users.Length;
                    _module.SetVariableValuePublic(IRCBridgeVariable.UserCount, userCount);
                    _module.SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
                }
                break;
            case 366: // RPL_ENDOFNAMES - End of channel user list
                _module.ChangeStatePublic(IRCBridgeState.Joined);
                _module.TriggerEventPublic(IRCBridgeEvent.OnChannelJoined);
                // Recalculate user count from all 353 responses received
                break;
            case 433: // ERR_NICKNAMEINUSE
                if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    _module.Log("Nickname already in use, trying alternative...");
                }
                var currentNick = _module.GetSettingValue<string>(IRCBridgeSetting.Nickname);
                await client.SendMessageAsync($"NICK {currentNick}_");
                break;
        }
    }

    private Task HandleJoinAsync(string prefix, List<string> parameters)
    {
        if (parameters.Count < 1)
        {
            return Task.CompletedTask;
        }

        var channel = parameters[0];
        var user = ExtractNickFromPrefix(prefix);
        
        if (string.IsNullOrEmpty(user))
        {
            return Task.CompletedTask;
        }

        // Check if it's us joining
        var ourNick = _module.GetVariableValue<string>(IRCBridgeVariable.Nickname);
        if (user.Equals(ourNick, StringComparison.OrdinalIgnoreCase))
        {
            _module.ChangeStatePublic(IRCBridgeState.Joined);
            _module.SetVariableValuePublic(IRCBridgeVariable.ChannelName, channel);
            _module.TriggerEventPublic(IRCBridgeEvent.OnChannelJoined);
            if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
            {
                _module.Log($"Joined channel: {channel}");
            }
        }
        else
        {
            // Another user joined
            if (ShouldProcessEvent("UserJoined", user))
            {
                _module.SetVariableValuePublic(IRCBridgeVariable.LastJoinedUser, user);
                _module.SetVariableValuePublic(IRCBridgeVariable.LastEventTime, DateTime.Now.ToString("HH:mm:ss"));
                _module.TriggerEventPublic(IRCBridgeEvent.OnUserJoined);
                try
                {
                    _module.SendParameterSafePublic(IRCBridgeParameter.UserJoined, true);
                    _ = Task.Delay(1000).ContinueWith(_ => 
                    {
                        try
                        {
                            _module.SendParameterSafePublic(IRCBridgeParameter.UserJoined, false);
                        }
                        catch { }
                    });
                }
                catch { }
                
                // Update user count (increment)
                var currentCount = _module.GetVariableValue<int>(IRCBridgeVariable.UserCount);
                _module.SetVariableValuePublic(IRCBridgeVariable.UserCount, currentCount + 1);
                _module.SendParameterSafePublic(IRCBridgeParameter.UserCount, currentCount + 1);
                
                if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    _module.Log($"User joined: {user}");
                }
            }
        }
        return Task.CompletedTask;
    }

    private Task HandlePartAsync(string prefix, List<string> parameters)
    {
        if (parameters.Count < 1)
        {
            return Task.CompletedTask;
        }

        var channel = parameters[0];
        var user = ExtractNickFromPrefix(prefix);
        
        if (string.IsNullOrEmpty(user))
        {
            return Task.CompletedTask;
        }

        // Check if it's us leaving
        var ourNick = _module.GetVariableValue<string>(IRCBridgeVariable.Nickname);
        if (user.Equals(ourNick, StringComparison.OrdinalIgnoreCase))
        {
            _module.ChangeStatePublic(IRCBridgeState.Connected);
            _module.TriggerEventPublic(IRCBridgeEvent.OnChannelLeft);
            if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
            {
                _module.Log($"Left channel: {channel}");
            }
        }
        else
        {
            // Another user left
            if (ShouldProcessEvent("UserLeft", user))
            {
                _module.SetVariableValuePublic(IRCBridgeVariable.LastLeftUser, user);
                _module.SetVariableValuePublic(IRCBridgeVariable.LastEventTime, DateTime.Now.ToString("HH:mm:ss"));
                _module.TriggerEventPublic(IRCBridgeEvent.OnUserLeft);
                try
                {
                    _module.SendParameterSafePublic(IRCBridgeParameter.UserLeft, true);
                    _ = Task.Delay(1000).ContinueWith(_ => 
                    {
                        try
                        {
                            _module.SendParameterSafePublic(IRCBridgeParameter.UserLeft, false);
                        }
                        catch { }
                    });
                }
                catch { }
                
                // Update user count (decrement)
                var currentCount = _module.GetVariableValue<int>(IRCBridgeVariable.UserCount);
                if (currentCount > 0)
                {
                    _module.SetVariableValuePublic(IRCBridgeVariable.UserCount, currentCount - 1);
                    _module.SendParameterSafePublic(IRCBridgeParameter.UserCount, currentCount - 1);
                }
                
                if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    _module.Log($"User left: {user}");
                }
            }
        }
        return Task.CompletedTask;
    }

    private Task HandleQuitAsync(string prefix, List<string> parameters)
    {
        var user = ExtractNickFromPrefix(prefix);
        
        if (string.IsNullOrEmpty(user))
        {
            return Task.CompletedTask;
        }

        // Check if it's us quitting
        var ourNick = _module.GetVariableValue<string>(IRCBridgeVariable.Nickname);
        if (user.Equals(ourNick, StringComparison.OrdinalIgnoreCase))
        {
            // We quit, but this shouldn't happen normally
            return Task.CompletedTask;
        }
        else
        {
            // Another user quit (treated as leaving)
            if (ShouldProcessEvent("UserLeft", user))
            {
                _module.SetVariableValuePublic(IRCBridgeVariable.LastLeftUser, user);
                _module.SetVariableValuePublic(IRCBridgeVariable.LastEventTime, DateTime.Now.ToString("HH:mm:ss"));
                _module.TriggerEventPublic(IRCBridgeEvent.OnUserLeft);
                try
                {
                    _module.SendParameterSafePublic(IRCBridgeParameter.UserLeft, true);
                    _ = Task.Delay(1000).ContinueWith(_ => 
                    {
                        try
                        {
                            _module.SendParameterSafePublic(IRCBridgeParameter.UserLeft, false);
                        }
                        catch { }
                    });
                }
                catch { }
                
                // Update user count (decrement)
                var currentCount = _module.GetVariableValue<int>(IRCBridgeVariable.UserCount);
                if (currentCount > 0)
                {
                    _module.SetVariableValuePublic(IRCBridgeVariable.UserCount, currentCount - 1);
                    _module.SendParameterSafePublic(IRCBridgeParameter.UserCount, currentCount - 1);
                }
                
                if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    _module.Log($"User quit: {user}");
                }
            }
        }
        return Task.CompletedTask;
    }

    private Task HandlePrivMsgAsync(string prefix, List<string> parameters)
    {
        if (parameters.Count < 2)
        {
            return Task.CompletedTask;
        }

        var target = parameters[0];
        var message = string.Join(" ", parameters.Skip(1)).TrimStart(':');

        var user = ExtractNickFromPrefix(prefix);
        
        if (string.IsNullOrEmpty(user))
        {
            return Task.CompletedTask;
        }

        // Only process channel messages (target starts with #)
        if (target.StartsWith("#"))
        {
            if (ShouldProcessEvent("MessageReceived", $"{user}:{message}"))
            {
                _module.SetVariableValuePublic(IRCBridgeVariable.LastMessage, message);
                _module.SetVariableValuePublic(IRCBridgeVariable.LastMessageUser, user);
                _module.SetVariableValuePublic(IRCBridgeVariable.LastEventTime, DateTime.Now.ToString("HH:mm:ss"));
                _module.TriggerEventPublic(IRCBridgeEvent.OnMessageReceived);
                try
                {
                    _module.SendParameterSafePublic(IRCBridgeParameter.MessageReceived, true);
                    _ = Task.Delay(1000).ContinueWith(_ => 
                    {
                        try
                        {
                            _module.SendParameterSafePublic(IRCBridgeParameter.MessageReceived, false);
                        }
                        catch
                        {
                            // Ignore errors during delayed parameter updates
                        }
                    });
                }
                catch
                {
                    // Ignore errors if module is stopping
                }
                
                if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    _module.Log($"<{user}> {message}");
                }
            }
        }
        return Task.CompletedTask;
    }

    private Task HandleNickChangeAsync(string prefix, List<string> parameters)
    {
        // User changed nickname - we can track this if needed
        if (parameters.Count > 0 && _module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            var oldNick = ExtractNickFromPrefix(prefix);
            var newNick = parameters[0].TrimStart(':');
            _module.Log($"User {oldNick} changed nickname to {newNick}");
        }
        return Task.CompletedTask;
    }

    private Task HandleModeAsync(string prefix, List<string> parameters)
    {
        // Mode changes - only log if logging is enabled
        if (_module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages) && parameters.Count > 0)
        {
            _module.Log($"Mode change: {string.Join(" ", parameters)}");
        }
        return Task.CompletedTask;
    }

    private Task HandleNoticeAsync(string prefix, List<string> parameters)
    {
        // Handle notices (like NickServ authentication responses)
        if (parameters.Count >= 2)
        {
            var target = parameters[0];
            var notice = string.Join(" ", parameters.Skip(1)).TrimStart(':');

            if ((target.Equals("NickServ", StringComparison.OrdinalIgnoreCase) ||
                 notice.Contains("NickServ", StringComparison.OrdinalIgnoreCase)) &&
                _module.GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
            {
                _module.Log($"NickServ: {notice}");
            }
        }
        return Task.CompletedTask;
    }

    private void ParseISupport(List<string> parameters)
    {
        // Parse ISUPPORT (005) messages to extract server capabilities and limits
        // Format: TARGMAX=NAMES:1,LIST:1,KICK:1,WHOIS:1,PRIVMSG:4,NOTICE:4
        if (parameters.Count < 2) return;

        var isupportText = string.Join(" ", parameters.Skip(1));
        var parts = isupportText.Split(' ');

        foreach (var part in parts)
        {
            if (part.StartsWith("TARGMAX="))
            {
                var targmax = part.Substring(8); // Remove "TARGMAX="
                var limits = targmax.Split(',');
                foreach (var limit in limits)
                {
                    var kvp = limit.Split(':');
                    if (kvp.Length == 2 && int.TryParse(kvp[1], out var maxValue))
                    {
                        _rateLimits[kvp[0].ToUpperInvariant()] = maxValue;
                    }
                }
            }
        }
    }

    public int GetRateLimit(string command)
    {
        // Get rate limit for a command (e.g., PRIVMSG:4 means max 4 targets per message)
        return _rateLimits.TryGetValue(command.ToUpperInvariant(), out var limit) ? limit : int.MaxValue;
    }

    private string ExtractNickFromPrefix(string prefix)
    {
        // IRC prefix format: :nickname!username@hostname
        prefix = prefix.TrimStart(':');
        var exclamationIndex = prefix.IndexOf('!');
        return exclamationIndex > 0 ? prefix.Substring(0, exclamationIndex) : prefix;
    }

    private bool ShouldProcessEvent(string eventType, string identifier)
    {
        lock (_eventLock)
        {
            var key = $"{eventType}:{identifier}";
            var now = DateTime.Now;
            
            if (_lastEventTimes.TryGetValue(key, out var lastTime))
            {
                var cooldown = _module.GetSettingValue<int>(IRCBridgeSetting.MessageCooldown);
                if ((now - lastTime).TotalMilliseconds < cooldown)
                {
                    return false;
                }
            }
            
            _lastEventTimes[key] = now;
            
            // Clean up old entries periodically (keep only last 1000)
            if (_lastEventTimes.Count > 1000)
            {
                var toRemove = _lastEventTimes
                    .Where(kvp => (now - kvp.Value).TotalMinutes > 10)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var keyToRemove in toRemove)
                {
                    _lastEventTimes.Remove(keyToRemove);
                }
            }
            
            return true;
        }
    }
}
