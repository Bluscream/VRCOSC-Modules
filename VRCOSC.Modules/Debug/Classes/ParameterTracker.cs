// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Bluscream.Modules.Debug;

public abstract class ParameterTracker
{
    private readonly object _lock = new();
    private readonly Dictionary<string, ParameterData> _parameters = new();
    
    protected int MaxParameters { get; set; }
    protected bool LogUpdates { get; set; }
    
    public int TotalUpdates { get; private set; }
    public int UniqueParameters => _parameters.Count;

    // Events
    public event Action<ParameterData>? OnParameterTracked;
    public event Action<string>? OnMaxLimitReached;
    public event Action? OnCleared;

    protected ParameterTracker(int maxParameters = 0, bool logUpdates = false)
    {
        MaxParameters = maxParameters;
        LogUpdates = logUpdates;
    }

    public void TrackParameter(string path, string type, object? value)
    {
        lock (_lock)
        {
            if (MaxParameters > 0 && _parameters.Count >= MaxParameters && !_parameters.ContainsKey(path))
            {
                OnMaxLimitReached?.Invoke(path);
                return;
            }

            ParameterData paramData;
            
            if (_parameters.ContainsKey(path))
            {
                var existing = _parameters[path];
                paramData = existing with 
                { 
                    Value = value, 
                    LastUpdate = DateTime.Now,
                    UpdateCount = existing.UpdateCount + 1
                };
            }
            else
            {
                paramData = new ParameterData(path, type, value, DateTime.Now, 1);
            }

            _parameters[path] = paramData;
            TotalUpdates++;
            
            OnParameterTracked?.Invoke(paramData);
        }
    }

    public Dictionary<string, ParameterData> GetAllParameters()
    {
        lock (_lock)
        {
            return new Dictionary<string, ParameterData>(_parameters);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _parameters.Clear();
            TotalUpdates = 0;
            OnCleared?.Invoke();
        }
    }

    public List<string> ExportToCsvLines(string direction, bool includeHeader = false)
    {
        var lines = new List<string>();
        
        if (includeHeader)
        {
            lines.Add("Direction;Parameter Path;Type;Value;First Seen;Last Update;Update Count");
        }

        lock (_lock)
        {
            foreach (var param in _parameters.Values)
            {
                var valueStr = param.Value?.ToString()?.Replace(";", ",") ?? "null";
                lines.Add($"{direction};{param.Path};{param.Type};{valueStr};{param.FirstSeen:yyyy-MM-dd HH:mm:ss};{param.LastUpdate:yyyy-MM-dd HH:mm:ss};{param.UpdateCount}");
            }
        }

        return lines;
    }

    public void UpdateSettings(int maxParameters, bool logUpdates)
    {
        MaxParameters = maxParameters;
        LogUpdates = logUpdates;
    }
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
