// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace Bluscream;

/// <summary>
/// Functional programming utility functions
/// </summary>
public static class FunctionalUtils
{
    public static TResult Pipe<T, TResult>(T value, Func<T, TResult> func)
        => func(value);
    
    public static T Tap<T>(T value, Action<T> action)
    {
        action(value);
        return value;
    }
    
    public static TResult Match<T, TResult>(T? value, Func<T, TResult> some, Func<TResult> none) where T : class
        => value != null ? some(value) : none();
}
