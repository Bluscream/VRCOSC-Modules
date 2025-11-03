// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Threading.Tasks;

namespace Bluscream;

/// <summary>
/// Task and async utility functions
/// </summary>
public static class TaskUtils
{
    public static async void FireAndForget(Task task, Action<Exception>? onException = null)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            onException?.Invoke(ex);
        }
    }
    
    public static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs, T defaultValue = default!)
    {
        if (await Task.WhenAny(task, Task.Delay(timeoutMs)) == task)
        {
            return await task;
        }
        return defaultValue;
    }
    
    public static async Task<bool> WithTimeout(Task task, int timeoutMs)
    {
        return await Task.WhenAny(task, Task.Delay(timeoutMs)) == task;
    }
    
    public static async Task<T?> RetryAsync<T>(Func<Task<T>> action, int maxRetries = 3, int initialDelayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(initialDelayMs * (int)Math.Pow(2, i));
            }
        }
        return default;
    }
    
    public static async Task RetryAsync(Func<Task> action, int maxRetries = 3, int initialDelayMs = 100)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await action();
                return;
            }
            catch
            {
                if (i == maxRetries - 1) throw;
                await Task.Delay(initialDelayMs * (int)Math.Pow(2, i));
            }
        }
    }

    /// <summary>
    /// Poll a condition until it returns true or timeout
    /// </summary>
    /// <param name="condition">Function that returns true when condition is met</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
    /// <param name="pollIntervalMs">Time between polling attempts in milliseconds</param>
    /// <returns>True if condition met, false if timeout</returns>
    public static async Task<bool> PollUntil(Func<bool> condition, int timeoutMs = 30000, int pollIntervalMs = 500)
    {
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            if (condition()) return true;
            await Task.Delay(pollIntervalMs);
        }
        return false;
    }

    /// <summary>
    /// Poll a condition until it returns a non-null value or timeout
    /// </summary>
    /// <typeparam name="T">Type of value to return</typeparam>
    /// <param name="provider">Function that returns the value or null if not ready</param>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
    /// <param name="pollIntervalMs">Time between polling attempts in milliseconds</param>
    /// <returns>The value if found, or null if timeout</returns>
    public static async Task<T?> PollUntilValue<T>(Func<T?> provider, int timeoutMs = 30000, int pollIntervalMs = 500) where T : class
    {
        var startTime = DateTime.UtcNow;
        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
        {
            var value = provider();
            if (value != null) return value;
            await Task.Delay(pollIntervalMs);
        }
        return null;
    }
}
