// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bluscream.Modules.Debug;
using Bluscream;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("Debug")]
[ModuleDescription("Debug tools for tracking and exporting OSC parameters")]
[ModuleType(ModuleType.Generic)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class DebugModule : VRCOSC.App.SDK.Modules.Module
{
    private IncomingParameterTracker? _incomingTracker;
    private OutgoingParameterTracker? _outgoingTracker;
    private MethodInfo? _baseSendParameterMethod;

    protected override void OnPreLoad()
    {
        // Dump settings
        CreateTextBox(DebugSetting.DumpDirectory, "Dump Directory", "Directory for parameter dumps (leave empty for 'dumps' folder in module directory)", string.Empty);
        CreateDropdown(DebugSetting.SortBy, "Sort By", "Which column to sort the CSV by before saving", CsvSortBy.ParameterPath);
        CreateDropdown(DebugSetting.SortDirection, "Sort Direction", "Sort order for CSV export", CsvSortDirection.Ascending);
        
        // Tracking mode - DISABLED (always using VRCOSC tracking)
        // CreateToggle(DebugSetting.UseVrcoscTracking, "Use VRCOSC Tracking", "Use VRCOSC's built-in parameter tracking", true);
        // CreateToggle(DebugSetting.TrackAvatarOnly, "Avatar Parameters Only", "Only track avatar parameters", true);
        // CreateToggle(DebugSetting.AutoTrackIncoming, "Auto-Track Incoming", "Automatically track incoming parameters", true);
        // CreateToggle(DebugSetting.AutoTrackOutgoing, "Auto-Track Outgoing", "Automatically track outgoing parameters", true);
        // CreateSlider(DebugSetting.MaxParameters, "Max Parameters", "Maximum parameters to track (0 = unlimited)", 0, 0, 10000, 100);
        
        // Debug settings
        CreateToggle(DebugSetting.LogParameterUpdates, "Log Parameter Updates", "Log all parameter updates to console", false);
        CreateToggle(DebugSetting.AutoStartModules, "Auto Start Modules on Load", "Automatically start all VRCOSC modules 5 seconds after this module loads", false);

        // OSC Parameters
        RegisterParameter<bool>(DebugParameter.DumpNow, "VRCOSC/Debug/DumpNow", ParameterMode.Read, "Dump Now", "Set to true to trigger a parameter dump");
        RegisterParameter<bool>(DebugParameter.ClearTracking, "VRCOSC/Debug/Clear", ParameterMode.Read, "Clear Tracking", "Set to true to clear tracked parameters");
        RegisterParameter<int>(DebugParameter.IncomingCount, "VRCOSC/Debug/IncomingCount", ParameterMode.Write, "Incoming Count", "Number of unique incoming parameters");
        RegisterParameter<int>(DebugParameter.OutgoingCount, "VRCOSC/Debug/OutgoingCount", ParameterMode.Write, "Outgoing Count", "Number of unique outgoing parameters");
        RegisterParameter<int>(DebugParameter.TotalCount, "VRCOSC/Debug/TotalCount", ParameterMode.Write, "Total Count", "Total unique parameters (incoming + outgoing)");
    }

    protected override void OnPostLoad()
    {
        // ChatBox variables
        var incomingCountRef = CreateVariable<int>(DebugVariable.IncomingCount, "Incoming Count")!;
        var outgoingCountRef = CreateVariable<int>(DebugVariable.OutgoingCount, "Outgoing Count")!;
        var totalCountRef = CreateVariable<int>(DebugVariable.TotalCount, "Total Count")!;
        var lastDumpPathRef = CreateVariable<string>(DebugVariable.LastDumpPath, "Last Dump Path")!;

        // ChatBox states (cast to non-nullable IEnumerable to satisfy nullability)
        CreateState(DebugState.Idle, "Idle", "Debug\nTracking: {0} params", new[] { totalCountRef }.Where(x => x != null));
        CreateState(DebugState.Dumping, "Dumping", "Dumping {0} params...", new[] { totalCountRef }.Where(x => x != null));

        // ChatBox events
        CreateEvent(DebugEvent.OnDumpComplete, "Dump Complete", "Dumped to: {0}", new[] { lastDumpPathRef }.Where(x => x != null));
        CreateEvent(DebugEvent.OnTrackingCleared, "Tracking Cleared", "Cleared all tracked parameters");

        ChangeState(DebugState.Idle);
        
        // Auto-start VRCOSC if enabled (read from disk since settings aren't loaded yet in OnPostLoad)
        if (this.GetSetting("AutoStartModules", false))
        {
            _ = Task.Run(async () =>
            {
                // Small delay to ensure VRCOSC is fully initialized
                await Task.Delay(1000);
                
                Log("Auto-start enabled, checking VRCOSC state...");
                
                var currentState = ReflectionUtils.GetAppManagerState();
                Log($"Current AppManager state: {currentState ?? "Unknown"}");
                
                if (currentState == "Started" || currentState == "Starting")
                {
                    Log($"⏭ VRCOSC is already {currentState}, skipping auto-start");
                    return;
                }
                
                Log("🚀 Force-starting VRCOSC (skipping VRChat detection)...");
                var error = ReflectionUtils.ForceAppManagerStart();
                
                if (error != null)
                {
                    Log($"❌ Auto-start failed: {error}");
                    return;
                }
                
                Log("⏳ Waiting for VRCOSC to complete startup...");
                var isStarted = await ReflectionUtils.WaitForAppManagerStarted(30000);
                
                if (!isStarted)
                {
                    Log("⚠ VRCOSC didn't reach 'Started' state within 30 seconds");
                    return;
                }
                
                Log("✅ VRCOSC auto-started successfully - all enabled modules are now running");
            });
        }
    }

    protected override Task<bool> OnModuleStart()
    {
        // Initialize custom trackers
        _incomingTracker = new IncomingParameterTracker(
            10000,  // max parameters
            IsLoggingEnabled(),
            false   // track all parameters, not just avatar
        );
        
        _outgoingTracker = new OutgoingParameterTracker(
            10000,  // max parameters
            IsLoggingEnabled()
        );

        // Subscribe to tracker events
        _incomingTracker.OnParameterTracked += OnIncomingTracked;
        _incomingTracker.OnParameterReceived += OnIncomingReceived;
        _incomingTracker.OnMaxLimitReached += OnMaxLimitReached;
        _incomingTracker.OnCleared += OnTrackingCleared;

        _outgoingTracker.OnParameterTracked += OnOutgoingTracked;
        _outgoingTracker.OnParameterSent += OnOutgoingSent;
        _outgoingTracker.OnMaxLimitReached += OnMaxLimitReached;

        // Cache the base SendParameter method for reflection-based interception
        _baseSendParameterMethod = ReflectionUtils.GetModuleSendParameterMethod();
        
        Log("Using custom parameter tracking");
        
        Log("Debug module started");
        UpdateCounts();
        
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        var incoming = _incomingTracker?.GetAllParameters().Count ?? 0;
        var outgoing = _outgoingTracker?.GetAllParameters().Count ?? 0;
        Log($"Stopped tracking. Final counts: {incoming} incoming, {outgoing} outgoing");
        return Task.CompletedTask;
    }

    private void OnIncomingTracked(ParameterData data)
    {
        UpdateCounts();
    }

    private void OnOutgoingTracked(ParameterData data)
    {
        UpdateCounts();
    }

    private void OnIncomingReceived(string path, string type, object? value)
    {
        if (IsLoggingEnabled())
        {
            Log($"INCOMING: {path} ({type}) = {value}");
        }
    }

    private void OnOutgoingSent(string path, string type, object? value)
    {
        if (IsLoggingEnabled())
        {
            Log($"OUTGOING: {path} ({type}) = {value}");
        }
    }

    private void OnMaxLimitReached(string path)
    {
        Log($"Warning: Max parameter limit (10000) reached, skipping: {path}");
    }

    private void OnTrackingCleared()
    {
        UpdateCounts();
    }

    protected override void OnAnyParameterReceived(VRChatParameter parameter)
    {
        if (_incomingTracker == null) return;
        _incomingTracker.ProcessParameter(parameter);
    }
    
    // Override SendParameter to track outgoing parameters
    protected new void SendParameter(string name, object value)
    {
        // Track outgoing
        if (_outgoingTracker != null)
        {
            _outgoingTracker.ProcessParameter(name, value);
        }
        
        // Call base implementation using reflection
        _baseSendParameterMethod?.Invoke(this, new[] { name, value });
    }
    
    protected new void SendParameter(Enum lookup, object value)
    {
        // Get the parameter name from reflection to access internal Parameters dictionary
        var parametersDict = ReflectionUtils.GetAllModuleParameters(this);
        if (parametersDict != null)
        {
            // Track using the dictionary
            if (_outgoingTracker != null)
            {
                _outgoingTracker.ProcessParameter(lookup, value, parametersDict);
            }
            
            // Get parameter name and send
            if (parametersDict.TryGetValue(lookup, out var moduleParameter))
            {
                var paramName = ReflectionUtils.GetParameterName(moduleParameter);
                if (!string.IsNullOrEmpty(paramName))
                {
                    SendParameter(paramName, value);
                    return;
                }
            }
        }
        
        // Fallback to base if reflection fails
        _baseSendParameterMethod?.Invoke(this, new[] { lookup, value });
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        if ((DebugParameter)parameter.Lookup == DebugParameter.DumpNow && parameter.GetValue<bool>())
        {
            Log("Dump triggered via OSC parameter");
            _ = Task.Run(async () => await DumpParametersAsync());
        }
        else if ((DebugParameter)parameter.Lookup == DebugParameter.ClearTracking && parameter.GetValue<bool>())
        {
            Log("Clear tracking triggered via OSC parameter");
            ClearTracking();
        }
    }


    public async Task<string> DumpParametersAsync(bool includeIncoming = true, bool includeOutgoing = true, string? customFilePath = null)
    {
        ChangeState(DebugState.Dumping);
        
        try
        {
            string filepath;
            
            if (!string.IsNullOrWhiteSpace(customFilePath))
            {
                // Use custom file path
                filepath = customFilePath;
                var directory = System.IO.Path.GetDirectoryName(filepath);
                if (!string.IsNullOrEmpty(directory) && !System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }
            }
            else
            {
                // Use auto-generated path
                var dumpDir = GetDumpDirectory();
                if (!System.IO.Directory.Exists(dumpDir))
                {
                    System.IO.Directory.CreateDirectory(dumpDir);
                }

                var timestamp = DateTime.Now.ToString("ddMMyyyy-HH-mm-ss");
                var filename = $"params_{timestamp}.csv";
                filepath = System.IO.Path.Combine(dumpDir, filename);
            }

            var lines = new List<string>();
            
            // CSV Header (always include timestamps, direction last)
            lines.Add("Parameter Path;Type;Value;First Seen;Last Update;Update Count;Direction");

            // Collect all parameters
            var allParams = new List<(ParameterData param, string direction)>();
            
            if (includeIncoming)
            {
                var incoming = GetIncomingParameters();
                Log($"[Dump] Collected {incoming.Count} incoming parameters");
                foreach (var param in incoming.Values)
                {
                    allParams.Add((param, "IN"));
                }
            }

            if (includeOutgoing)
            {
                var outgoing = GetOutgoingParameters();
                Log($"[Dump] Collected {outgoing.Count} outgoing parameters");
                foreach (var param in outgoing.Values)
                {
                    allParams.Add((param, "OUT"));
                }
            }
            
            Log($"[Dump] Total parameters collected: {allParams.Count}");

            // Sort parameters based on settings
            var sortedParams = SortParameters(allParams);

            // Write sorted parameters to CSV
            foreach (var (param, direction) in sortedParams)
            {
                var valueStr = param.Value?.ToString()?.Replace(";", ",") ?? "null";
                lines.Add($"{param.Path};{param.Type};{valueStr};{param.FirstSeen:yyyy-MM-dd HH:mm:ss};{param.LastUpdate:yyyy-MM-dd HH:mm:ss};{param.UpdateCount};{direction}");
            }

            var totalParams = sortedParams.Count();
            await System.IO.File.WriteAllLinesAsync(filepath, lines);
            
            Log($"Dumped {totalParams} parameters to: {filepath}");

            SetVariableValue(DebugVariable.LastDumpPath, filepath);
            TriggerEvent(DebugEvent.OnDumpComplete);
            
            ChangeState(DebugState.Idle);
            return filepath;
        }
        catch (Exception ex)
        {
            Log($"Error: Failed to dump parameters: {ex.Message}");
            ChangeState(DebugState.Idle);
            throw;
        }
    }

    private IEnumerable<(ParameterData param, string direction)> SortParameters(List<(ParameterData param, string direction)> parameters)
    {
        var sortColumn = GetSortColumn();
        var sortDirection = GetSortDirection();

        IOrderedEnumerable<(ParameterData param, string direction)> sorted = sortColumn switch
        {
            CsvSortBy.ParameterPath => parameters.OrderBy(p => p.param.Path),
            CsvSortBy.ParameterName => parameters.OrderBy(p => p.param.Path.Split('/').LastOrDefault() ?? p.param.Path),
            CsvSortBy.Type => parameters.OrderBy(p => p.param.Type),
            CsvSortBy.Value => parameters.OrderBy(p => p.param.Value?.ToString() ?? string.Empty),
            CsvSortBy.FirstSeen => parameters.OrderBy(p => p.param.FirstSeen),
            CsvSortBy.LastUpdate => parameters.OrderBy(p => p.param.LastUpdate),
            CsvSortBy.UpdateCount => parameters.OrderBy(p => p.param.UpdateCount),
            CsvSortBy.Direction => parameters.OrderBy(p => p.direction),
            _ => parameters.OrderBy(p => p.param.Path)
        };

        return sortDirection == CsvSortDirection.Descending 
            ? sorted.Reverse() 
            : sorted;
    }

    public void ClearTracking()
    {
        var inCount = _incomingTracker?.UniqueParameters ?? 0;
        var outCount = _outgoingTracker?.UniqueParameters ?? 0;
        
        _incomingTracker?.Clear();
        _outgoingTracker?.Clear();
        
        Log($"Cleared tracking: {inCount} incoming, {outCount} outgoing");
        UpdateCounts();
        TriggerEvent(DebugEvent.OnTrackingCleared);
    }

    private void UpdateCounts()
    {
        var inCount = _incomingTracker?.GetAllParameters().Count ?? 0;
        var outCount = _outgoingTracker?.GetAllParameters().Count ?? 0;
        var total = inCount + outCount;

        SetVariableValue(DebugVariable.IncomingCount, inCount);
        SetVariableValue(DebugVariable.OutgoingCount, outCount);
        SetVariableValue(DebugVariable.TotalCount, total);

        // Use SendParameterSafe to avoid OSC connection issues during auto-start
        this.SendParameterSafe(DebugParameter.IncomingCount, inCount);
        this.SendParameterSafe(DebugParameter.OutgoingCount, outCount);
        this.SendParameterSafe(DebugParameter.TotalCount, total);
    }

    public Dictionary<string, ParameterData> GetIncomingParameters()
    {
        return _incomingTracker?.GetAllParameters() ?? new Dictionary<string, ParameterData>();
    }

    public Dictionary<string, ParameterData> GetOutgoingParameters()
    {
        return _outgoingTracker?.GetAllParameters() ?? new Dictionary<string, ParameterData>();
    }

    // Settings accessors
    private string GetDumpDirectory()
    {
        var dir = GetSettingValue<string>(DebugSetting.DumpDirectory);
        if (string.IsNullOrEmpty(dir))
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dir = System.IO.Path.Combine(appDataPath, "VRCOSC", "Dumps", "Debug");
        }
        return dir;
    }

    private CsvSortBy GetSortColumn() => GetSettingValue<CsvSortBy>(DebugSetting.SortBy);
    private CsvSortDirection GetSortDirection() => GetSettingValue<CsvSortDirection>(DebugSetting.SortDirection);
    
    // CUSTOM TRACKING ACCESSORS - DISABLED
    // private bool UseVrcoscTracking() => GetSettingValue<bool>(DebugSetting.UseVrcoscTracking);
    // private bool IsTrackAvatarOnly() => GetSettingValue<bool>(DebugSetting.TrackAvatarOnly);
    // private bool IsAutoTrackIncoming() => GetSettingValue<bool>(DebugSetting.AutoTrackIncoming);
    // private bool IsAutoTrackOutgoing() => GetSettingValue<bool>(DebugSetting.AutoTrackOutgoing);
    // private int GetMaxParameters() => GetSettingValue<int>(DebugSetting.MaxParameters);
    private bool IsLoggingEnabled() => GetSettingValue<bool>(DebugSetting.LogParameterUpdates);

    private enum DebugSetting
    {
        DumpDirectory,
        SortBy,
        SortDirection,
        // UseVrcoscTracking,     // Disabled - always using VRCOSC tracking
        // TrackAvatarOnly,       // Disabled - custom tracking not active
        // AutoTrackIncoming,     // Disabled - custom tracking not active
        // AutoTrackOutgoing,     // Disabled - custom tracking not active
        // MaxParameters,         // Disabled - custom tracking not active
        LogParameterUpdates,
        AutoStartModules
    }

    private enum CsvSortBy
    {
        ParameterPath,
        ParameterName,
        Type,
        Value,
        FirstSeen,
        LastUpdate,
        UpdateCount,
        Direction
    }

    private enum CsvSortDirection
    {
        Ascending,
        Descending
    }

    private enum DebugParameter
    {
        DumpNow,
        ClearTracking,
        IncomingCount,
        OutgoingCount,
        TotalCount
    }

    private enum DebugVariable
    {
        IncomingCount,
        OutgoingCount,
        TotalCount,
        LastDumpPath
    }

    private enum DebugState
    {
        Idle,
        Dumping
    }

    private enum DebugEvent
    {
        OnDumpComplete,
        OnTrackingCleared
    }
}
