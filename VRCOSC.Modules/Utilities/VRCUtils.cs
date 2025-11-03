// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Text.RegularExpressions;

namespace Bluscream;

/// <summary>
/// VRChat-specific utility functions
/// Consolidated from VRChatSettings module
/// </summary>
public static class VRCUtils
{
    private static readonly Regex UserIdPattern = new(@"usr_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
    
    #region User ID Utilities

    /// <summary>
    /// Validate a VRChat user ID format
    /// </summary>
    public static bool IsValidUserId(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return false;
        
        return UserIdPattern.IsMatch(userId);
    }

    /// <summary>
    /// Expand a key template with the provided user ID
    /// Example: "{userId}_setting" + "usr_xxx" -> "usr_xxx_setting"
    /// </summary>
    public static string? ExpandKeyTemplate(string keyTemplate, string? userId)
    {
        if (!keyTemplate.Contains("{userId}"))
            return keyTemplate;

        if (string.IsNullOrEmpty(userId))
            return null;

        return keyTemplate.Replace("{userId}", userId);
    }

    /// <summary>
    /// Check if a key is a user-specific template
    /// </summary>
    public static bool IsUserTemplate(string key)
    {
        return key.Contains("{userId}");
    }

    #endregion

    #region VRChat Hash Utilities

    /// <summary>
    /// Generate VRChat-style hash for registry keys
    /// Uses the same algorithm VRChat uses internally
    /// </summary>
    public static string AddHashToKeyName(string key)
    {
        uint hash = 5381;
        foreach (var c in key)
            hash = (hash * 33) ^ c;
        return key + "_h" + hash;
    }

    /// <summary>
    /// Extract the original key name from a hashed key
    /// </summary>
    public static string? RemoveHashFromKeyName(string hashedKey)
    {
        var index = hashedKey.LastIndexOf("_h", StringComparison.Ordinal);
        if (index <= 0) return null;

        return hashedKey.Substring(0, index);
    }

    #endregion

    #region Avatar/World ID Utilities

    /// <summary>
    /// Check if string looks like a VRChat avatar ID
    /// </summary>
    public static bool IsVRChatAvatarId(string? str)
        => !string.IsNullOrEmpty(str) && str.StartsWith("avtr_") && str.Length == 41;
    
    /// <summary>
    /// Check if string looks like a VRChat world ID
    /// </summary>
    public static bool IsVRChatWorldId(string? str)
        => !string.IsNullOrEmpty(str) && str.StartsWith("wrld_") && str.Length == 41;

    /// <summary>
    /// Get VRChat avatar ID from world instance
    /// Example: wrld_xxx:12345~hidden(usr_xxx)~region(eu) -> wrld_xxx:12345
    /// </summary>
    public static string ExtractWorldId(string worldInstance)
    {
        if (string.IsNullOrEmpty(worldInstance)) return worldInstance;
        
        var tilde = worldInstance.IndexOf('~');
        return tilde > 0 ? worldInstance.Substring(0, tilde) : worldInstance;
    }

    #endregion
}
