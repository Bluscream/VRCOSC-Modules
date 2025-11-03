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
}
