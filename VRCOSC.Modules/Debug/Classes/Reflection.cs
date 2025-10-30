// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Bluscream.Modules.Debug;

public static class DebugReflection
{
    private static Type? _appManagerType;
    private static Type? _avatarParameterTabViewType;
    private static MethodInfo? _getInstanceMethod;
    private static object? _appManagerInstance;
    private static object? _avatarParameterTabView;
    
    public static bool Initialize()
    {
        try
        {
            // Find AppManager type
            var vrcoscAppAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "VRCOSC.App");
            
            if (vrcoscAppAssembly == null)
                return false;

            _appManagerType = vrcoscAppAssembly.GetType("VRCOSC.App.Modules.AppManager");
            if (_appManagerType == null)
                return false;

            // Get AppManager singleton instance
            _getInstanceMethod = _appManagerType.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public);
            if (_getInstanceMethod == null)
                return false;

            _appManagerInstance = _getInstanceMethod.Invoke(null, null);
            if (_appManagerInstance == null)
                return false;

            // Find AvatarParameterTabView type
            _avatarParameterTabViewType = vrcoscAppAssembly.GetType("VRCOSC.App.UI.Views.Run.Tabs.AvatarParameterTabView");
            
            return _avatarParameterTabViewType != null;
        }
        catch
        {
            return false;
        }
    }

    public static object? GetAvatarParameterTabView()
    {
        if (_avatarParameterTabView != null)
            return _avatarParameterTabView;

        try
        {
            if (_appManagerInstance == null || _avatarParameterTabViewType == null)
                return null;

            // Try to find the view instance through AppManager's UI/Window hierarchy
            // This is a heuristic approach - we'll search for instantiated AvatarParameterTabView
            var runningInstances = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(t => _avatarParameterTabViewType.IsAssignableFrom(t))
                .SelectMany(t =>
                {
                    try
                    {
                        var instanceField = t.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                            .FirstOrDefault(f => _avatarParameterTabViewType.IsAssignableFrom(f.FieldType));
                        return instanceField != null ? new[] { instanceField.GetValue(null) } : Array.Empty<object>();
                    }
                    catch
                    {
                        return Array.Empty<object>();
                    }
                })
                .Where(i => i != null)
                .ToList();

            if (runningInstances.Any())
            {
                _avatarParameterTabView = runningInstances.First();
                return _avatarParameterTabView;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, object>? GetOutgoingMessages()
    {
        try
        {
            var view = GetAvatarParameterTabView();
            if (view == null || _avatarParameterTabViewType == null)
                return null;

            var outgoingProperty = _avatarParameterTabViewType.GetProperty("OutgoingMessages", BindingFlags.Instance | BindingFlags.Public);
            if (outgoingProperty == null)
                return null;

            var outgoingDict = outgoingProperty.GetValue(view);
            if (outgoingDict == null)
                return null;

            // Convert ObservableDictionary to regular Dictionary
            var dictType = outgoingDict.GetType();
            var toDictMethod = dictType.GetMethod("ToDictionary") 
                ?? dictType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                    ?.GetMethod("GetEnumerator");

            // If it implements IDictionary, we can enumerate it
            if (outgoingDict is System.Collections.IDictionary dict)
            {
                var result = new Dictionary<string, object>();
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (entry.Key is string key && entry.Value != null)
                    {
                        result[key] = entry.Value;
                    }
                }
                return result;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, object>? GetIncomingMessages()
    {
        try
        {
            var view = GetAvatarParameterTabView();
            if (view == null || _avatarParameterTabViewType == null)
                return null;

            var incomingProperty = _avatarParameterTabViewType.GetProperty("IncomingMessages", BindingFlags.Instance | BindingFlags.Public);
            if (incomingProperty == null)
                return null;

            var incomingDict = incomingProperty.GetValue(view);
            if (incomingDict == null)
                return null;

            // Convert ObservableDictionary to regular Dictionary
            if (incomingDict is System.Collections.IDictionary dict)
            {
                var result = new Dictionary<string, object>();
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    if (entry.Key is string key && entry.Value != null)
                    {
                        result[key] = entry.Value;
                    }
                }
                return result;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static (int incoming, int outgoing) GetParameterCounts()
    {
        var incoming = GetIncomingMessages()?.Count ?? 0;
        var outgoing = GetOutgoingMessages()?.Count ?? 0;
        return (incoming, outgoing);
    }
}
