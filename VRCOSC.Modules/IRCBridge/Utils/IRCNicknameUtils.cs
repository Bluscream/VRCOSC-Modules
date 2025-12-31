// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

namespace Bluscream.Modules.IRCBridge.Utils;

/// <summary>
/// Utility functions for IRC nickname handling and conflict resolution
/// </summary>
public static class IRCNicknameUtils
{
    /// <summary>
    /// Generates an alternative nickname when the original is already in use
    /// </summary>
    /// <param name="baseNickname">The original nickname that caused the conflict</param>
    /// <param name="conflictCount">The number of conflicts encountered (1-based)</param>
    /// <param name="maxLength">Maximum nickname length (from server NICKLEN)</param>
    /// <returns>An alternative nickname that fits within the length limit</returns>
    public static string GenerateAlternativeNickname(string baseNickname, int conflictCount, int maxLength)
    {
        if (string.IsNullOrEmpty(baseNickname))
        {
            return "User" + conflictCount;
        }
        
        string newNick;
        
        if (baseNickname.Length < maxLength)
        {
            // If we have room, append number or underscore
            if (conflictCount == 1)
            {
                // First attempt: try underscore
                newNick = $"{baseNickname}_";
                if (newNick.Length > maxLength)
                {
                    newNick = baseNickname.Substring(0, maxLength - 1) + conflictCount.ToString();
                }
            }
            else
            {
                // Subsequent attempts: append number
                newNick = $"{baseNickname}{conflictCount}";
                if (newNick.Length > maxLength)
                {
                    // Truncate base and append number
                    var availableLength = maxLength - conflictCount.ToString().Length;
                    newNick = baseNickname.Substring(0, availableLength) + conflictCount.ToString();
                }
            }
        }
        else
        {
            // Nickname is already at max length, replace last character(s) with number
            var numberStr = conflictCount.ToString();
            if (numberStr.Length >= maxLength)
            {
                // If number is too long, just use last digit
                numberStr = conflictCount.ToString().Substring(conflictCount.ToString().Length - 1);
            }
            var availableLength = maxLength - numberStr.Length;
            newNick = baseNickname.Substring(0, availableLength) + numberStr;
        }
        
        // Safety check: ensure we don't exceed max length
        if (newNick.Length > maxLength)
        {
            newNick = newNick.Substring(0, maxLength);
        }
        
        return newNick;
    }
}
