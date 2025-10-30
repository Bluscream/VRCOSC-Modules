// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.Debug;

public class IncomingParameterTracker : ParameterTracker
{
    public bool TrackAvatarOnly { get; set; }
    
    public event Action<string, string, object?>? OnParameterReceived;

    public IncomingParameterTracker(int maxParameters = 0, bool logUpdates = false, bool trackAvatarOnly = true)
        : base(maxParameters, logUpdates)
    {
        TrackAvatarOnly = trackAvatarOnly;
    }

    public void ProcessParameter(VRChatParameter parameter)
    {
        // Skip non-avatar parameters if filter is enabled
        if (TrackAvatarOnly && !IsAvatarParameter(parameter.Name))
        {
            return;
        }

        var paramPath = parameter.Name;
        var paramValue = parameter.Value;
        var paramType = paramValue?.GetType().Name ?? "null";

        TrackParameter(paramPath, paramType, paramValue);
        OnParameterReceived?.Invoke(paramPath, paramType, paramValue);
    }

    private bool IsAvatarParameter(string name)
    {
        // Avatar parameters don't typically start with VRCOSC or system prefixes
        return !name.StartsWith("VRCOSC/", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Tracking/", StringComparison.OrdinalIgnoreCase);
    }

    public void UpdateSettings(int maxParameters, bool logUpdates, bool trackAvatarOnly)
    {
        base.UpdateSettings(maxParameters, logUpdates);
        TrackAvatarOnly = trackAvatarOnly;
    }
}
