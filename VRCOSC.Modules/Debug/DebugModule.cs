// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bluscream.Modules.Debug;
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
    // CUSTOM TRACKING FIELDS - DISABLED (keeping for future use)
    // private IncomingParameterTracker? _incomingTracker;
    // private OutgoingParameterTracker? _outgoingTracker;
    // private MethodInfo? _baseSendParameterMethod;
    // private bool _useVrcoscTracking;

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
    }

    protected override Task<bool> OnModuleStart()
    {
        // Try to initialize VRCOSC tracking reflection
        if (DebugReflection.Initialize())
        {
            Log("Using VRCOSC's built-in parameter tracking");
        }
        else
        {
            Log("Error: Failed to initialize VRCOSC tracking reflection");
            return Task.FromResult(false);
        }

        // CUSTOM TRACKING DISABLED - Only using DebugReflection
        /*
        if (!_useVrcoscTracking)
        {
            // Initialize custom trackers
            _incomingTracker = new IncomingParameterTracker(
                GetMaxParameters(),
                IsLoggingEnabled(),
                IsTrackAvatarOnly()
            );
            
            _outgoingTracker = new OutgoingParameterTracker(
                GetMaxParameters(),
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
            _baseSendParameterMethod = typeof(VRCOSC.App.SDK.Modules.Module).GetMethod("SendParameter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(string), typeof(object) }, null);
            
            Log("Using custom parameter tracking");
        }
        */
        
        Log("Debug module started");
        UpdateCounts();
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        var counts = DebugReflection.GetParameterCounts();
        Log($"Stopped tracking. Final counts: {counts.incoming} incoming, {counts.outgoing} outgoing");
        return Task.CompletedTask;
    }

    // CUSTOM TRACKING EVENT HANDLERS - DISABLED
    /*
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
        Log($"Warning: Max parameter limit ({GetMaxParameters()}) reached, skipping: {path}");
    }

    private void OnTrackingCleared()
    {
        UpdateCounts();
    }

    protected override void OnAnyParameterReceived(VRChatParameter parameter)
    {
        // Skip if using VRCOSC tracking (it handles everything)
        if (_useVrcoscTracking) return;
        
        if (!IsAutoTrackIncoming() || _incomingTracker == null) return;
        _incomingTracker.ProcessParameter(parameter);
    }
    
    // Override SendParameter to track outgoing parameters
    protected new void SendParameter(string name, object value)
    {
        // Track outgoing if enabled (skip if using VRCOSC tracking)
        if (!_useVrcoscTracking && IsAutoTrackOutgoing() && _outgoingTracker != null)
        {
            _outgoingTracker.ProcessParameter(name, value);
        }
        
        // Call base implementation using reflection
        _baseSendParameterMethod?.Invoke(this, new[] { name, value });
    }
    
    protected new void SendParameter(Enum lookup, object value)
    {
        // Get the parameter name from reflection to access internal Parameters dictionary
        var parametersField = typeof(VRCOSC.App.SDK.Modules.Module).GetField("Parameters", BindingFlags.Instance | BindingFlags.NonPublic);
        if (parametersField != null)
        {
            var parametersDict = (Dictionary<Enum, ModuleParameter>?)parametersField.GetValue(this);
            if (parametersDict != null)
            {
                // Track using the dictionary
                if (IsAutoTrackOutgoing() && _outgoingTracker != null)
                {
                    _outgoingTracker.ProcessParameter(lookup, value, new Dictionary<Enum, object>(parametersDict.ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)));
                }
                
                // Get parameter name and send
                if (parametersDict.TryGetValue(lookup, out var moduleParameter))
                {
                    SendParameter(moduleParameter.Name.Value, value);
                    return;
                }
            }
        }
        
        // Fallback to base if reflection fails
        _baseSendParameterMethod?.Invoke(this, new[] { lookup, value });
    }
    */

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


    public async Task<string> DumpParametersAsync(bool includeIncoming = true, bool includeOutgoing = true)
    {
        ChangeState(DebugState.Dumping);
        
        try
        {
            var dumpDir = GetDumpDirectory();
            if (!System.IO.Directory.Exists(dumpDir))
            {
                System.IO.Directory.CreateDirectory(dumpDir);
            }

            var timestamp = DateTime.Now.ToString("ddMMyyyy-HH-mm-ss");
            var filename = $"params_{timestamp}.csv";
            var filepath = System.IO.Path.Combine(dumpDir, filename);

            var lines = new List<string>();
            
            // CSV Header (always include timestamps, direction last)
            lines.Add("Parameter Path;Type;Value;First Seen;Last Update;Update Count;Direction");

            // Collect all parameters
            var allParams = new List<(ParameterData param, string direction)>();
            
            if (includeIncoming)
            {
                var incoming = GetIncomingParameters();
                foreach (var param in incoming.Values)
                {
                    allParams.Add((param, "IN"));
                }
            }

            if (includeOutgoing)
            {
                var outgoing = GetOutgoingParameters();
                foreach (var param in outgoing.Values)
                {
                    allParams.Add((param, "OUT"));
                }
            }

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
        Log("Warning: Cannot clear VRCOSC's built-in tracking (managed by VRCOSC).");
        
        // CUSTOM TRACKING CLEAR - DISABLED
        /*
        var inCount = _incomingTracker?.UniqueParameters ?? 0;
        var outCount = _outgoingTracker?.UniqueParameters ?? 0;
        
        _incomingTracker?.Clear();
        _outgoingTracker?.Clear();
        
        Log($"Cleared tracking: {inCount} incoming, {outCount} outgoing");
        UpdateCounts();
        TriggerEvent(DebugEvent.OnTrackingCleared);
        */
    }

    private void UpdateCounts()
    {
        var counts = DebugReflection.GetParameterCounts();
        var inCount = counts.incoming;
        var outCount = counts.outgoing;
        var total = inCount + outCount;

        SetVariableValue(DebugVariable.IncomingCount, inCount);
        SetVariableValue(DebugVariable.OutgoingCount, outCount);
        SetVariableValue(DebugVariable.TotalCount, total);

        // Use base.SendParameter to avoid recursive tracking
        base.SendParameter(DebugParameter.IncomingCount, inCount);
        base.SendParameter(DebugParameter.OutgoingCount, outCount);
        base.SendParameter(DebugParameter.TotalCount, total);
    }

    public Dictionary<string, ParameterData> GetIncomingParameters()
    {
        var vrcoscParams = DebugReflection.GetIncomingMessages();
        if (vrcoscParams != null)
        {
            // Convert to ParameterData format
            var now = DateTime.Now;
            return vrcoscParams.ToDictionary(
                kvp => kvp.Key,
                kvp => new ParameterData(kvp.Key, kvp.Value?.GetType().Name ?? "null", kvp.Value, now, 1)
            );
        }
        
        return new Dictionary<string, ParameterData>();
        
        // CUSTOM TRACKING - DISABLED
        // return _incomingTracker?.GetAllParameters() ?? new Dictionary<string, ParameterData>();
    }

    public Dictionary<string, ParameterData> GetOutgoingParameters()
    {
        var vrcoscParams = DebugReflection.GetOutgoingMessages();
        if (vrcoscParams != null)
        {
            // Convert to ParameterData format
            var now = DateTime.Now;
            return vrcoscParams.ToDictionary(
                kvp => kvp.Key,
                kvp => new ParameterData(kvp.Key, kvp.Value?.GetType().Name ?? "null", kvp.Value, now, 1)
            );
        }
        
        return new Dictionary<string, ParameterData>();
        
        // CUSTOM TRACKING - DISABLED
        // return _outgoingTracker?.GetAllParameters() ?? new Dictionary<string, ParameterData>();
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
        LogParameterUpdates
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
