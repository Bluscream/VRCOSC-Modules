// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace Bluscream;

/// <summary>
/// Validation utility functions
/// </summary>
public static class ValidationUtils
{
    public static T ThrowIfNull<T>(T? value, string? paramName = null) where T : class
        => value ?? throw new ArgumentNullException(paramName ?? "value");
    
    public static string ThrowIfNullOrEmpty(string? value, string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", paramName ?? "value");
        return value;
    }
    
    public static T? IfNotNull<T>(T? value, Action<T> action) where T : class
    {
        if (value != null) action(value);
        return value;
    }
    
    public static TResult? IfNotNull<T, TResult>(T? value, Func<T, TResult> func) where T : class
        => value != null ? func(value) : default;
}
