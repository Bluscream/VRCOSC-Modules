// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using IrcDotNet;

namespace Bluscream.Modules.IRCBridge.Utils;

/// <summary>
/// Parses ISUPPORT (005) messages from IRC servers to extract server limits
/// </summary>
public class IRCISupportParser
{
    public int NickLen { get; private set; } = 9;
    public int ChanLen { get; private set; } = 200;
    public int TopicLen { get; private set; } = 390;
    public int RealNameLen { get; private set; } = 512;
    
    /// <summary>
    /// Action to call when a limit is updated (for logging)
    /// </summary>
    public Action<string>? OnLimitUpdated { get; set; }
    
    /// <summary>
    /// Resets all limits to default values
    /// </summary>
    public void ResetToDefaults()
    {
        NickLen = 9;
        ChanLen = 200;
        TopicLen = 390;
        RealNameLen = 512;
    }
    
    /// <summary>
    /// Parses an ISUPPORT (005) message and updates server limits
    /// </summary>
    public void ParseISupport(IrcDotNet.IrcClient.IrcMessage message)
    {
        if (message.Parameters == null)
        {
            return;
        }
        
        // ISUPPORT format: :server 005 nickname KEY=VALUE KEY2=VALUE2 ... :are supported on this server
        // Parse each parameter that looks like KEY=VALUE
        foreach (var param in message.Parameters)
        {
            if (string.IsNullOrEmpty(param) || param.StartsWith(":"))
            {
                continue; // Skip trailing text parameter
            }
            
            var parts = param.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }
            
            var key = parts[0].ToUpperInvariant();
            var value = parts[1];
            
            switch (key)
            {
                case "NICKLEN":
                    if (int.TryParse(value, out var nickLen) && nickLen > 0)
                    {
                        NickLen = nickLen;
                        OnLimitUpdated?.Invoke($"Server NICKLEN: {nickLen}");
                    }
                    break;
                case "CHANNELLEN":
                    if (int.TryParse(value, out var chanLen) && chanLen > 0)
                    {
                        ChanLen = chanLen;
                        OnLimitUpdated?.Invoke($"Server CHANNELLEN: {chanLen}");
                    }
                    break;
                case "TOPICLEN":
                    if (int.TryParse(value, out var topicLen) && topicLen > 0)
                    {
                        TopicLen = topicLen;
                        OnLimitUpdated?.Invoke($"Server TOPICLEN: {topicLen}");
                    }
                    break;
                case "REALNAMELEN":
                    // Some servers might specify real name length limit
                    if (int.TryParse(value, out var realNameLen) && realNameLen > 0)
                    {
                        RealNameLen = realNameLen;
                        OnLimitUpdated?.Invoke($"Server REALNAMELEN: {realNameLen}");
                    }
                    break;
            }
        }
    }
}
