// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bluscream;

/// <summary>
/// Collection utility functions
/// </summary>
public static class CollectionUtils
{
    public static void ForEach<T>(IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source)
            action(item);
    }
    
    public static bool IsNullOrEmpty<T>(IEnumerable<T>? source)
        => source == null || !source.Any();
    
    public static TValue? GetValueOrDefault<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, TValue? defaultValue = default) where TKey : notnull
        => dict.TryGetValue(key, out var value) ? value : defaultValue;
    
    public static void AddOrUpdate<TKey, TValue>(Dictionary<TKey, TValue> dict, TKey key, TValue value) where TKey : notnull
    {
        dict[key] = value;
    }
    
    public static IEnumerable<IEnumerable<T>> Chunk<T>(IEnumerable<T> source, int chunkSize)
    {
        var list = source.ToList();
        for (int i = 0; i < list.Count; i += chunkSize)
        {
            yield return list.Skip(i).Take(chunkSize);
        }
    }
}
