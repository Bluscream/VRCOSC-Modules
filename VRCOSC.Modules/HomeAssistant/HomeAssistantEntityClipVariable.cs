// Copyright (c) VolcanicArts / Bluscream. Licensed under the GPL-3.0 License.

using System;
using System.Collections.Generic;
using VRCOSC.App.ChatBox.Clips.Variables;

namespace Bluscream.Modules.HomeAssistant;

public class HomeAssistantEntityClipVariable : ClipVariable
{
    public HomeAssistantEntityClipVariable()
    {
    }

    public HomeAssistantEntityClipVariable(ClipVariableReference reference)
        : base(reference)
    {
    }

    [ClipVariableOption("entity_id", "Entity ID", "HomeAssistant entity ID (e.g. sensor.bedroom_temperature, light.desk_lamp)")]
    public string EntityID { get; set; } = string.Empty;

    [ClipVariableOption("attribute", "Attribute", "Optional attribute name (e.g. temperature, brightness). Leave empty for main state.")]
    public string Attribute { get; set; } = string.Empty;

    [ClipVariableOption("append_unit", "Append Unit of Measurement", "Automatically append HomeAssistant's unit_of_measurement attribute (e.g. °C, %, W)")]
    public bool AppendUnit { get; set; } = true;

    [ClipVariableOption("unit", "Format / Suffix", "Optional custom format string or suffix (e.g. '{0}°C', 'W'). Use {0} as value placeholder.")]
    public string FormatString { get; set; } = "{0}";

    public override bool IsDefault() => base.IsDefault() && EntityID == string.Empty && Attribute == string.Empty && AppendUnit == true && FormatString == "{0}";

    public override HomeAssistantEntityClipVariable Clone()
    {
        var clone = (HomeAssistantEntityClipVariable)base.Clone();
        clone.EntityID = EntityID;
        clone.Attribute = Attribute;
        clone.AppendUnit = AppendUnit;
        clone.FormatString = FormatString;
        return clone;
    }

    protected override string Format(object value)
    {
        if (string.IsNullOrWhiteSpace(EntityID)) return string.Empty;
        if (value is not Dictionary<string, HAEntityStateSnapshot> stateMap) return string.Empty;

        var key = EntityID.Trim().ToLowerInvariant();
        if (!stateMap.TryGetValue(key, out var snapshot)) return string.Empty;

        string? valStr = null;
        if (!string.IsNullOrWhiteSpace(Attribute))
        {
            if (snapshot.Attributes != null && snapshot.Attributes.TryGetValue(Attribute.Trim(), out var attrVal) && attrVal != null)
            {
                valStr = attrVal.ToString();
            }
        }
        else
        {
            valStr = snapshot.State;
        }

        if (valStr is null) return string.Empty;

        if (AppendUnit && snapshot.Attributes != null)
        {
            string? unit = null;
            if (snapshot.Attributes.TryGetValue("unit_of_measurement", out var uObj) && uObj != null)
                unit = uObj.ToString();
            else if (snapshot.Attributes.TryGetValue("unit", out var uObj2) && uObj2 != null)
                unit = uObj2.ToString();

            if (!string.IsNullOrEmpty(unit))
            {
                valStr = $"{valStr}{unit}";
            }
        }

        if (string.IsNullOrEmpty(FormatString)) return valStr;

        try
        {
            if (FormatString.Contains("{0}"))
                return string.Format(FormatString, valStr);

            return $"{valStr}{FormatString}";
        }
        catch
        {
            return valStr;
        }
    }
}

public class HAEntityStateSnapshot
{
    public string State { get; set; } = string.Empty;
    public Dictionary<string, object?> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
