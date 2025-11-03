// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Reflection;

namespace Bluscream;

/// <summary>
/// Type conversion and reflection utility functions
/// </summary>
public static class TypeUtils
{
    public static T? ConvertTo<T>(object? value, T? defaultValue = default)
    {
        if (value == null) return defaultValue;
        
        try
        {
            if (value is T t) return t;
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
    
    public static T? As<T>(object? obj) where T : class
        => obj as T;
    
    public static bool Is<T>(object? obj)
        => obj is T;
    
    public static T? GetPropertyValue<T>(object obj, string propertyName, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName, flags);
            var value = prop?.GetValue(obj);
            return value != null ? (T)value : default;
        }
        catch
        {
            return default;
        }
    }
    
    public static bool SetPropertyValue(object obj, string propertyName, object? value, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        try
        {
            var prop = obj.GetType().GetProperty(propertyName, flags);
            if (prop == null || !prop.CanWrite) return false;
            
            prop.SetValue(obj, value);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public static T? GetFieldValue<T>(object obj, string fieldName, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        try
        {
            var field = obj.GetType().GetField(fieldName, flags);
            var value = field?.GetValue(obj);
            return value != null ? (T)value : default;
        }
        catch
        {
            return default;
        }
    }
    
    public static T? InvokeMethod<T>(object obj, string methodName, object?[]? parameters = null, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
    {
        try
        {
            var method = obj.GetType().GetMethod(methodName, flags);
            if (method == null) return default;
            
            var result = method.Invoke(obj, parameters);
            return result != null ? (T)result : default;
        }
        catch
        {
            return default;
        }
    }
    
    public static bool HasAttribute<T>(Type type) where T : Attribute
        => type.GetCustomAttribute<T>() != null;
    
    public static T? GetAttribute<T>(Type type) where T : Attribute
        => type.GetCustomAttribute<T>();
}
