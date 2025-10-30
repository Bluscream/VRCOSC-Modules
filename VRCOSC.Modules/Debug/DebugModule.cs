// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;

namespace Bluscream.Modules;

[ModuleTitle("Debug")]
[ModuleDescription("Debug tools for tracking and exporting OSC parameters")]
[ModuleType(ModuleType.Generic)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class DebugModule : VRCOSC.App.SDK.Modules.Module
{
    private readonly object _parametersLock = new();
    private readonly Dictionary<string, ParameterData> _incomingParameters = new();
    private readonly Dictionary<string, ParameterData> _outgoingParameters = new();
    
    private int _totalIncoming;
    private int _totalOutgoing;
    
    private MethodInfo? _baseSendParameterMethod;

    protected override void OnPreLoad()
    {
        // Dump settings
        CreateTextBox(DebugSetting.DumpDirectory, "Dump Directory", "Directory for parameter dumps (leave empty for 'dumps' folder in module directory)", string.Empty);
        CreateToggle(DebugSetting.TrackAvatarOnly, "Avatar Parameters Only", "Only track avatar parameters (ignore system OSC messages)", true);
        
        // Tracking settings
        CreateToggle(DebugSetting.AutoTrackIncoming, "Auto-Track Incoming", "Automatically track incoming parameters when module starts", true);
        CreateToggle(DebugSetting.AutoTrackOutgoing, "Auto-Track Outgoing", "Automatically track outgoing parameters when module starts", true);
        CreateSlider(DebugSetting.MaxParameters, "Max Parameters", "Maximum parameters to track (0 = unlimited)", 0, 0, 10000, 100);
        
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
        var incomingCountRef = CreateVariable<int>(DebugVariable.IncomingCount, "Incoming Count");
        var outgoingCountRef = CreateVariable<int>(DebugVariable.OutgoingCount, "Outgoing Count");
        var totalCountRef = CreateVariable<int>(DebugVariable.TotalCount, "Total Count");
        var lastDumpPathRef = CreateVariable<string>(DebugVariable.LastDumpPath, "Last Dump Path");

        // ChatBox states
        CreateState(DebugState.Idle, "Idle", "Debug\nTracking: {0} params", new[] { totalCountRef });
        CreateState(DebugState.Dumping, "Dumping", "Dumping {0} params...", new[] { totalCountRef });

        // ChatBox events
        CreateEvent(DebugEvent.OnDumpComplete, "Dump Complete", "Dumped to: {0}", new[] { lastDumpPathRef });
        CreateEvent(DebugEvent.OnTrackingCleared, "Tracking Cleared", "Cleared all tracked parameters");

        ChangeState(DebugState.Idle);
    }

    protected override Task<bool> OnModuleStart()
    {
        // Cache the base SendParameter method for reflection-based interception
        _baseSendParameterMethod = typeof(VRCOSC.App.SDK.Modules.Module).GetMethod("SendParameter", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(string), typeof(object) }, null);
        
        Log("Debug module started");
        UpdateCounts();
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        Log($"Stopped tracking. Final counts: {_incomingParameters.Count} incoming, {_outgoingParameters.Count} outgoing");
        return Task.CompletedTask;
    }

    protected override void OnAnyParameterReceived(VRChatParameter parameter)
    {
        if (!IsAutoTrackIncoming()) return;
        
        var paramPath = parameter.Name;
        var paramValue = parameter.Value;
        var paramType = paramValue?.GetType().Name ?? "null";

        TrackParameter(_incomingParameters, paramPath, paramType, paramValue);
        _totalIncoming++;

        if (IsLoggingEnabled())
        {
            Log($"INCOMING: {paramPath} ({paramType}) = {paramValue}");
        }
        
        UpdateCounts();
    }
    
    // Override SendParameter to track outgoing parameters
    protected new void SendParameter(string name, object value)
    {
        // Track outgoing if enabled
        if (IsAutoTrackOutgoing())
        {
            var paramType = value?.GetType().Name ?? "null";
            TrackParameter(_outgoingParameters, name, paramType, value);
            _totalOutgoing++;

            if (IsLoggingEnabled())
            {
                Log($"OUTGOING: {name} ({paramType}) = {value}");
            }
            
            UpdateCounts();
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
            if (parametersDict != null && parametersDict.TryGetValue(lookup, out var moduleParameter))
            {
                SendParameter(moduleParameter.Name.Value, value);
                return;
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

    private void TrackParameter(Dictionary<string, ParameterData> dictionary, string path, string type, object? value)
    {
        lock (_parametersLock)
        {
            var maxParams = GetMaxParameters();
            if (maxParams > 0 && dictionary.Count >= maxParams && !dictionary.ContainsKey(path))
            {
                Log($"Warning: Max parameter limit ({maxParams}) reached, skipping: {path}");
                return;
            }

            if (dictionary.ContainsKey(path))
            {
                dictionary[path] = dictionary[path] with 
                { 
                    Value = value, 
                    LastUpdate = DateTime.Now,
                    UpdateCount = dictionary[path].UpdateCount + 1
                };
            }
            else
            {
                dictionary[path] = new ParameterData(path, type, value, DateTime.Now, 1);
            }
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
            
            // CSV Header (always include timestamps)
            lines.Add("Direction;Parameter Path;Type;Value;First Seen;Last Update;Update Count");

            lock (_parametersLock)
            {
                if (includeIncoming)
                {
                    foreach (var param in _incomingParameters.Values)
                    {
                        lines.Add(FormatParameterCsv("Incoming", param));
                    }
                }

                if (includeOutgoing)
                {
                    foreach (var param in _outgoingParameters.Values)
                    {
                        lines.Add(FormatParameterCsv("Outgoing", param));
                    }
                }
            }

            await System.IO.File.WriteAllLinesAsync(filepath, lines);
            
            var totalParams = (includeIncoming ? _incomingParameters.Count : 0) + (includeOutgoing ? _outgoingParameters.Count : 0);
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

    private string FormatParameterCsv(string direction, ParameterData param)
    {
        var valueStr = param.Value?.ToString()?.Replace(";", ",") ?? "null";
        return $"{direction};{param.Path};{param.Type};{valueStr};{param.FirstSeen:yyyy-MM-dd HH:mm:ss};{param.LastUpdate:yyyy-MM-dd HH:mm:ss};{param.UpdateCount}";
    }

    public void ClearTracking()
    {
        lock (_parametersLock)
        {
            var inCount = _incomingParameters.Count;
            var outCount = _outgoingParameters.Count;
            
            _incomingParameters.Clear();
            _outgoingParameters.Clear();
            _totalIncoming = 0;
            _totalOutgoing = 0;
            
            Log($"Cleared tracking: {inCount} incoming, {outCount} outgoing");
            UpdateCounts();
            TriggerEvent(DebugEvent.OnTrackingCleared);
        }
    }

    private void UpdateCounts()
    {
        lock (_parametersLock)
        {
            var inCount = _incomingParameters.Count;
            var outCount = _outgoingParameters.Count;
            var total = inCount + outCount;

            SetVariableValue(DebugVariable.IncomingCount, inCount);
            SetVariableValue(DebugVariable.OutgoingCount, outCount);
            SetVariableValue(DebugVariable.TotalCount, total);

            SendParameter(DebugParameter.IncomingCount, inCount);
            SendParameter(DebugParameter.OutgoingCount, outCount);
            SendParameter(DebugParameter.TotalCount, total);
        }
    }

    public Dictionary<string, ParameterData> GetIncomingParameters()
    {
        lock (_parametersLock)
        {
            return new Dictionary<string, ParameterData>(_incomingParameters);
        }
    }

    public Dictionary<string, ParameterData> GetOutgoingParameters()
    {
        lock (_parametersLock)
        {
            return new Dictionary<string, ParameterData>(_outgoingParameters);
        }
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

    private bool IsTrackAvatarOnly() => GetSettingValue<bool>(DebugSetting.TrackAvatarOnly);
    private bool IsAutoTrackIncoming() => GetSettingValue<bool>(DebugSetting.AutoTrackIncoming);
    private bool IsAutoTrackOutgoing() => GetSettingValue<bool>(DebugSetting.AutoTrackOutgoing);
    private int GetMaxParameters() => GetSettingValue<int>(DebugSetting.MaxParameters);
    private bool IsLoggingEnabled() => GetSettingValue<bool>(DebugSetting.LogParameterUpdates);

    private enum DebugSetting
    {
        DumpDirectory,
        TrackAvatarOnly,
        AutoTrackIncoming,
        AutoTrackOutgoing,
        MaxParameters,
        LogParameterUpdates
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

    public record ParameterData(
        string Path,
        string Type,
        object? Value,
        DateTime FirstSeen,
        int UpdateCount,
        DateTime LastUpdate
    )
    {
        public ParameterData(string path, string type, object? value, DateTime timestamp, int updateCount)
            : this(path, type, value, timestamp, updateCount, timestamp)
        {
        }
    }
}
