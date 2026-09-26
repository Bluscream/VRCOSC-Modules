// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// The editable status list: one row per status with text, group and cycle flag.

using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using Bluscream.Modules.Status.UI;

namespace Bluscream.Modules.Status;

public class StatusListModuleSetting : ListModuleSetting<StatusEntry>
{
    public StatusListModuleSetting()
        : base("Statuses", "Your status texts. Tick \"cycle\" on the ones that should take turns; group names let you enable or disable sets of statuses at once.", typeof(StatusListModuleSettingView), [])
    {
    }

    protected override StatusEntry CreateItem() => new();
}

/// <summary>
/// One status row. JSON names are stable so the converter can round-trip the list; they mirror
/// MagicChatbox's StatusList.json fields where a counterpart exists.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class StatusEntry : IEquatable<StatusEntry>
{
    [JsonProperty("text")]
    public Observable<string> Text { get; set; } = new("New status");

    [JsonProperty("group")]
    public Observable<string> Group { get; set; } = new(string.Empty);

    [JsonProperty("cycle")]
    public Observable<bool> UseInCycle { get; set; } = new(true);

    [JsonConstructor]
    public StatusEntry()
    {
    }

    public bool Equals(StatusEntry? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Text.Value == other.Text.Value && Group.Value == other.Group.Value && UseInCycle.Value == other.UseInCycle.Value;
    }

    public override bool Equals(object? obj) => obj is StatusEntry other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Text.Value, Group.Value, UseInCycle.Value);
}
