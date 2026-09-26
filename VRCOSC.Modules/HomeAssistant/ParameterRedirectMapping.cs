// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Pure conversion between an avatar parameter value and a Home Assistant service call (and back).
// No I/O here so the domain table can be read and tested in one place.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Bluscream.Modules.HomeAssistant;

/// <summary>A resolved Home Assistant service call: <c>domain.service</c> with extra service data (entity_id is added by the client).</summary>
public sealed record HaServiceCall(string Domain, string Service, Dictionary<string, object>? Data = null);

public static class ParameterRedirectMapping
{
    /// <summary>Domains a redirect can target. Anything else is rejected once per row.</summary>
    public static readonly IReadOnlyList<string> KnownDomains = new[]
    {
        "switch", "light", "fan", "input_boolean", "automation", "script", "media_player", "climate",
        "humidifier", "siren", "remote", "water_heater", "camera", "lock", "cover", "valve", "vacuum",
        "scene", "button", "input_button", "number", "input_number", "select", "input_select"
    };

    private static readonly HashSet<string> OnStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "on", "open", "opening", "locked", "playing", "home", "active", "cleaning", "running",
        "heat", "cool", "heat_cool", "auto", "dry", "fan_only"
    };

    /// <summary>Splits <c>domain.object_id</c>; null when the id is not a valid entity id.</summary>
    public static string? DomainOf(string entityId)
    {
        var dot = entityId.IndexOf('.');
        if (dot <= 0 || dot == entityId.Length - 1) return null;

        var domain = entityId[..dot];
        var objectId = entityId[(dot + 1)..];
        return domain.All(IsIdChar) && objectId.All(IsIdChar) ? domain : null;
    }

    private static bool IsIdChar(char c) => c == '_' || char.IsAsciiDigit(c) || char.IsAsciiLetterLower(c);

    public static bool IsOscToHa(RedirectConversion conversion) => conversion is RedirectConversion.Passthrough or RedirectConversion.BoolToOnOff or RedirectConversion.FloatToLevel or RedirectConversion.IntToValue;

    /// <summary>
    /// Builds the service call for a received value. Returns null when nothing should be sent
    /// (e.g. scene/button receiving false, or a select whose options are unknown).
    /// </summary>
    public static HaServiceCall? ToServiceCall(ParameterRedirect row, string domain, object value, IReadOnlyDictionary<string, object?>? attributes)
    {
        var conversion = row.Conversion.Value;
        if (conversion == RedirectConversion.Passthrough)
        {
            conversion = value switch
            {
                bool => RedirectConversion.BoolToOnOff,
                int => RedirectConversion.IntToValue,
                _ => RedirectConversion.FloatToLevel
            };
        }

        return conversion switch
        {
            RedirectConversion.BoolToOnOff => OnOffCall(domain, AsBool(value) ^ row.Invert.Value),
            RedirectConversion.FloatToLevel => LevelCall(domain, Normalize(AsFloat(value), row), attributes),
            RedirectConversion.IntToValue => IntCall(domain, AsInt(value), attributes),
            _ => null
        };
    }

    /// <summary>Converts an entity state into the value to write to the source parameter, or null when the state does not convert.</summary>
    public static object? ToParameterValue(ParameterRedirect row, string domain, string state, IReadOnlyDictionary<string, object?>? attributes)
    {
        switch (row.Conversion.Value)
        {
            case RedirectConversion.StateToBool:
                return OnStates.Contains(state) ^ row.Invert.Value;

            case RedirectConversion.StateToFloat:
            {
                var level = LevelOf(domain, state, attributes);
                if (level is null) return null;

                var n = row.Invert.Value ? 1f - level.Value : level.Value;
                return row.Min.Value + n * (row.Max.Value - row.Min.Value);
            }

            case RedirectConversion.StateToInt:
                return IntOf(domain, state, attributes);

            default:
                return null;
        }
    }

    #region OSC -> HA

    private static HaServiceCall? OnOffCall(string domain, bool on)
    {
        return domain switch
        {
            "lock" => new HaServiceCall(domain, on ? "lock" : "unlock"),
            "cover" => new HaServiceCall(domain, on ? "open_cover" : "close_cover"),
            "valve" => new HaServiceCall(domain, on ? "open_valve" : "close_valve"),
            "vacuum" => new HaServiceCall(domain, on ? "start" : "return_to_base"),
            "scene" => on ? new HaServiceCall(domain, "turn_on") : null,
            "button" or "input_button" => on ? new HaServiceCall(domain, "press") : null,
            "select" or "input_select" or "number" or "input_number" => null,
            _ => new HaServiceCall(domain, on ? "turn_on" : "turn_off")
        };
    }

    private static HaServiceCall? LevelCall(string domain, float n, IReadOnlyDictionary<string, object?>? attributes)
    {
        return domain switch
        {
            "light" => n <= 0f ? new HaServiceCall(domain, "turn_off") : new HaServiceCall(domain, "turn_on", Data("brightness", (int)Math.Round(n * 255f))),
            "fan" => new HaServiceCall(domain, "set_percentage", Data("percentage", (int)Math.Round(n * 100f))),
            "cover" => new HaServiceCall(domain, "set_cover_position", Data("position", (int)Math.Round(n * 100f))),
            "valve" => new HaServiceCall(domain, "set_valve_position", Data("position", (int)Math.Round(n * 100f))),
            "media_player" => new HaServiceCall(domain, "volume_set", Data("volume_level", n)),
            "humidifier" => new HaServiceCall(domain, "set_humidity", Data("humidity", (int)Math.Round(n * 100f))),
            "climate" => new HaServiceCall(domain, "set_temperature", Data("temperature", Lerp(n, Attr(attributes, "min_temp") ?? 7d, Attr(attributes, "max_temp") ?? 35d))),
            "number" or "input_number" => new HaServiceCall(domain, "set_value", Data("value", Lerp(n, Attr(attributes, "min") ?? 0d, Attr(attributes, "max") ?? 100d))),
            _ => OnOffCall(domain, n >= 0.5f)
        };
    }

    private static HaServiceCall? IntCall(string domain, int i, IReadOnlyDictionary<string, object?>? attributes)
    {
        switch (domain)
        {
            case "select":
            case "input_select":
            {
                var options = Options(attributes);
                if (options.Count == 0) return null;

                return new HaServiceCall(domain, "select_option", Data("option", options[Math.Clamp(i, 0, options.Count - 1)]));
            }

            case "number":
            case "input_number":
                return new HaServiceCall(domain, "set_value", Data("value", i));

            case "light":
                return i <= 0 ? new HaServiceCall(domain, "turn_off") : new HaServiceCall(domain, "turn_on", Data("brightness", Math.Clamp(i, 1, 255)));

            case "fan":
                return new HaServiceCall(domain, "set_percentage", Data("percentage", Math.Clamp(i, 0, 100)));

            case "cover":
                return new HaServiceCall(domain, "set_cover_position", Data("position", Math.Clamp(i, 0, 100)));

            case "valve":
                return new HaServiceCall(domain, "set_valve_position", Data("position", Math.Clamp(i, 0, 100)));

            case "media_player":
                return new HaServiceCall(domain, "volume_set", Data("volume_level", Math.Clamp(i, 0, 100) / 100f));

            case "humidifier":
                return new HaServiceCall(domain, "set_humidity", Data("humidity", Math.Clamp(i, 0, 100)));

            case "climate":
                return new HaServiceCall(domain, "set_temperature", Data("temperature", i));

            default:
                return OnOffCall(domain, i != 0);
        }
    }

    #endregion

    #region HA -> OSC

    /// <summary>The entity's level as 0..1, or its numeric state unscaled when the domain has no notion of level.</summary>
    private static float? LevelOf(string domain, string state, IReadOnlyDictionary<string, object?>? attributes)
    {
        double? level = domain switch
        {
            "light" => OnStates.Contains(state) ? (Attr(attributes, "brightness") ?? 255d) / 255d : 0d,
            "fan" => Attr(attributes, "percentage") / 100d,
            "cover" or "valve" => Attr(attributes, "current_position") / 100d,
            "media_player" => Attr(attributes, "volume_level"),
            "humidifier" => Attr(attributes, "humidity") / 100d,
            "climate" => Unlerp(Attr(attributes, "temperature"), Attr(attributes, "min_temp") ?? 7d, Attr(attributes, "max_temp") ?? 35d),
            "number" or "input_number" => Unlerp(ParseDouble(state), Attr(attributes, "min") ?? 0d, Attr(attributes, "max") ?? 100d),
            _ => ParseDouble(state)
        };

        return level is null ? null : (float)level.Value;
    }

    private static int? IntOf(string domain, string state, IReadOnlyDictionary<string, object?>? attributes)
    {
        double? value = domain switch
        {
            "select" or "input_select" => Options(attributes).IndexOf(state) is var idx && idx >= 0 ? idx : null,
            "light" => OnStates.Contains(state) ? Attr(attributes, "brightness") ?? 255d : 0d,
            "fan" => Attr(attributes, "percentage"),
            "cover" or "valve" => Attr(attributes, "current_position"),
            "media_player" => Attr(attributes, "volume_level") * 100d,
            "humidifier" => Attr(attributes, "humidity"),
            "climate" => Attr(attributes, "temperature"),
            _ => ParseDouble(state) ?? (OnStates.Contains(state) ? 1d : 0d)
        };

        return value is null ? null : (int)Math.Round(value.Value);
    }

    #endregion

    #region helpers

    private static Dictionary<string, object> Data(string key, object value) => new() { [key] = value };

    private static bool AsBool(object value) => value switch
    {
        bool b => b,
        int i => i != 0,
        float f => f >= 0.5f,
        _ => false
    };

    private static float AsFloat(object value) => value switch
    {
        float f => f,
        int i => i,
        bool b => b ? 1f : 0f,
        _ => 0f
    };

    private static int AsInt(object value) => value switch
    {
        int i => i,
        float f => (int)Math.Round(f),
        bool b => b ? 1 : 0,
        _ => 0
    };

    /// <summary>Maps the row's Min..Max onto 0..1 (clamped), then applies Invert.</summary>
    private static float Normalize(float value, ParameterRedirect row)
    {
        var range = row.Max.Value - row.Min.Value;
        var n = Math.Abs(range) < float.Epsilon ? (value >= row.Max.Value ? 1f : 0f) : (value - row.Min.Value) / range;
        n = Math.Clamp(n, 0f, 1f);
        return row.Invert.Value ? 1f - n : n;
    }

    private static double Lerp(float n, double min, double max) => Math.Round(min + n * (max - min), 2);

    private static double? Unlerp(double? value, double min, double max)
    {
        if (value is null) return null;

        var range = max - min;
        return Math.Abs(range) < double.Epsilon ? 0d : Math.Clamp((value.Value - min) / range, 0d, 1d);
    }

    private static double? ParseDouble(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    /// <summary>Reads a numeric attribute; attributes arrive as JsonElement (REST), string (WebSocket) or boxed numbers.</summary>
    private static double? Attr(IReadOnlyDictionary<string, object?>? attributes, string key)
    {
        if (attributes is null || !attributes.TryGetValue(key, out var raw) || raw is null) return null;

        return raw switch
        {
            JsonElement { ValueKind: JsonValueKind.Number } el => el.GetDouble(),
            JsonElement => null,
            IConvertible c => ParseDouble(c.ToString(CultureInfo.InvariantCulture)),
            _ => null
        };
    }

    /// <summary>The select's option list; the WebSocket path stringifies the array so it is parsed back.</summary>
    private static List<string> Options(IReadOnlyDictionary<string, object?>? attributes)
    {
        if (attributes is null || !attributes.TryGetValue("options", out var raw) || raw is null) return [];

        switch (raw)
        {
            case JsonElement { ValueKind: JsonValueKind.Array } el:
                return el.EnumerateArray().Select(o => o.ToString()).ToList();

            case string s when s.TrimStart().StartsWith('['):
                try
                {
                    return JsonSerializer.Deserialize<List<string>>(s) ?? [];
                }
                catch (JsonException)
                {
                    return [];
                }

            case IEnumerable<object?> list:
                return list.Select(o => o?.ToString() ?? string.Empty).ToList();

            default:
                return [];
        }
    }

    #endregion
}
