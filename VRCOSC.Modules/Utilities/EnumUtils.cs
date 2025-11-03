// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Bluscream;

/// <summary>
/// Enum utility functions
/// </summary>
public static class EnumUtils
{
    public static string GetDescription(Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        if (field == null) return value.ToString();
        
        var attribute = field.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
    
    public static T ToEnumOrDefault<T>(string? str, T defaultValue = default!) where T : struct, Enum
        => Enum.TryParse<T>(str, true, out var result) ? result : defaultValue;
    
    public static IEnumerable<T> GetValues<T>() where T : Enum
        => Enum.GetValues(typeof(T)).Cast<T>();
}
