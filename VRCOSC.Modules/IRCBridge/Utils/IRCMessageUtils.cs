// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

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
}
