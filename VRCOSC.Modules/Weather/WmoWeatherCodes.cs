// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// WMO 4677 weather-code descriptions and emoji, shared by every weather source in the suite
// (Open-Meteo emits WMO codes natively; other providers map their own codes onto WMO first).

namespace Bluscream.Modules;

public static class WmoWeatherCodes
{
    /// <summary>Condition text plus a day and a night emoji for a WMO 4677 code, as used by Open-Meteo.</summary>
    public static (string Condition, string Day, string Night) Describe(int code) => code switch
    {
        0 => ("Clear", "☀️", "\U0001F319"),
        1 => ("Mostly clear", "\U0001F324️", "\U0001F319"),
        2 => ("Partly cloudy", "⛅", "☁️"),
        3 => ("Overcast", "☁️", "☁️"),
        45 or 48 => ("Fog", "\U0001F32B️", "\U0001F32B️"),
        51 or 53 or 55 => ("Drizzle", "\U0001F326️", "\U0001F327️"),
        56 or 57 => ("Freezing drizzle", "\U0001F328️", "\U0001F328️"),
        61 or 63 or 65 => ("Rain", "\U0001F327️", "\U0001F327️"),
        66 or 67 => ("Freezing rain", "\U0001F328️", "\U0001F328️"),
        71 or 73 or 75 or 77 => ("Snow", "\U0001F328️", "\U0001F328️"),
        80 or 81 or 82 => ("Rain showers", "\U0001F326️", "\U0001F327️"),
        85 or 86 => ("Snow showers", "\U0001F328️", "\U0001F328️"),
        95 => ("Thunderstorm", "⛈️", "⛈️"),
        96 or 99 => ("Thunderstorm with hail", "⛈️", "⛈️"),
        _ => ("Unknown", "\U0001F321️", "\U0001F321️")
    };

    /// <summary>Emoji for a WMO code, picking the day or night glyph.</summary>
    public static string Emoji(int code, bool isDay)
    {
        var (_, day, night) = Describe(code);
        return isDay ? day : night;
    }

    /// <summary>Eight-point compass label for a wind direction in degrees.</summary>
    public static string Compass(float degrees)
    {
        string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var index = (int)MathF.Round(((degrees % 360f) + 360f) % 360f / 45f) % 8;
        return points[index];
    }
}
