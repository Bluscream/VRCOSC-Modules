// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using IrcDotNet;

namespace Bluscream.Modules.IRCBridge.Utils;

public static class IRCMessageUtils
{
    /// <summary>
    /// Reconstructs a raw IRC message string from an IrcMessage object
    /// </summary>
    public static string ReconstructRawMessage(IrcDotNet.IrcClient.IrcMessage message)
    {
        var rawMessage = string.Empty;
        
        if (message.Prefix != null)
        {
            rawMessage += $":{message.Prefix} ";
        }
        
        if (message.Command != null)
        {
            rawMessage += message.Command;
        }
        
        if (message.Parameters != null && message.Parameters.Count > 0)
        {
            for (int i = 0; i < message.Parameters.Count; i++)
            {
                var param = message.Parameters[i];
                if (param == null) continue;
                
                if (i == message.Parameters.Count - 1 && param.Contains(' '))
                {
                    rawMessage += $" :{param}";
                }
                else
                {
                    rawMessage += $" {param}";
                }
            }
        }
        
        return rawMessage;
    }
    
    /// <summary>
    /// Sanitizes a message for logging by replacing control characters with readable representations
    /// </summary>
    public static string SanitizeForLogging(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }
        
        // Replace common control characters with readable representations
        return message
            .Replace("\x01", "\\x01")  // SOH (Start of Heading) - used in CTCP/ACTION
            .Replace("\x00", "\\x00")   // NUL
            .Replace("\r", "\\r")       // Carriage return
            .Replace("\n", "\\n");      // Line feed
    }
    
    /// <summary>
    /// Categorizes an IRC message type for logging purposes
    /// </summary>
    public static IRCMessageCategory CategorizeMessage(string rawMessage, IrcDotNet.IrcClient.IrcMessage? message = null)
    {
        if (string.IsNullOrEmpty(rawMessage))
        {
            return IRCMessageCategory.System;
        }
        
        var upperMessage = rawMessage.ToUpperInvariant();
        
        // Parse the raw message to extract the command
        // Format: [:prefix] COMMAND [params...]
        var parts = rawMessage.TrimStart().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return IRCMessageCategory.System;
        }
        
        // First part might be prefix (starts with :), second part is command
        string command;
        if (parts[0].StartsWith(":"))
        {
            // Has prefix, command is second part
            if (parts.Length < 2)
            {
                return IRCMessageCategory.System;
            }
            command = parts[1];
        }
        else
        {
            // No prefix, command is first part
            command = parts[0];
        }
        
        // Check for numeric responses (001-999) - these are system messages
        if (int.TryParse(command, out int numericCode))
        {
            if (numericCode >= 1 && numericCode <= 999)
            {
                return IRCMessageCategory.System;
            }
        }
        
        // Check for chat messages (PRIVMSG, NOTICE from users)
        if (command == "PRIVMSG" || command == "NOTICE")
        {
            // Skip CTCP ACTION messages with CSV format (welcome messages)
            if (rawMessage.Contains("PRIVMSG") && rawMessage.Contains("ACTION") && rawMessage.Split(';').Length > 5)
            {
                return IRCMessageCategory.System; // Skip these
            }
            return IRCMessageCategory.Chat;
        }
        
        // Check for IRC events (JOIN, PART, QUIT, NICK, MODE, etc.)
        var eventCommands = new[] { "JOIN", "PART", "QUIT", "NICK", "MODE", "TOPIC", "KICK", "INVITE", "WHOIS", "WHOWAS" };
        foreach (var cmd in eventCommands)
        {
            if (command == cmd)
            {
                return IRCMessageCategory.Event;
            }
        }
        
        // Default to system for everything else (PING, PONG, etc.)
        return IRCMessageCategory.System;
    }
}

/// <summary>
/// Categories for IRC messages used in logging
/// </summary>
public enum IRCMessageCategory
{
    Chat,      // User messages (PRIVMSG, NOTICE)
    System,    // Server responses (numeric codes, PING/PONG, etc.)
    Event      // IRC events (JOIN, NICK, MODE, etc.)
}
