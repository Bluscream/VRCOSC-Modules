// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Bluscream;

/// <summary>
/// Reflection utilities for accessing VRCOSC internal APIs
/// Uses caching for improved performance when calling methods multiple times
/// </summary>
public static class ReflectionUtils
{
    #region Reflection Caches
    
    // Type caches
    private static Type? _appManagerType;
    private static Type? _moduleManagerType;
    private static Type? _chatBoxManagerType;

    // Method caches
    private static MethodInfo? _appManagerGetInstanceMethod;
    private static MethodInfo? _moduleManagerGetInstanceMethod;
    private static MethodInfo? _chatBoxManagerGetInstanceMethod;
    private static MethodInfo? _moduleManagerStopAsyncMethod;
    private static MethodInfo? _moduleManagerStartAsyncMethod;
    private static MethodInfo? _oscClientSendMethod;
    private static MethodInfo? _moduleSendParameterMethod;

    // Property caches
    private static PropertyInfo? _moduleManagerModulesProp;
    private static PropertyInfo? _appManagerOscClientProp;
    private static PropertyInfo? _chatBoxPulseTextProp;
    private static PropertyInfo? _chatBoxPulseMinimalBgProp;

    // Field caches
    private static FieldInfo? _moduleParametersField;
    
    #endregion

    #region AppManager Access

    /// <summary>
    /// Get the AppManager singleton instance (cached)
    /// Returns (instance, error message)
    /// </summary>
    private static (object? instance, string? error) GetAppManagerWithError()
    {
        try
        {
            _appManagerType ??= Type.GetType("VRCOSC.App.AppManager, VRCOSC.App");
            if (_appManagerType == null) return (null, "AppManager type not found");

            _appManagerGetInstanceMethod ??= _appManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (_appManagerGetInstanceMethod == null) return (null, "AppManager.GetInstance method not found");

            var instance = _appManagerGetInstanceMethod.Invoke(null, null);
            if (instance == null) return (null, "AppManager.GetInstance() returned null");

            return (instance, null);
        }
        catch (Exception ex)
        {
            return (null, $"Exception getting AppManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the AppManager singleton instance (cached)
    /// </summary>
    public static object? GetAppManager()
    {
        var (instance, _) = GetAppManagerWithError();
        return instance;
    }

    /// <summary>
    /// Get the ModuleManager singleton instance (cached)
    /// Returns (instance, error message)
    /// </summary>
    private static (object? instance, string? error) GetModuleManagerWithError()
    {
        try
        {
            _moduleManagerType ??= Type.GetType("VRCOSC.App.Modules.ModuleManager, VRCOSC.App");
            if (_moduleManagerType == null) return (null, "ModuleManager type not found");

            _moduleManagerGetInstanceMethod ??= _moduleManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (_moduleManagerGetInstanceMethod == null) return (null, "ModuleManager.GetInstance method not found");

            var instance = _moduleManagerGetInstanceMethod.Invoke(null, null);
            if (instance == null) return (null, "ModuleManager.GetInstance() returned null");

            return (instance, null);
        }
        catch (Exception ex)
        {
            return (null, $"Exception getting ModuleManager: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the ModuleManager singleton instance (cached)
    /// </summary>
    public static object? GetModuleManager()
    {
        var (instance, _) = GetModuleManagerWithError();
        return instance;
    }

    /// <summary>
    /// Get ProfileManager instance using reflection (cached)
    /// </summary>
    private static object? GetProfileManager()
    {
        try
        {
            var profileManagerType = Type.GetType("VRCOSC.App.Profiles.ProfileManager, VRCOSC.App");
            if (profileManagerType == null) return null;

            var getInstanceMethod = profileManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (getInstanceMethod == null) return null;

            return getInstanceMethod.Invoke(null, null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the current active profile ID (GUID) from ProfileManager (cached)
    /// </summary>
    public static string? GetCurrentProfileId()
    {
        try
        {
            var profileManager = GetProfileManager();
            if (profileManager == null) return null;

            // Get ActiveProfile property (Observable<Profile>)
            var activeProfileProp = profileManager.GetType().GetProperty("ActiveProfile", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (activeProfileProp == null) return null;

            var activeProfileObservable = activeProfileProp.GetValue(profileManager);
            if (activeProfileObservable == null) return null;

            // Get Value property from Observable<Profile>
            var valueProp = activeProfileObservable.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (valueProp == null) return null;

            var profile = valueProp.GetValue(activeProfileObservable);
            if (profile == null) return null;

            // Get ID from profile
            var idProp = profile.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (idProp == null) return null;

            var id = idProp.GetValue(profile);
            return id?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the current profile's modules directory path
    /// Returns: %AppData%/VRCOSC/profiles/{profile-id}/modules
    /// </summary>
    public static string? GetCurrentProfileModulesPath()
    {
        try
        {
            var profileId = GetCurrentProfileId();
            if (profileId == null) return null;

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VRCOSC",
                "profiles",
                profileId,
                "modules"
            );

            return Directory.Exists(appDataPath) ? appDataPath : null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region ChatBox Operations

    /// <summary>
    /// Get the ChatBoxManager singleton instance (cached)
    /// </summary>
    public static object? GetChatBoxManager()
    {
        try
        {
            _chatBoxManagerType ??= Type.GetType("VRCOSC.App.ChatBox.ChatBoxManager, VRCOSC.App");
            if (_chatBoxManagerType == null) return null;

            _chatBoxManagerGetInstanceMethod ??= _chatBoxManagerType.GetMethod("GetInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (_chatBoxManagerGetInstanceMethod == null) return null;

            return _chatBoxManagerGetInstanceMethod.Invoke(null, null);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Send text to VRChat chatbox via VRCOSC's ChatBoxManager (cached)
    /// </summary>
    /// <param name="text">Text to display in chatbox</param>
    /// <param name="minimalBackground">Use minimal background style</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool SendChatBox(string text, bool minimalBackground = false)
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null)
            {
                return SendRawOSC("/chatbox/input", text, true, false);
            }

            var chatBoxManagerType = chatBoxManager.GetType();

            // Cache properties
            _chatBoxPulseTextProp ??= chatBoxManagerType.GetProperty("PulseText");
            _chatBoxPulseMinimalBgProp ??= chatBoxManagerType.GetProperty("PulseMinimalBackground");

            // Set values
            _chatBoxPulseTextProp?.SetValue(chatBoxManager, text);
            _chatBoxPulseMinimalBgProp?.SetValue(chatBoxManager, minimalBackground);

            return true;
        }
        catch
        {
            return SendRawOSC("/chatbox/input", text, true, false);
        }
    }

    #endregion

    #region OSC Operations

    /// <summary>
    /// Send raw OSC message to VRChat via VRCOSC's OSC client (cached)
    /// </summary>
    /// <param name="address">OSC address</param>
    /// <param name="args">OSC arguments</param>
    /// <returns>True if successful, false otherwise</returns>
    public static bool SendRawOSC(string address, params object[] args)
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager == null) return false;

            // Cache properties and methods
            _appManagerOscClientProp ??= appManager.GetType().GetProperty("VRChatOscClient");
            if (_appManagerOscClientProp == null) return false;

            var oscClient = _appManagerOscClientProp.GetValue(appManager);
            if (oscClient == null) return false;

            _oscClientSendMethod ??= oscClient.GetType().GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);
            if (_oscClientSendMethod == null) return false;

            var allArgs = new object[args.Length + 1];
            allArgs[0] = address;
            Array.Copy(args, 0, allArgs, 1, args.Length);

            _oscClientSendMethod.Invoke(oscClient, allArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get AppManager state
    /// Returns: "Waiting", "Starting", "Started", "Stopping", "Stopped", or null if failed
    /// </summary>
    public static string? GetAppManagerState()
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager == null) return null;

            var stateProp = appManager.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateProp == null) return null;

            var stateObservable = stateProp.GetValue(appManager);
            if (stateObservable == null) return null;

            var valueProp = stateObservable.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProp == null) return null;

            var state = valueProp.GetValue(stateObservable);
            return state?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Wait for AppManager to reach "Started" state
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait in milliseconds</param>
    /// <returns>True if Started state reached, false if timeout or error</returns>
    public static async System.Threading.Tasks.Task<bool> WaitForAppManagerStarted(int timeoutMs = 30000)
    {
        return await TaskUtils.PollUntil(() => GetAppManagerState() == "Started", timeoutMs, pollIntervalMs: 500);
    }

    /// <summary>
    /// Request AppManager to start (equivalent to clicking Play button)
    /// Waits for VRChat to be detected before starting
    /// NOTE: For auto-start on load, use ForceAppManagerStart() instead to skip VRChat detection
    /// </summary>
    /// <returns>Error message if failed, null if successful</returns>
    public static string? RequestAppManagerStart()
    {
        try
        {
            var (appManager, error) = GetAppManagerWithError();
            if (appManager == null) return error ?? "Failed to get AppManager instance";

            // Check current state - don't start if already starting/started
            var currentState = GetAppManagerState();
            if (currentState == "Starting" || currentState == "Started" || currentState == "Waiting")
            {
                return $"AppManager is already {currentState}";
            }

            // Get RequestStart method
            var requestStartMethod = appManager.GetType().GetMethod("RequestStart", BindingFlags.Public | BindingFlags.Instance);
            if (requestStartMethod == null) return "RequestStart method not found on AppManager";

            // Invoke RequestStart (returns Task)
            var task = requestStartMethod.Invoke(appManager, null) as Task;
            if (task == null) return "RequestStart invocation returned null";

            // Don't wait for completion - let it run async
            return null;
        }
        catch (Exception ex)
        {
            return $"Exception in RequestAppManagerStart: {ex.GetType().Name} - {ex.Message}";
        }
    }

    /// <summary>
    /// Force AppManager to start immediately (equivalent to clicking "Force Start" button)
    /// Skips VRChat detection and starts with loopback
    /// </summary>
    /// <returns>Error message if failed, null if successful</returns>
    public static string? ForceAppManagerStart()
    {
        try
        {
            var (appManager, error) = GetAppManagerWithError();
            if (appManager == null) return error ?? "Failed to get AppManager instance";

            // Check current state - don't start if already starting/started
            var currentState = GetAppManagerState();
            if (currentState == "Starting" || currentState == "Started")
            {
                return $"AppManager is already {currentState}";
            }

            // FIX: Initialize the CancellationTokenSource that ForceStart needs
            // ForceStart() calls CancelStartRequest() which tries to cancel this token
            var tokenSourceField = appManager.GetType().GetField("requestStartCancellationSource", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (tokenSourceField != null)
            {
                var currentTokenSource = tokenSourceField.GetValue(appManager);
                if (currentTokenSource == null)
                {
                    // Initialize with a new CancellationTokenSource
                    var newTokenSource = new CancellationTokenSource();
                    tokenSourceField.SetValue(appManager, newTokenSource);
                }
            }

            // Get ForceStart method
            var forceStartMethod = appManager.GetType().GetMethod("ForceStart", BindingFlags.Public | BindingFlags.Instance);
            if (forceStartMethod == null) return "ForceStart method not found on AppManager";

            // Get Application.Current.Dispatcher to invoke on UI thread
            var applicationType = Type.GetType("System.Windows.Application, PresentationFramework");
            if (applicationType == null) return "Could not get Application type";

            var currentProperty = applicationType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            if (currentProperty == null) return "Could not get Application.Current property";

            var application = currentProperty.GetValue(null);
            if (application == null) return "Application.Current is null";

            var dispatcherProperty = applicationType.GetProperty("Dispatcher", BindingFlags.Public | BindingFlags.Instance);
            if (dispatcherProperty == null) return "Could not get Dispatcher property";

            var dispatcher = dispatcherProperty.GetValue(application);
            if (dispatcher == null) return "Dispatcher is null";

            // Invoke ForceStart on UI thread
            var dispatcherType = dispatcher.GetType();
            var invokeMethod = dispatcherType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance, null,
                new[] { typeof(Action) }, null);
            if (invokeMethod == null) return "Could not get Dispatcher.Invoke method";

            // Wrap ForceStart call in Action and invoke on UI thread
            Action forceStartAction = () =>
            {
                try
                {
                    var task = forceStartMethod.Invoke(appManager, null) as Task;
                    if (task != null)
                    {
                        // Handle any exceptions from the task
                        task.ContinueWith(t =>
                        {
                            if (t.IsFaulted && t.Exception != null)
                            {
                                var baseException = t.Exception.GetBaseException();
                                System.Diagnostics.Debug.WriteLine($"ForceStart task failed: {baseException.Message}");
                            }
                        }, TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ForceStart invocation exception: {ex.Message}");
                    throw;
                }
            };

            invokeMethod.Invoke(dispatcher, new object[] { forceStartAction });

            return null;
        }
        catch (Exception ex)
        {
            return $"Exception in ForceAppManagerStart: {ex.GetType().Name} - {ex.Message}";
        }
    }

    /// <summary>
    /// Request AppManager to start and wait for it to complete
    /// Waits for VRChat detection before starting
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait</param>
    /// <returns>True if started successfully, false otherwise</returns>
    public static async System.Threading.Tasks.Task<bool> RequestAppManagerStartAndWait(int timeoutMs = 30000)
    {
        var error = RequestAppManagerStart();
        if (error != null)
        {
            // If already started/starting, that's okay
            if (error.Contains("already")) return true;
            return false;
        }

        // Wait for "Started" state
        return await WaitForAppManagerStarted(timeoutMs);
    }

    /// <summary>
    /// Force AppManager to start immediately and wait for it to complete
    /// Skips VRChat detection and starts with loopback
    /// </summary>
    /// <param name="timeoutMs">Maximum time to wait</param>
    /// <returns>True if started successfully, false otherwise</returns>
    public static async System.Threading.Tasks.Task<bool> ForceAppManagerStartAndWait(int timeoutMs = 30000)
    {
        var error = ForceAppManagerStart();
        if (error != null)
        {
            // If already started/starting, that's okay
            if (error.Contains("already")) return true;
            return false;
        }

        // Wait for "Started" state
        return await WaitForAppManagerStarted(timeoutMs);
    }

    #endregion

    #region OSC Parameter Access

    /// <summary>
    /// Get the parameter cache from AppManager (ConcurrentDictionary of ParameterDefinition to VRChatParameter)
    /// </summary>
    public static object? GetParameterCache()
    {
        try
        {
            var (appManager, _) = GetAppManagerWithError();
            if (appManager == null) return null;

            // parameterCache is a property, not a field
            var parameterCacheProp = appManager.GetType().GetProperty("parameterCache", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            return parameterCacheProp?.GetValue(appManager);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get Debug module instance if it's loaded and running
    /// </summary>
    public static object? GetDebugModule()
    {
        try
        {
            var modules = GetModules();
            if (modules == null) return null;

            foreach (var module in modules)
            {
                if (module == null) continue;
                
                var moduleType = module.GetType();
                if (moduleType.Name == "DebugModule" || moduleType.FullName?.Contains("Debug.DebugModule") == true)
                {
                    return module;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all OSC parameters from Debug module's trackers (if available)
    /// Returns (incoming, outgoing) dictionaries or nulls
    /// </summary>
    public static (Dictionary<string, object>? Incoming, Dictionary<string, object>? Outgoing)? GetDebugModuleParameters()
    {
        try
        {
            var debugModule = GetDebugModule();
            if (debugModule == null) return null;

            var debugType = debugModule.GetType();
            
            // Get GetAllIncomingParameters method
            var getIncomingMethod = debugType.GetMethod("GetAllIncomingParameters", BindingFlags.Public | BindingFlags.Instance);
            var getOutgoingMethod = debugType.GetMethod("GetAllOutgoingParameters", BindingFlags.Public | BindingFlags.Instance);

            if (getIncomingMethod == null || getOutgoingMethod == null) return null;

            var incomingDict = getIncomingMethod.Invoke(debugModule, null);
            var outgoingDict = getOutgoingMethod.Invoke(debugModule, null);

            if (incomingDict == null && outgoingDict == null) return null;

            // Convert ParameterData dictionaries to simple dictionaries
            var incoming = ConvertParameterDataDict(incomingDict);
            var outgoing = ConvertParameterDataDict(outgoingDict);

            return (incoming, outgoing);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, object>? ConvertParameterDataDict(object? paramDataDict)
    {
        if (paramDataDict == null) return null;

        try
        {
            var result = new Dictionary<string, object>();
            var dictType = paramDataDict.GetType();
            
            foreach (var kvp in (System.Collections.IDictionary)paramDataDict)
            {
                var key = kvp.GetType().GetProperty("Key")?.GetValue(kvp) as string;
                var value = kvp.GetType().GetProperty("Value")?.GetValue(kvp);

                if (key == null || value == null) continue;

                // Extract ParameterData properties
                var paramType = value.GetType();
                var pathProp = paramType.GetProperty("Path");
                var typeProp = paramType.GetProperty("Type");
                var valueProp = paramType.GetProperty("Value");

                var paramData = new
                {
                    path = pathProp?.GetValue(value) as string,
                    type = typeProp?.GetValue(value) as string,
                    value = valueProp?.GetValue(value)
                };

                result[key] = paramData;
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all OSC parameters from the parameter cache
    /// Returns List of (name, value, type) tuples
    /// </summary>
    public static List<(string Name, object? Value, string Type)>? GetAllOscParameters()
    {
        try
        {
            var cache = GetParameterCache();
            if (cache == null) return null;

            // parameterCache is ConcurrentDictionary<ParameterDefinition, VRChatParameter>
            var results = new List<(string, object?, string)>();
            var dictType = cache.GetType();
            
            // Get Values property to iterate VRChatParameter values
            var valuesProperty = dictType.GetProperty("Values");
            if (valuesProperty == null) return null;

            var values = valuesProperty.GetValue(cache) as System.Collections.IEnumerable;
            if (values == null) return null;

            foreach (var param in values)
            {
                if (param == null) continue;

                var paramType = param.GetType();
                var nameProperty = paramType.GetProperty("Name");
                var valueProperty = paramType.GetProperty("Value");
                var typeProperty = paramType.GetProperty("Type");

                var name = nameProperty?.GetValue(param) as string;
                var value = valueProperty?.GetValue(param);
                var paramTypeValue = typeProperty?.GetValue(param);

                if (name == null) continue;

                // Get type from VRChatParameter.Type property, or fallback to value type
                string typeStr;
                if (paramTypeValue != null)
                {
                    // VRChatParameter.Type is an enum (ParameterType)
                    typeStr = paramTypeValue.ToString()?.ToLowerInvariant() ?? "unknown";
                }
                else
                {
                    typeStr = value switch
                    {
                        bool => "bool",
                        int => "int",
                        float => "float",
                        string => "string",
                        _ => "unknown"
                    };
                }

                results.Add((name, value, typeStr));
            }

            return results;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get a specific OSC parameter value by name
    /// Returns (value, type) or null if not found
    /// </summary>
    public static (object? Value, string Type)? GetOscParameter(string parameterName)
    {
        try
        {
            var cache = GetParameterCache();
            if (cache == null) return null;

            // Iterate through all values since the dictionary is keyed by ParameterDefinition, not string
            var dictType = cache.GetType();
            var valuesProperty = dictType.GetProperty("Values");
            if (valuesProperty == null) return null;

            var values = valuesProperty.GetValue(cache) as System.Collections.IEnumerable;
            if (values == null) return null;

            foreach (var param in values)
            {
                if (param == null) continue;

                var paramType = param.GetType();
                var nameProperty = paramType.GetProperty("Name");
                var name = nameProperty?.GetValue(param) as string;

                if (name == parameterName)
                {
                    var valueProperty = paramType.GetProperty("Value");
                    var typeProperty = paramType.GetProperty("Type");

                    var value = valueProperty?.GetValue(param);
                    var paramTypeValue = typeProperty?.GetValue(param);

                    // Get type from VRChatParameter.Type property, or fallback to value type
                    string typeStr;
                    if (paramTypeValue != null)
                    {
                        typeStr = paramTypeValue.ToString()?.ToLowerInvariant() ?? "unknown";
                    }
                    else
                    {
                        typeStr = value switch
                        {
                            bool => "bool",
                            int => "int",
                            float => "float",
                            string => "string",
                            _ => "unknown"
                        };
                    }

                    return (value, typeStr);
                }
            }

            return null; // Parameter not found
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Send an OSC parameter via AppManager's VRChatOscClient
    /// </summary>
    public static bool SendOscParameter(string parameterName, object value)
    {
        try
        {
            var (appManager, _) = GetAppManagerWithError();
            if (appManager == null) return false;

            // Get VRChatOscClient property
            _appManagerOscClientProp ??= appManager.GetType().GetProperty("VRChatOscClient");
            if (_appManagerOscClientProp == null) return false;

            var oscClient = _appManagerOscClientProp.GetValue(appManager);
            if (oscClient == null) return false;

            // Get Send method
            var sendMethod = oscClient.GetType().GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);
            if (sendMethod == null) return false;

            // Build full address
            var address = parameterName.StartsWith("/avatar/parameters/") 
                ? parameterName 
                : $"/avatar/parameters/{parameterName}";

            sendMethod.Invoke(oscClient, new object[] { address, value });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get current avatar config from AppManager
    /// Returns (avatarId, avatarName) or null if no avatar loaded
    /// </summary>
    public static (string? Id, string? Name)? GetCurrentAvatarInfo()
    {
        try
        {
            var (appManager, _) = GetAppManagerWithError();
            if (appManager == null) return null;

            // Get currentAvatarConfig property (not field)
            var avatarConfigProp = appManager.GetType().GetProperty("currentAvatarConfig", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (avatarConfigProp == null) return null;

            var avatarConfig = avatarConfigProp.GetValue(appManager);
            if (avatarConfig == null) return (null, null); // No avatar loaded

            // Get Id and Name properties
            var idProperty = avatarConfig.GetType().GetProperty("Id");
            var nameProperty = avatarConfig.GetType().GetProperty("Name");

            var id = idProperty?.GetValue(avatarConfig) as string;
            var name = nameProperty?.GetValue(avatarConfig) as string;

            return (id, name);
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Module Control

    /// <summary>
    /// Stop all VRCOSC modules (same as clicking stop button) (cached)
    /// </summary>
    public static bool StopModules()
    {
        try
        {
            var moduleManager = GetModuleManager();
            if (moduleManager == null) return false;

            // Cache method
            _moduleManagerStopAsyncMethod ??= moduleManager.GetType().GetMethod("StopAsync", BindingFlags.Public | BindingFlags.Instance);
            if (_moduleManagerStopAsyncMethod != null)
            {
                var task = _moduleManagerStopAsyncMethod.Invoke(moduleManager, null) as Task;
                task?.Wait(5000);
                return task?.IsCompletedSuccessfully == true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Start all VRCOSC modules (same as clicking play button) (cached)
    /// Returns error message if failed, null if successful
    /// </summary>
    public static string? StartModules()
    {
        try
        {
            var (moduleManager, mmError) = GetModuleManagerWithError();
            if (moduleManager == null) return mmError ?? "Failed to get ModuleManager instance";

            // Cache method
            _moduleManagerStartAsyncMethod ??= moduleManager.GetType().GetMethod("StartAsync", BindingFlags.Public | BindingFlags.Instance);
            if (_moduleManagerStartAsyncMethod == null) return "StartAsync method not found on ModuleManager";

            var task = _moduleManagerStartAsyncMethod.Invoke(moduleManager, null) as Task;
            if (task == null) return "StartAsync invocation returned null (not a Task)";

            // Wait for completion with timeout
            if (!task.Wait(10000)) return "StartAsync timed out after 10 seconds";

            // Check if task completed successfully
            if (task.IsFaulted)
            {
                var exception = task.Exception?.GetBaseException();
                return $"StartAsync faulted: {exception?.GetType().Name} - {exception?.Message ?? "Unknown error"}";
            }

            if (task.IsCanceled) return "StartAsync was cancelled";

            return task.IsCompletedSuccessfully ? null : "StartAsync completed but not successfully";
        }
        catch (Exception ex)
        {
            return $"Exception in StartModules: {ex.GetType().Name} - {ex.Message}";
        }
    }

    /// <summary>
    /// Start all VRCOSC modules - returns true if successful, false otherwise
    /// Use StartModules() for detailed error message
    /// </summary>
    public static bool TryStartModules() => StartModules() == null;

    /// <summary>
    /// Get all loaded modules (cached)
    /// </summary>
    public static IEnumerable? GetModules()
    {
        try
        {
            var moduleManager = GetModuleManager();
            if (moduleManager == null) return null;

            // Cache property - Modules is an ObservableDictionary<ModulePackage, List<Module>>
            _moduleManagerModulesProp ??= moduleManager.GetType().GetProperty("Modules", BindingFlags.Public | BindingFlags.Instance);
            return _moduleManagerModulesProp?.GetValue(moduleManager) as IEnumerable;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Persistence Operations

    /// <summary>
    /// Force VRCOSC to save all module data to disk
    /// </summary>
    public static bool FlushToDisk()
    {
        try
        {
            var modules = GetModules();
            if (modules == null) return false;

            foreach (var module in modules)
            {
                if (module == null) continue;

                var serialiseMethod = module.GetType().GetMethod("Serialise", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                serialiseMethod?.Invoke(module, null);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Force VRCOSC to reload all module data from disk
    /// </summary>
    public static bool LoadFromDisk()
    {
        try
        {
            var modules = GetModules();
            if (modules == null) return false;

            foreach (var module in modules)
            {
                if (module == null) continue;

                var moduleType = module.GetType();
                
                var serialisationManagerField = moduleType.GetField("moduleSerialisationManager", BindingFlags.NonPublic | BindingFlags.Instance);
                if (serialisationManagerField == null) continue;

                var serialisationManager = serialisationManagerField.GetValue(module);
                if (serialisationManager == null) continue;

                var deserialiseMethod = serialisationManager.GetType().GetMethod("Deserialise", BindingFlags.Public | BindingFlags.Instance);
                if (deserialiseMethod != null)
                {
                    deserialiseMethod.Invoke(serialisationManager, new object[] { true, null! });
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region ChatBox States & Events

    /// <summary>
    /// Get all ChatBox states with optional prefix filter
    /// </summary>
    public static List<object>? GetChatBoxStates(string? prefixFilter = null)
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null) return null;

            var statesProp = chatBoxManager.GetType().GetProperty("States", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var statesField = chatBoxManager.GetType().GetField("States", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            object? statesCollection = statesProp?.GetValue(chatBoxManager) ?? statesField?.GetValue(chatBoxManager);
            if (statesCollection == null) return new List<object>();

            var result = new List<object>();
            if (statesCollection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;

                    var lookupProp = item.GetType().GetProperty("Lookup");
                    var titleProp = item.GetType().GetProperty("Title");

                    if (lookupProp == null || titleProp == null) continue;

                    var lookup = lookupProp.GetValue(item)?.ToString();
                    if (lookup == null) continue;

                    if (prefixFilter != null && !lookup.StartsWith(prefixFilter))
                        continue;

                    var titleObj = titleProp.GetValue(item);
                    var titleValueProp = titleObj?.GetType().GetProperty("Value");
                    var displayName = titleValueProp?.GetValue(titleObj)?.ToString() ?? lookup;

                    result.Add(new
                    {
                        name = prefixFilter != null ? lookup.Replace(prefixFilter, "") : lookup,
                        key = lookup,
                        displayName
                    });
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all VRCX states (states starting with vrcx_)
    /// </summary>
    public static List<object>? GetVRCXStates() => GetChatBoxStates("vrcx_");

    /// <summary>
    /// Get all ChatBox events with optional prefix filter
    /// </summary>
    public static List<object>? GetChatBoxEvents(string? prefixFilter = null)
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null) return null;

            var eventsProp = chatBoxManager.GetType().GetProperty("Events", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var eventsField = chatBoxManager.GetType().GetField("Events", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            object? eventsCollection = eventsProp?.GetValue(chatBoxManager) ?? eventsField?.GetValue(chatBoxManager);
            if (eventsCollection == null) return new List<object>();

            var result = new List<object>();
            if (eventsCollection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;

                    var lookupProp = item.GetType().GetProperty("Lookup");
                    var titleProp = item.GetType().GetProperty("Title");

                    if (lookupProp == null || titleProp == null) continue;

                    var lookup = lookupProp.GetValue(item)?.ToString();
                    if (lookup == null) continue;

                    if (prefixFilter != null && !lookup.StartsWith(prefixFilter))
                        continue;

                    var titleObj = titleProp.GetValue(item);
                    var titleValueProp = titleObj?.GetType().GetProperty("Value");
                    var displayName = titleValueProp?.GetValue(titleObj)?.ToString() ?? lookup;

                    result.Add(new
                    {
                        name = prefixFilter != null ? lookup.Replace(prefixFilter, "") : lookup,
                        key = lookup,
                        displayName
                    });
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all VRCX events (events starting with vrcx_)
    /// </summary>
    public static List<object>? GetVRCXEvents() => GetChatBoxEvents("vrcx_");

    #endregion

    #region Module Settings Helpers

    /// <summary>
    /// Get the module's settings file path
    /// </summary>
    /// <param name="module">The module instance</param>
    /// <returns>Full path to the module's settings JSON file, or null if not found</returns>
    public static string? GetModuleSettingsFilePath(object module)
    {
        try
        {
            // Try to get current profile's modules directory directly
            var modulesDir = GetCurrentProfileModulesPath();
            
            if (modulesDir != null && Directory.Exists(modulesDir))
            {
                // Use VRCOSC's actual module ID via reflection
                var fullId = GetModuleFullId(module);
                if (fullId != null)
                {
                    var exactPath = Path.Combine(modulesDir, $"{fullId}.json");
                    if (File.Exists(exactPath))
                        return exactPath;
                }

                // Fallback to naming pattern matching
                var moduleTypeName = module.GetType().Name.ToLowerInvariant();
                var possibleNames = new[]
                {
                    $"local.{moduleTypeName}.json",
                    $"{moduleTypeName}.json"
                };

                foreach (var name in possibleNames)
                {
                    var filePath = Path.Combine(modulesDir, name);
                    if (File.Exists(filePath))
                        return filePath;
                }
            }

            // Fallback: Search all profile directories if reflection failed
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VRCOSC"
            );

            if (!Directory.Exists(appDataPath))
                return null;

            var profilesPath = Path.Combine(appDataPath, "profiles");
            if (!Directory.Exists(profilesPath))
                return null;

            var fullId2 = GetModuleFullId(module);
            var moduleTypeName2 = module.GetType().Name.ToLowerInvariant();

            foreach (var profileDir in Directory.GetDirectories(profilesPath))
            {
                var fallbackModulesDir = Path.Combine(profileDir, "modules");
                if (!Directory.Exists(fallbackModulesDir))
                    continue;

                // Try exact match first
                if (fullId2 != null)
                {
                    var exactPath = Path.Combine(fallbackModulesDir, $"{fullId2}.json");
                    if (File.Exists(exactPath))
                        return exactPath;
                }

                // Fallback to patterns
                var possibleNames = new[]
                {
                    $"local.{moduleTypeName2}.json",
                    $"{moduleTypeName2}.json"
                };

                foreach (var name in possibleNames)
                {
                    var filePath = Path.Combine(fallbackModulesDir, name);
                    if (File.Exists(filePath))
                        return filePath;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the module's settings from disk as a dictionary with JsonElement values
    /// Reads the JSON file directly without requiring the module to be started
    /// JsonElement preserves the full JSON structure including nested objects and arrays
    /// </summary>
    /// <param name="module">The module instance</param>
    /// <returns>Dictionary containing the settings as JsonElements, or null if file not found or error</returns>
    public static Dictionary<string, System.Text.Json.JsonElement>? GetModuleSettings(object module)
    {
        try
        {
            var filePath = GetModuleSettingsFilePath(module);
            if (filePath == null || !File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            // The settings are nested under "settings" key
            if (doc.RootElement.TryGetProperty("settings", out var settingsElement))
            {
                var settings = new Dictionary<string, System.Text.Json.JsonElement>();
                
                foreach (var property in settingsElement.EnumerateObject())
                {
                    // Clone the JsonElement so it survives after the JsonDocument is disposed
                    settings[property.Name] = property.Value.Clone();
                }

                return settings;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get a specific setting value from disk
    /// </summary>
    /// <typeparam name="T">Type to cast the setting to</typeparam>
    /// <param name="module">The module instance</param>
    /// <param name="settingName">Name of the setting (case-insensitive)</param>
    /// <param name="defaultValue">Default value if setting not found</param>
    /// <returns>The setting value or default</returns>
    public static T? GetModuleSetting<T>(object module, string settingName, T? defaultValue = default)
    {
        try
        {
            var settings = GetModuleSettings(module);
            if (settings == null)
                return defaultValue;

            var key = settings.Keys.FirstOrDefault(k => k.Equals(settingName, StringComparison.OrdinalIgnoreCase));
            if (key == null)
                return defaultValue;

            var jsonElement = settings[key];

            // Handle different JSON value kinds
            try
            {
                // Try to deserialize directly to T
                return System.Text.Json.JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            catch
            {
                // Fallback: try simple conversions for primitives
                try
                {
                    var targetType = typeof(T);
                    
                    // Handle nullable types
                    if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        targetType = Nullable.GetUnderlyingType(targetType)!;
                    }

                    return jsonElement.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => (T?)(object?)jsonElement.GetString(),
                        System.Text.Json.JsonValueKind.Number when targetType == typeof(int) => (T?)(object?)jsonElement.GetInt32(),
                        System.Text.Json.JsonValueKind.Number when targetType == typeof(long) => (T?)(object?)jsonElement.GetInt64(),
                        System.Text.Json.JsonValueKind.Number when targetType == typeof(float) => (T?)(object?)(float)jsonElement.GetDouble(),
                        System.Text.Json.JsonValueKind.Number when targetType == typeof(double) => (T?)(object?)jsonElement.GetDouble(),
                        System.Text.Json.JsonValueKind.True => (T?)(object?)true,
                        System.Text.Json.JsonValueKind.False => (T?)(object?)false,
                        _ => defaultValue
                    };
                }
                catch
                {
                    return defaultValue;
                }
            }
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Check if module is enabled in settings
    /// </summary>
    public static bool IsModuleEnabled(object module)
    {
        try
        {
            var filePath = GetModuleSettingsFilePath(module);
            if (filePath == null || !File.Exists(filePath))
                return false;

            var json = File.ReadAllText(filePath);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("enabled", out var enabledElement))
            {
                return enabledElement.GetBoolean();
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Module ID Helpers

    /// <summary>
    /// Get the module's ID as VRCOSC sees it
    /// Returns: module type name in lowercase (e.g., "notificationsmodule")
    /// </summary>
    public static string? GetModuleId(object module)
    {
        try
        {
            var idProp = module.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return idProp?.GetValue(module)?.ToString();
        }
        catch
        {
            // Fallback to manual calculation
            return module.GetType().Name.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Get the module's package ID
    /// Returns: "local" for local modules, or package name for remote modules
    /// </summary>
    public static string? GetModulePackageId(object module)
    {
        try
        {
            var packageIdProp = module.GetType().GetProperty("PackageID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return packageIdProp?.GetValue(module)?.ToString();
        }
        catch
        {
            return "local";
        }
    }

    /// <summary>
    /// Get the module's full ID as used for file naming
    /// Returns: "{packageid}.{moduleid}" (e.g., "local.notificationsmodule")
    /// </summary>
    public static string? GetModuleFullId(object module)
    {
        try
        {
            var fullIdProp = module.GetType().GetProperty("FullID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return fullIdProp?.GetValue(module)?.ToString();
        }
        catch
        {
            // Fallback to manual calculation
            var packageId = GetModulePackageId(module) ?? "local";
            var moduleId = GetModuleId(module) ?? module.GetType().Name.ToLowerInvariant();
            return $"{packageId}.{moduleId}";
        }
    }

    #endregion

    #region VRCOSC SDK Reflection Helpers

    /// <summary>
    /// Get the SendParameter method from Module base class via reflection (cached)
    /// Useful for intercepting parameter sends
    /// </summary>
    public static MethodInfo? GetModuleSendParameterMethod()
    {
        try
        {
            _moduleSendParameterMethod ??= typeof(VRCOSC.App.SDK.Modules.Module).GetMethod(
                "SendParameter", 
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, 
                null, 
                new[] { typeof(string), typeof(object) }, 
                null
            );
            return _moduleSendParameterMethod;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the Parameters field from a Module instance (cached FieldInfo)
    /// Returns Dictionary&lt;Enum, ModuleParameter&gt;
    /// </summary>
    public static object? GetModuleParametersField(object module)
    {
        try
        {
            _moduleParametersField ??= typeof(VRCOSC.App.SDK.Modules.Module).GetField(
                "Parameters", 
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            return _moduleParametersField?.GetValue(module);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get parameter name from ModuleParameter object via reflection
    /// Navigates through Name.Value property chain
    /// </summary>
    public static string? GetParameterName(object parameterObject)
    {
        try
        {
            var nameProperty = parameterObject.GetType().GetProperty("Name");
            if (nameProperty == null) return null;

            var nameObservable = nameProperty.GetValue(parameterObject);
            if (nameObservable == null) return null;

            var valueProperty = nameObservable.GetType().GetProperty("Value");
            if (valueProperty == null) return null;

            return valueProperty.GetValue(nameObservable)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get parameter display name from ModuleParameter object
    /// </summary>
    public static string? GetParameterDisplayName(object parameterObject)
    {
        try
        {
            var displayNameProperty = parameterObject.GetType().GetProperty("DisplayName");
            if (displayNameProperty == null) return null;

            var displayNameObservable = displayNameProperty.GetValue(parameterObject);
            if (displayNameObservable == null) return null;

            var valueProperty = displayNameObservable.GetType().GetProperty("Value");
            return valueProperty?.GetValue(displayNameObservable)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get all parameters from a Module instance (cached FieldInfo)
    /// Returns dictionary of Enum -&gt; ModuleParameter
    /// </summary>
    public static Dictionary<Enum, object>? GetAllModuleParameters(object module)
    {
        try
        {
            var parametersField = GetModuleParametersField(module);
            if (parametersField == null) return null;

            // Convert to dictionary we can work with
            var result = new Dictionary<Enum, object>();
            
            if (parametersField is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is Enum enumKey && entry.Value != null)
                    {
                        result[enumKey] = entry.Value;
                    }
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
