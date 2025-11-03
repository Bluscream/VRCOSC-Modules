// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace Bluscream;

/// <summary>
/// String utility functions
/// </summary>
public static class StringUtils
{
    public static bool IsNullOrWhiteSpace(string? str) => string.IsNullOrWhiteSpace(str);
    
    public static bool IsNullOrEmpty(string? str) => string.IsNullOrEmpty(str);
    
    public static string OrDefault(string? str, string defaultValue) 
        => string.IsNullOrEmpty(str) ? defaultValue : str;
    
    public static string Truncate(string str, int maxLength, string suffix = "...")
    {
        if (str.Length <= maxLength) return str;
        return str.Substring(0, maxLength - suffix.Length) + suffix;
    }
    
    public static int ToIntOrDefault(string? str, int defaultValue = 0)
        => int.TryParse(str, out var result) ? result : defaultValue;
    
    public static float ToFloatOrDefault(string? str, float defaultValue = 0f)
        => float.TryParse(str, out var result) ? result : defaultValue;
    
    public static bool ToBoolOrDefault(string? str, bool defaultValue = false)
        => bool.TryParse(str, out var result) ? result : defaultValue;
    
    public static string RemovePrefix(string str, string prefix)
        => str.StartsWith(prefix) ? str.Substring(prefix.Length) : str;
    
    public static string RemoveSuffix(string str, string suffix)
        => str.EndsWith(suffix) ? str.Substring(0, str.Length - suffix.Length) : str;
}
