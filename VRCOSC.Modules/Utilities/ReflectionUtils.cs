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
    #region Type Resolution

    /// <summary>
    /// Resolve a type from a host assembly by full name, independent of load context.
    ///
    /// A plain <c>Type.GetType("Some.Type, VRCOSC.App")</c> resolves against the *calling*
    /// assembly's <see cref="System.Runtime.Loader.AssemblyLoadContext"/>. VRCOSC loads
    /// module packages into their own ALC, so a plain Type.GetType from module code can
    /// return null even though the host assembly is loaded — silently, since every caller
    /// here treats null as "not available". That produced empty module lists, a null
    /// active profile, and a Debug-module auto-start that never fired.
    ///
    /// Searching the loaded assemblies finds the host's already-loaded copy regardless of
    /// which context asks.
    /// </summary>
    private static Type? FindHostType(string fullTypeName, string assemblyName)
    {
        // Fast path: works when the calling context can already see the assembly.
        var type = Type.GetType($"{fullTypeName}, {assemblyName}");
        if (type != null) return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                continue;

            type = assembly.GetType(fullTypeName);
            if (type != null) return type;
        }

        return null;
    }

    /// <summary>
    /// Where VRCOSC actually stores its data (the directory containing
    /// <c>profiles/</c>, <c>configuration/</c>, <c>packages/</c>, <c>logs/</c>).
    ///
    /// Asks the host rather than reconstructing the path: <c>AppManager.Storage.BasePath</c>
    /// is whatever VRCOSC itself resolved at startup, so this stays correct no matter which
    /// OS user, Wine user, or prefix the process runs under — nothing here assumes a
    /// username or a fixed layout.
    ///
    /// Falls back to the same expression VRCOSC uses internally
    /// (<c>ApplicationData/VRCOSC</c>) only when the host isn't reachable yet.
    /// </summary>
    public static string? GetVrcoscBasePath()
    {
        try
        {
            var appManager = GetAppManager();
            if (appManager != null)
            {
                var storage = GetMemberValue(appManager, "Storage");
                if (storage != null &&
                    GetMemberValue(storage, "BasePath") is string basePath &&
                    !string.IsNullOrWhiteSpace(basePath) &&
                    Directory.Exists(basePath))
                {
                    return basePath;
                }
            }
        }
        catch
        {
            // Fall through to the environment-derived path below.
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData)) return null;

        var fallback = Path.Combine(appData, "VRCOSC");
        return Directory.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// Read an instance member by name, whether it is declared as a property or a field.
    ///
    /// VRCOSC's internals mix both — <c>Profile.ID</c> is a plain field while
    /// <c>Module.Enabled</c> is a property. Code that only calls GetProperty silently
    /// returns null for the fields, so prefer this for any host member lookup.
    /// </summary>
    public static object? GetMemberValue(object instance, string memberName)
    {
        var type = instance.GetType();

        var property = type.GetProperty(memberName, AnyInstance);
        if (property != null) return property.GetValue(instance);

        var field = type.GetField(memberName, AnyInstance);
        return field?.GetValue(instance);
    }

    #endregion

    #region Generic reflection primitives
    // ------------------------------------------------------------------------------------
    // Reflection belongs in this class. Module code should call these rather than reaching
    // for System.Reflection directly, so that the BindingFlags choices, the property-vs-field
    // fallback and the null handling are decided in exactly one place.
    //
    // Why that matters here specifically: VRCOSC's internals mix fields and properties
    // (Profile.ID is a field, Module.Enabled is a property), and a bare GetProperty call
    // silently returns null for the fields rather than throwing. Bugs from that look like
    // "the value is just empty" and are tedious to trace. Every helper below goes through
    // GetMemberValue, which checks both.
    // ------------------------------------------------------------------------------------

    /// <summary>Public + non-public instance members. The right default for host internals.</summary>
    public const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    /// <summary>Public + non-public static members.</summary>
    public const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>
    /// Reads a property or field and converts it to <typeparamref name="T"/>, returning
    /// <paramref name="defaultValue"/> if the member is missing, null, or not convertible.
    /// Never throws.
    /// </summary>
    public static T? GetMemberValue<T>(object? instance, string memberName, T? defaultValue = default)
    {
        if (instance is null) return defaultValue;

        try
        {
            var value = GetMemberValue(instance, memberName);
            if (value is null) return defaultValue;
            if (value is T typed) return typed;

            // Handles the int/long/enum widening that reflection hands back.
            var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return target.IsEnum
                ? (T)Enum.ToObject(target, value)
                : (T)Convert.ChangeType(value, target);
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>Try-pattern variant of <see cref="GetMemberValue{T}"/>.</summary>
    public static bool TryGetMemberValue<T>(object? instance, string memberName, out T? value)
    {
        value = default;
        if (instance is null) return false;

        var raw = GetMemberValue(instance, memberName);
        if (raw is null) return false;

        value = GetMemberValue<T>(instance, memberName);
        return value is not null;
    }

    /// <summary>
    /// Writes a property or field. Returns false if the member does not exist, is
    /// read-only, or the value will not convert — rather than throwing.
    /// </summary>
    public static bool SetMemberValue(object? instance, string memberName, object? value)
    {
        if (instance is null) return false;

        try
        {
            var type = instance.GetType();

            var property = type.GetProperty(memberName, AnyInstance);
            if (property is { CanWrite: true })
            {
                property.SetValue(instance, ConvertFor(property.PropertyType, value));
                return true;
            }

            var field = type.GetField(memberName, AnyInstance);
            if (field is not null && !field.IsInitOnly)
            {
                field.SetValue(instance, ConvertFor(field.FieldType, value));
                return true;
            }
        }
        catch
        {
            // fall through
        }

        return false;
    }

    private static object? ConvertFor(Type target, object? value)
    {
        if (value is null) return null;
        if (target.IsInstanceOfType(value)) return value;

        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        return underlying.IsEnum ? Enum.ToObject(underlying, value) : Convert.ChangeType(value, underlying);
    }

    /// <summary>
    /// Invokes an instance method by name. Returns null if it does not exist or throws.
    /// Overloads are resolved by argument types, so pass the arguments you actually mean.
    /// </summary>
    public static object? InvokeMethod(object? instance, string methodName, params object?[] args)
    {
        if (instance is null) return null;

        try
        {
            var type = instance.GetType();
            var argTypes = args.Select(a => a?.GetType() ?? typeof(object)).ToArray();

            var method = type.GetMethod(methodName, AnyInstance, null, argTypes, null)
                         ?? type.GetMethod(methodName, AnyInstance);

            return method?.Invoke(instance, args);
        }
        catch (TargetInvocationException e)
        {
            // Unwrap so callers see the real failure, not the reflection wrapper.
            Logger?.Invoke($"Reflection: {methodName} threw {e.InnerException?.GetType().Name}: {e.InnerException?.Message}");
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Typed variant of <see cref="InvokeMethod"/>.</summary>
    public static T? InvokeMethod<T>(object? instance, string methodName, params object?[] args)
    {
        var result = InvokeMethod(instance, methodName, args);
        return result is T typed ? typed : default;
    }

    /// <summary>
    /// Invokes a generic method by name, closing it over <paramref name="typeArguments"/>.
    /// <see cref="InvokeMethod"/> cannot do this — an open generic must be closed with
    /// MakeGenericMethod before it can be called.
    /// </summary>
    public static object? InvokeGenericMethod(object? instance, string methodName, Type[] typeArguments,
                                              Type[] parameterTypes, params object?[] args)
    {
        if (instance is null) return null;

        try
        {
            var method = instance.GetType().GetMethod(methodName, AnyInstance, null, parameterTypes, null);
            return method?.MakeGenericMethod(typeArguments).Invoke(instance, args);
        }
        catch (TargetInvocationException e)
        {
            Logger?.Invoke($"Reflection: {methodName}<{string.Join(",", typeArguments.Select(t => t.Name))}> threw: {e.InnerException?.Message}");
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a module variable via the SDK's protected <c>GetVariableValue&lt;T&gt;(Enum)</c>.
    /// </summary>
    /// <remarks>
    /// Modules need this to expose their own variables to their nodes, but the SDK method is
    /// protected, so it can only be reached reflectively. Two modules had hand-rolled the same
    /// MakeGenericMethod dance; this is that call, once.
    /// </remarks>
    public static T? GetModuleVariableValue<T>(object module, Enum variable, T? defaultValue = default)
    {
        var result = InvokeGenericMethod(module, "GetVariableValue", [typeof(T)], [typeof(Enum)], variable);
        return result is T typed ? typed : defaultValue;
    }

    /// <summary>
    /// All methods on <paramref name="type"/> carrying <typeparamref name="TAttribute"/>.
    /// For attribute-driven registration, where the caller genuinely needs MethodInfo
    /// rather than a value (e.g. handing them to a framework factory).
    /// </summary>
    public static IEnumerable<MethodInfo> GetMethodsWithAttribute<TAttribute>(Type type, BindingFlags flags = AnyStatic)
        where TAttribute : Attribute
        => type.GetMethods(flags).Where(m => m.GetCustomAttribute<TAttribute>() != null);

    /// <summary>Reads a static property or field off <paramref name="type"/>.</summary>
    public static object? GetStaticMemberValue(Type type, string memberName)
    {
        var property = type.GetProperty(memberName, AnyStatic);
        if (property != null) return property.GetValue(null);

        return type.GetField(memberName, AnyStatic)?.GetValue(null);
    }

    /// <summary>
    /// Optional sink for diagnostics from the helpers above. Left unset they stay silent,
    /// which is what module code wants — reflection failures here are usually expected
    /// (a host member that only exists on some VRCOSC versions).
    /// </summary>
    public static Action<string>? Logger { get; set; }

    #endregion

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
    private static PropertyInfo? _chatBoxPulseTextProp;
    private static PropertyInfo? _chatBoxPulseMinimalBgProp;

    // Field caches
    private static FieldInfo? _moduleParametersField;
    private static FieldInfo? _appManagerOscClientField;
    
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
            _appManagerType ??= FindHostType("VRCOSC.App.AppManager", "VRCOSC.App");
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
            _moduleManagerType ??= FindHostType("VRCOSC.App.Modules.ModuleManager", "VRCOSC.App");
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
            var profileManagerType = FindHostType("VRCOSC.App.Profiles.ProfileManager", "VRCOSC.App");
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

            // Profile.ID is a *field* (`public Guid ID;`), not a property — GetProperty
            // returns null for it, which is what made this silently return null and take
            // every caller down the wrong-profile fallback path.
            return GetMemberValue(profile, "ID")?.ToString();
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

            var basePath = GetVrcoscBasePath();
            if (basePath == null) return null;

            var appDataPath = Path.Combine(basePath, "profiles", profileId, "modules");

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
            _chatBoxManagerType ??= FindHostType("VRCOSC.App.ChatBox.ChatBoxManager", "VRCOSC.App");
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
    /// Get current chatbox text from ChatBoxManager
    /// Returns the text as it would appear in the "ChatBox Preview" window
    /// </summary>
    public static string? GetChatBoxText()
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null) return null;

            // Try PulseText first (text set by modules via SendChatBox)
            var pulseTextProp = chatBoxManager.GetType().GetProperty("PulseText", BindingFlags.Public | BindingFlags.Instance);
            var pulseText = pulseTextProp?.GetValue(chatBoxManager) as string;
            
            if (!string.IsNullOrEmpty(pulseText))
                return pulseText;

            // Fall back to LiveText (text from clips/timeline)
            var liveTextProp = chatBoxManager.GetType().GetProperty("LiveText", BindingFlags.Public | BindingFlags.Instance);
            var liveText = liveTextProp?.GetValue(chatBoxManager) as string;

            return liveText ?? string.Empty;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get comprehensive chatbox state from ChatBoxManager
    /// Returns object with current text, pulse text, live text, typing state, minimal background, etc.
    /// </summary>
    public static object? GetChatBoxState()
    {
        try
        {
            var chatBoxManager = GetChatBoxManager();
            if (chatBoxManager == null) return null;

            var chatBoxType = chatBoxManager.GetType();

            // Get all relevant properties
            var liveTextProp = chatBoxType.GetProperty("LiveText", BindingFlags.Public | BindingFlags.Instance);
            var pulseTextProp = chatBoxType.GetProperty("PulseText", BindingFlags.Public | BindingFlags.Instance);
            var pulseMinimalBgProp = chatBoxType.GetProperty("PulseMinimalBackground", BindingFlags.Public | BindingFlags.Instance);
            var sendEnabledProp = chatBoxType.GetProperty("SendEnabled", BindingFlags.Public | BindingFlags.Instance);

            var liveText = liveTextProp?.GetValue(chatBoxManager) as string ?? string.Empty;
            var pulseText = pulseTextProp?.GetValue(chatBoxManager) as string;
            var pulseMinimalBg = pulseMinimalBgProp?.GetValue(chatBoxManager) as bool? ?? false;
            var sendEnabled = sendEnabledProp?.GetValue(chatBoxManager) as bool? ?? false;

            // Determine current text (what's actually displayed)
            var currentText = !string.IsNullOrEmpty(pulseText) ? pulseText : liveText;

            // Check if typing (PulseText is set but send is false)
            var isTyping = !string.IsNullOrEmpty(pulseText) && !sendEnabled;

            return new
            {
                currentText = currentText,
                liveText = liveText,
                pulseText = pulseText,
                minimalBackground = pulseMinimalBg,
                isTyping = isTyping,
                sendEnabled = sendEnabled
            };
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

            // Cache field and methods (VRChatOscClient is a field, not a property!)
            _appManagerOscClientField ??= appManager.GetType().GetField("VRChatOscClient", BindingFlags.Public | BindingFlags.Instance);
            if (_appManagerOscClientField == null) return false;

            var oscClient = _appManagerOscClientField.GetValue(appManager);
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
    public static (bool Success, string? Error) SendOscParameter(string parameterName, object value)
    {
        try
        {
            var (appManager, error) = GetAppManagerWithError();
            if (appManager == null)
            {
                System.Diagnostics.Debug.WriteLine($"SendOscParameter: AppManager is null - {error}");
                return (false, error ?? "AppManager not available");
            }

            // Get VRChatOscClient field (it's a field, not a property!)
            var oscClientField = appManager.GetType().GetField("VRChatOscClient", BindingFlags.Public | BindingFlags.Instance);
            if (oscClientField == null)
            {
                System.Diagnostics.Debug.WriteLine("SendOscParameter: VRChatOscClient field not found");
                return (false, "VRChatOscClient field not found");
            }

            var oscClient = oscClientField.GetValue(appManager);
            if (oscClient == null)
            {
                System.Diagnostics.Debug.WriteLine("SendOscParameter: VRChatOscClient is null - VRCOSC may not be started or OSC not connected");
                return (false, "OSC client not initialized - is VRCOSC started?");
            }

            // Get Send method - it has multiple overloads, find the one that takes (string, object)
            var sendMethod = oscClient.GetType().GetMethod("Send", 
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(object) },
                null);
            
            if (sendMethod == null)
            {
                // Try finding any Send method
                var allSendMethods = oscClient.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Send").ToArray();
                System.Diagnostics.Debug.WriteLine($"SendOscParameter: Available Send methods: {string.Join(", ", allSendMethods.Select(m => m.ToString()))}");
                
                // Use the first Send method with at least 2 parameters
                sendMethod = allSendMethods.FirstOrDefault(m => m.GetParameters().Length >= 2);
                
                if (sendMethod == null)
                {
                    System.Diagnostics.Debug.WriteLine("SendOscParameter: No suitable Send method found on VRChatOscClient");
                    return (false, "Send method not found on OSC client");
                }
            }

            // Build full address
            var address = parameterName.StartsWith("/avatar/parameters/") 
                ? parameterName 
                : $"/avatar/parameters/{parameterName}";

            System.Diagnostics.Debug.WriteLine($"SendOscParameter: Sending {address} = {value} using {sendMethod}");
            sendMethod.Invoke(oscClient, new object[] { address, value });
            System.Diagnostics.Debug.WriteLine($"SendOscParameter: Successfully sent {address}");
            return (true, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SendOscParameter exception: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            return (false, $"Exception: {ex.Message}");
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
            if (appManager == null)
            {
                System.Diagnostics.Debug.WriteLine("GetCurrentAvatarInfo: AppManager is null");
                return null;
            }

            // Get currentAvatarConfig property (not field)
            var avatarConfigProp = appManager.GetType().GetProperty("currentAvatarConfig", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (avatarConfigProp == null)
            {
                System.Diagnostics.Debug.WriteLine("GetCurrentAvatarInfo: currentAvatarConfig property not found");
                return null;
            }

            var avatarConfig = avatarConfigProp.GetValue(appManager);
            if (avatarConfig == null)
            {
                System.Diagnostics.Debug.WriteLine("GetCurrentAvatarInfo: currentAvatarConfig is null (no avatar loaded)");
                return (null, null); // No avatar loaded
            }

            System.Diagnostics.Debug.WriteLine($"GetCurrentAvatarInfo: Got avatarConfig type: {avatarConfig.GetType().FullName}");

            // Get Id and Name properties
            var idProperty = avatarConfig.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            var nameProperty = avatarConfig.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);

            var id = idProperty?.GetValue(avatarConfig) as string;
            var name = nameProperty?.GetValue(avatarConfig) as string;

            System.Diagnostics.Debug.WriteLine($"GetCurrentAvatarInfo: ID={id}, Name={name}");

            return (id, name);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetCurrentAvatarInfo exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get all modules with their information (name, id, enabled, running, etc.)
    /// </summary>
    public static List<object>? GetAllModulesInfo()
    {
        try
        {
            var moduleManager = GetModuleManager();
            if (moduleManager == null)
            {
                System.Diagnostics.Debug.WriteLine("GetAllModulesInfo: ModuleManager is null");
                return null;
            }

            // ModuleManager exposes a private `modules` property that already flattens
            // Modules.Values.SelectMany(...). Prefer it: ObservableDictionary implements
            // IDictionary<,> *and* declares its own Values, so GetProperty("Values") can
            // throw AmbiguousMatchException — which the catch below turned into a silent
            // null, reporting zero modules while 60 were loaded.
            var flat = GetMemberValue(moduleManager, "modules") as System.Collections.IEnumerable;
            if (flat == null)
            {
                System.Diagnostics.Debug.WriteLine("GetAllModulesInfo: could not read ModuleManager.modules");
                return null;
            }

            var modulesList = new List<object>();
            int totalModules = 0;

            foreach (var module in flat)
            {
                if (module == null) continue;
                totalModules++;

                    var moduleType = module.GetType();

                // Get basic properties
                var titleAttr = moduleType.GetCustomAttribute(typeof(System.ComponentModel.DisplayNameAttribute), true) 
                    ?? moduleType.GetCustomAttribute(FindHostType("VRCOSC.App.SDK.Modules.ModuleTitleAttribute", "VRCOSC.App.SDK") ?? typeof(object), true);
                var descAttr = moduleType.GetCustomAttribute(FindHostType("VRCOSC.App.SDK.Modules.ModuleDescriptionAttribute", "VRCOSC.App.SDK") ?? typeof(object), true);
                var authorAttr = moduleType.GetCustomAttribute(FindHostType("VRCOSC.App.SDK.Modules.ModuleAuthorAttribute", "VRCOSC.App.SDK") ?? typeof(object), true);

                var titleProp = titleAttr?.GetType().GetProperty("Title") ?? titleAttr?.GetType().GetProperty("DisplayName");
                var descProp = descAttr?.GetType().GetProperty("Description");
                var authorProp = authorAttr?.GetType().GetProperty("Author") ?? authorAttr?.GetType().GetProperty("AuthorName");

                var title = titleProp?.GetValue(titleAttr) as string ?? moduleType.Name;
                var description = descProp?.GetValue(descAttr) as string ?? "";
                var author = authorProp?.GetValue(authorAttr) as string;

                // Get state
                var stateProp = moduleType.GetProperty("State", BindingFlags.Public | BindingFlags.Instance);
                var stateObservable = stateProp?.GetValue(module);
                var stateValue = stateObservable?.GetType().GetProperty("Value")?.GetValue(stateObservable);
                var stateStr = stateValue?.ToString() ?? "Unknown";

                // Get enabled
                var enabledProp = moduleType.GetProperty("Enabled", BindingFlags.Public | BindingFlags.Instance);
                var enabledObservable = enabledProp?.GetValue(module);
                var enabled = enabledObservable?.GetType().GetProperty("Value")?.GetValue(enabledObservable) as bool? ?? false;

                // Get IDs
                var fullId = GetModuleFullId(module);
                var (packageId, moduleId) = ParseFullId(fullId);

                    modulesList.Add(new
                    {
                        name = title,
                        id = moduleId,
                        packageId = packageId,
                        fullId = fullId,
                        author = author,
                        description = description,
                        enabled = enabled,
                        state = stateStr,
                        running = stateStr.Equals("Started", StringComparison.OrdinalIgnoreCase)
                    });
            }

            return modulesList;
        }
        catch
        {
            return null;
        }
    }

    private static (string packageId, string moduleId) ParseFullId(string? fullId)
    {
        if (string.IsNullOrEmpty(fullId)) return ("unknown", "unknown");
        
        var parts = fullId.Split('#');
        if (parts.Length == 2)
            return (parts[0], parts[1]);
        
        return ("unknown", fullId);
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
    /// Get all loaded modules (flattened list from ModuleManager)
    /// </summary>
    public static IEnumerable? GetModules()
    {
        try
        {
            var moduleManager = GetModuleManager();
            if (moduleManager == null) return null;

            // Get Modules property - ObservableDictionary<ModulePackage, List<Module>>
            _moduleManagerModulesProp ??= moduleManager.GetType().GetProperty("Modules", BindingFlags.Public | BindingFlags.Instance);
            var modulesDict = _moduleManagerModulesProp?.GetValue(moduleManager);
            if (modulesDict == null) return null;

            // Flatten the dictionary - get all List<Module> values and combine them
            var valuesProperty = modulesDict.GetType().GetProperty("Values");
            var values = valuesProperty?.GetValue(modulesDict) as System.Collections.IEnumerable;
            
            if (values == null) return null;

            var allModules = new List<object>();
            foreach (var moduleList in values)
            {
                if (moduleList is System.Collections.IEnumerable enumerable)
                {
                    foreach (var module in enumerable)
                    {
                        if (module != null) allModules.Add(module);
                    }
                }
            }

            return allModules;
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
            var appDataPath = GetVrcoscBasePath();
            if (appDataPath == null)
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
