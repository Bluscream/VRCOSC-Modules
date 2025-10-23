// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.
using System.Reflection;

namespace Bluscream.Modules;

/// <summary>
/// Static utility class for interacting with VRCOSC internals via reflection
/// </summary>
public static class Utils
{
    /// <summary>
    /// Send text to VRChat chatbox via VRCOSC's ChatBoxManager
    /// </summary>
    /// <param name="text">Text to display in chatbox</param>
    /// <param name="minimalBackground">Use minimal background style</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool SendChatBox(string text, bool minimalBackground = false)
    {
        try
        {
            // Use reflection to access ChatBoxManager.GetInstance()
            var chatBoxManagerType = Type.GetType("VRCOSC.App.ChatBox.ChatBoxManager, VRCOSC.App");
            if (chatBoxManagerType == null)
            {
                // Fallback to raw OSC
                return SendRawOSC("/chatbox/input", text, true, false);
            }

            var getInstanceMethod = chatBoxManagerType.GetMethod("GetInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (getInstanceMethod == null)
            {
                return SendRawOSC("/chatbox/input", text, true, false);
            }

            var chatBoxManager = getInstanceMethod.Invoke(null, null);
            if (chatBoxManager == null)
            {
                return SendRawOSC("/chatbox/input", text, true, false);
            }

            // Set PulseText property
            var pulseTextProp = chatBoxManagerType.GetProperty("PulseText");
            pulseTextProp?.SetValue(chatBoxManager, text);

            // Set PulseMinimalBackground property
            var pulseMinimalBgProp = chatBoxManagerType.GetProperty("PulseMinimalBackground");
            pulseMinimalBgProp?.SetValue(chatBoxManager, minimalBackground);

            return true;
        }
        catch (Exception)
        {
            // Fallback to raw OSC
            return SendRawOSC("/chatbox/input", text, true, false);
        }
    }

    /// <summary>
    /// Send raw OSC message to VRChat via VRCOSC's OSC client
    /// </summary>
    /// <param name="address">OSC address</param>
    /// <param name="args">OSC arguments</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool SendRawOSC(string address, params object[] args)
    {
        try
        {
            // Use reflection to access AppManager.GetInstance().VRChatOscClient.Send
            var appManagerType = Type.GetType("VRCOSC.App.Modules.AppManager, VRCOSC.App");
            if (appManagerType == null) return false;

            var getInstanceMethod = appManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
            if (getInstanceMethod == null) return false;

            var appManager = getInstanceMethod.Invoke(null, null);
            if (appManager == null) return false;

            var oscClientProp = appManagerType.GetProperty("VRChatOscClient");
            if (oscClientProp == null) return false;

            var oscClient = oscClientProp.GetValue(appManager);
            if (oscClient == null) return false;

            // Call Send method
            var sendMethod = oscClient.GetType().GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);
            if (sendMethod == null) return false;

            // Combine address with args
            var allArgs = new object[args.Length + 1];
            allArgs[0] = address;
            Array.Copy(args, 0, allArgs, 1, args.Length);

            sendMethod.Invoke(oscClient, allArgs);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Stop all VRCOSC modules (same as clicking stop button in UI)
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public static bool StopModules()
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager == null) return false;

            var appManagerType = appManager.GetType();
            
            // Get ModuleManager property
            var moduleManagerProp = appManagerType.GetProperty("ModuleManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (moduleManagerProp == null) return false;

            var moduleManager = moduleManagerProp.GetValue(appManager);
            if (moduleManager == null) return false;

            // Call StopAsync() method on ModuleManager (same as VRCOSC stop button)
            var stopAsyncMethod = moduleManager.GetType().GetMethod("StopAsync", BindingFlags.Public | BindingFlags.Instance);
            if (stopAsyncMethod != null)
            {
                var task = stopAsyncMethod.Invoke(moduleManager, null) as Task;
                task?.Wait(5000); // Wait up to 5 seconds
                return task?.IsCompletedSuccessfully == true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Start all VRCOSC modules (same as clicking play button in UI)
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public static bool StartModules()
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager == null) return false;

            var appManagerType = appManager.GetType();
            
            // Get ModuleManager property
            var moduleManagerProp = appManagerType.GetProperty("ModuleManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (moduleManagerProp == null) return false;

            var moduleManager = moduleManagerProp.GetValue(appManager);
            if (moduleManager == null) return false;

            // Call StartAsync() method on ModuleManager (same as VRCOSC play button)
            var startAsyncMethod = moduleManager.GetType().GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance);
            if (startAsyncMethod != null)
            {
                var task = startAsyncMethod.Invoke(moduleManager, null) as Task;
                task?.Wait(5000); // Wait up to 5 seconds
                return task?.IsCompletedSuccessfully == true;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Force VRCOSC to flush all persistent data to disk
    /// Calls all modules' Serialise() methods to save their state immediately
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public static bool FlushToDisk()
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager == null) return false;

            var appManagerType = appManager.GetType();
            
            // Get Modules property
            var modulesProp = appManagerType.GetProperty("Modules", BindingFlags.Public | BindingFlags.Instance);
            if (modulesProp == null) return false;

            var modules = modulesProp.GetValue(appManager);
            if (modules == null) return false;

            // Iterate through modules and call Serialise on each
            var modulesEnumerable = modules as System.Collections.IEnumerable;
            if (modulesEnumerable == null) return false;

            foreach (var module in modulesEnumerable)
            {
                if (module == null) continue;

                // Call Serialise() method on module
                var serialiseMethod = module.GetType().GetMethod("Serialise", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                serialiseMethod?.Invoke(module, null);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Force VRCOSC to reload all persistent data from disk
    /// Calls moduleSerialisationManager.Deserialise() on all modules
    /// </summary>
    /// <returns>True if successful, false otherwise</returns>
    public static bool LoadFromDisk()
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager == null) return false;

            var appManagerType = appManager.GetType();
            
            // Get Modules property
            var modulesProp = appManagerType.GetProperty("Modules", BindingFlags.Public | BindingFlags.Instance);
            if (modulesProp == null) return false;

            var modules = modulesProp.GetValue(appManager);
            if (modules == null) return false;

            // Iterate through modules and call deserialise on each
            var modulesEnumerable = modules as System.Collections.IEnumerable;
            if (modulesEnumerable == null) return false;

            foreach (var module in modulesEnumerable)
            {
                if (module == null) continue;

                var moduleType = module.GetType();
                
                // Get moduleSerialisationManager field
                var serialisationManagerField = moduleType.GetField("moduleSerialisationManager", BindingFlags.NonPublic | BindingFlags.Instance);
                if (serialisationManagerField == null) continue;

                var serialisationManager = serialisationManagerField.GetValue(module);
                if (serialisationManager == null) continue;

                // Call Deserialise method
                var deserialiseMethod = serialisationManager.GetType().GetMethod("Deserialise", BindingFlags.Public | BindingFlags.Instance);
                if (deserialiseMethod != null)
                {
                    // Deserialise takes two bool parameters: useDefaultPath and filePathOverride
                    deserialiseMethod.Invoke(serialisationManager, new object[] { true, null! });
                }
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Get the AppManager instance
    /// </summary>
    /// <returns>AppManager instance or null</returns>
    public static object? GetAppManager()
    {
        try
        {
            var appManagerType = Type.GetType("VRCOSC.App.Modules.AppManager, VRCOSC.App");
            if (appManagerType == null) return null;

            var getInstanceMethod = appManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
            if (getInstanceMethod == null) return null;

            return getInstanceMethod.Invoke(null, null);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get the ChatBoxManager instance
    /// </summary>
    /// <returns>ChatBoxManager instance or null</returns>
    public static object? GetChatBoxManager()
    {
        try
        {
            var chatBoxManagerType = Type.GetType("VRCOSC.App.ChatBox.ChatBoxManager, VRCOSC.App");
            if (chatBoxManagerType == null) return null;

            var getInstanceMethod = chatBoxManagerType.GetMethod("GetInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (getInstanceMethod == null) return null;

            return getInstanceMethod.Invoke(null, null);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get all VRCX states (states starting with vrcx_)
    /// </summary>
    /// <returns>List of state objects with name, key, and displayName</returns>
    public static List<object>? GetVRCXStates()
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null) return null;

            var chatBoxManagerType = chatBoxManager.GetType();
            
            // Try to get States property or field
            var statesProp = chatBoxManagerType.GetProperty("States", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var statesField = chatBoxManagerType.GetField("States", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            object? statesCollection = statesProp?.GetValue(chatBoxManager) ?? statesField?.GetValue(chatBoxManager);
            if (statesCollection == null) return new List<object>();

            // States is likely a dictionary or collection - iterate and filter
            var result = new List<object>();
            var enumerable = statesCollection as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;

                    var itemType = item.GetType();
                    var lookupProp = itemType.GetProperty("Lookup");
                    var titleProp = itemType.GetProperty("Title");

                    if (lookupProp == null || titleProp == null) continue;

                    var lookup = lookupProp.GetValue(item)?.ToString();

                    // Filter by vrcx_ prefix only
                    if (lookup != null && lookup.StartsWith("vrcx_"))
                    {
                        var titleObj = titleProp.GetValue(item);
                        var titleValueProp = titleObj?.GetType().GetProperty("Value");
                        var displayName = titleValueProp?.GetValue(titleObj)?.ToString() ?? lookup;

                        result.Add(new
                        {
                            name = lookup.Replace("vrcx_", ""),
                            key = lookup,
                            displayName
                        });
                    }
                }
            }

            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get all VRCX events (events starting with vrcx_)
    /// </summary>
    /// <returns>List of event objects with name, key, and displayName</returns>
    public static List<object>? GetVRCXEvents()
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null) return null;

            var chatBoxManagerType = chatBoxManager.GetType();
            
            // Try to get Events property or field
            var eventsProp = chatBoxManagerType.GetProperty("Events", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var eventsField = chatBoxManagerType.GetField("Events", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            object? eventsCollection = eventsProp?.GetValue(chatBoxManager) ?? eventsField?.GetValue(chatBoxManager);
            if (eventsCollection == null) return new List<object>();

            // Events is likely a dictionary or collection - iterate and filter
            var result = new List<object>();
            var enumerable = eventsCollection as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;

                    var itemType = item.GetType();
                    var lookupProp = itemType.GetProperty("Lookup");
                    var titleProp = itemType.GetProperty("Title");

                    if (lookupProp == null || titleProp == null) continue;

                    var lookup = lookupProp.GetValue(item)?.ToString();

                    // Filter by vrcx_ prefix only
                    if (lookup != null && lookup.StartsWith("vrcx_"))
                    {
                        var titleObj = titleProp.GetValue(item);
                        var titleValueProp = titleObj?.GetType().GetProperty("Value");
                        var displayName = titleValueProp?.GetValue(titleObj)?.ToString() ?? lookup;

                        result.Add(new
                        {
                            name = lookup.Replace("vrcx_", ""),
                            key = lookup,
                            displayName
                        });
                    }
                }
            }

            return result;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
