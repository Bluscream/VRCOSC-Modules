// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace Bluscream;

/// <summary>
/// DateTime utility functions
/// </summary>
public static class DateTimeUtils
{
    public static long ToUnixTimestamp(DateTime dateTime)
        => ((DateTimeOffset)dateTime).ToUnixTimeSeconds();
    
    public static long ToUnixTimestampMs(DateTime dateTime)
        => ((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
    
    public static DateTime FromUnixTimestamp(long timestamp)
        => DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
    
    public static DateTime FromUnixTimestampMs(long timestamp)
        => DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
    
    public static string ToIso8601(DateTime dateTime)
        => dateTime.ToString("o");
    
    public static string ToTimeAgo(DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;
        
        if (timeSpan.TotalSeconds < 60)
            return $"{(int)timeSpan.TotalSeconds}s ago";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes}m ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours}h ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays}d ago";
        if (timeSpan.TotalDays < 30)
            return $"{(int)(timeSpan.TotalDays / 7)}w ago";
        if (timeSpan.TotalDays < 365)
            return $"{(int)(timeSpan.TotalDays / 30)}mo ago";
        return $"{(int)(timeSpan.TotalDays / 365)}y ago";
    }
}
