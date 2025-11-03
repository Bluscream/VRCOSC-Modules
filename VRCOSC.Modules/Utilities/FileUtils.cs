// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Bluscream;

/// <summary>
/// File and performance utility functions
/// </summary>
public static class FileUtils
{
    public static string ToFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
    
    public static TimeSpan MeasureTime(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.Elapsed;
    }
    
    public static async Task<TimeSpan> MeasureTimeAsync(Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();
        return sw.Elapsed;
    }
}
