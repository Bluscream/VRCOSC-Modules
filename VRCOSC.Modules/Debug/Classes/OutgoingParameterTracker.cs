// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Bluscream.Modules.Debug;

public class OutgoingParameterTracker : ParameterTracker
{
    public event Action<string, string, object?>? OnParameterSent;

    public OutgoingParameterTracker(int maxParameters = 0, bool logUpdates = false)
        : base(maxParameters, logUpdates)
    {
    }

    public void ProcessParameter(string name, object value)
    {
        var paramType = value?.GetType().Name ?? "null";
        TrackParameter(name, paramType, value);
        OnParameterSent?.Invoke(name, paramType, value);
    }

    public void ProcessParameter(Enum lookup, object value, Dictionary<Enum, object> parametersDict)
    {
        // Try to get the parameter name from the lookup
        if (parametersDict.TryGetValue(lookup, out var paramObj))
        {
            // Extract the name using reflection
            var nameProperty = paramObj.GetType().GetProperty("Name");
            if (nameProperty != null)
            {
                var nameObservable = nameProperty.GetValue(paramObj);
                var valueProperty = nameObservable?.GetType().GetProperty("Value");
                if (valueProperty != null)
                {
                    var name = valueProperty.GetValue(nameObservable)?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        ProcessParameter(name, value);
                        return;
                    }
                }
            }
        }
        
        // Fallback to enum name if we can't extract parameter name
        ProcessParameter(lookup.ToString(), value);
    }
}
