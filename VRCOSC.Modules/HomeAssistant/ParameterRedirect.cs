// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// The parameter redirect list: one row aliases an arbitrary avatar parameter to a Home Assistant entity.

using Newtonsoft.Json;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.Utils;
using Bluscream.Modules.HomeAssistant.UI;

namespace Bluscream.Modules.HomeAssistant;

/// <summary>
/// How a redirect row converts between the avatar parameter and the entity. The first four go
/// OSC to Home Assistant; the State* members go the other way (entity state to the source parameter).
/// </summary>
public enum RedirectConversion
{
    /// <summary>Pick the conversion from the received value's type: bool = BoolToOnOff, float = FloatToLevel, int = IntToValue.</summary>
    Passthrough,

    /// <summary>true/false turns the entity on or off (lock/unlock, open/close, press, start/return for the odd domains).</summary>
    BoolToOnOff,

    /// <summary>A float in Min..Max maps to the entity's level: light brightness, fan percentage, cover position, number value, volume, temperature, humidity.</summary>
    FloatToLevel,

    /// <summary>An int selects an option by index (select/input_select) or is sent as the raw level (brightness 0..255, percentage, position, number value).</summary>
    IntToValue,

    /// <summary>Entity state to the source parameter as a bool (on/open/locked/playing = true).</summary>
    StateToBool,

    /// <summary>Entity level to the source parameter as a float scaled into Min..Max; entities without a level send their numeric state as-is.</summary>
    StateToFloat,

    /// <summary>Entity level, option index or numeric state to the source parameter as an int.</summary>
    StateToInt
}

public class ParameterRedirectListModuleSetting : ListModuleSetting<ParameterRedirect>
{
    public ParameterRedirectListModuleSetting()
        : base("Parameter Redirects", "Alias any avatar parameter to any entity without renaming the parameter. Source is the parameter address as VRChat sends it (e.g. HomeAssistant/fan/desk_socket_fan), Target the entity id (e.g. switch.desk_socket). Min/Max is the float range of the source parameter; Invert flips bools and levels.", typeof(ParameterRedirectListModuleSettingView), [])
    {
    }

    protected override ParameterRedirect CreateItem() => new();
}

/// <summary>
/// One redirect row. JSON names are stable so saved profiles keep working across renames of the C# members.
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class ParameterRedirect : IEquatable<ParameterRedirect>
{
    [JsonProperty("enabled")]
    public Observable<bool> Enabled { get; set; } = new(true);

    [JsonProperty("source")]
    public Observable<string> Source { get; set; } = new(string.Empty);

    [JsonProperty("target")]
    public Observable<string> Target { get; set; } = new(string.Empty);

    [JsonProperty("conversion")]
    public Observable<RedirectConversion> Conversion { get; set; } = new(RedirectConversion.Passthrough);

    [JsonProperty("invert")]
    public Observable<bool> Invert { get; set; } = new(false);

    [JsonProperty("min")]
    public Observable<float> Min { get; set; } = new(0f);

    [JsonProperty("max")]
    public Observable<float> Max { get; set; } = new(1f);

    [JsonConstructor]
    public ParameterRedirect()
    {
    }

    public bool Equals(ParameterRedirect? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;

        return Enabled.Value == other.Enabled.Value
               && Source.Value == other.Source.Value
               && Target.Value == other.Target.Value
               && Conversion.Value == other.Conversion.Value
               && Invert.Value == other.Invert.Value
               && Min.Value.Equals(other.Min.Value)
               && Max.Value.Equals(other.Max.Value);
    }

    public override bool Equals(object? obj) => obj is ParameterRedirect other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Enabled.Value, Source.Value, Target.Value, Conversion.Value, Invert.Value, Min.Value, Max.Value);
}
