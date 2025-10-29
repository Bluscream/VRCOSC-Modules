// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Bluscream.Modules;

/// <summary>
/// Helper class for VRChat user ID template expansion
/// </summary>
public static class VRChatUserIdHelper
{
    private static readonly Regex UserIdPattern = new Regex(@"usr_[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);
    
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
    /// Example: "{userId}_currentShowMaxNumberOfAvatarsEnabled" + "usr_xxx" -> "usr_xxx_currentShowMaxNumberOfAvatarsEnabled"
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
}
