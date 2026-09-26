// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Weather from Open-Meteo (https://open-meteo.com): keyless, and it carries apparent
// temperature, wind and a WMO weather code, which the official (weatherapi.com) module
// does not expose. Complements rather than replaces the official module.

using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules;

[ModuleTitle("Open-Meteo Weather")]
[ModuleDescription("Temperature, feels-like, wind, humidity, condition and a weather emoji from Open-Meteo. No API key needed.")]
[ModuleType(ModuleType.Integrations)]
public class OpenMeteoWeatherModule : Module
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);

    private (double Lat, double Lon, string Name)? _location;
    private string _resolvedFor = string.Empty;
    private DateTime _lastFetch = DateTime.MinValue;
    private bool _fetching;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VRCOSC-BluscreamModules/1.0 (https://github.com/Bluscream/VRCOSC-Modules)");
        return client;
    }

    protected override void OnPreLoad()
    {
        CreateTextBox(WeatherSetting.Location, "Location",
            "City name (e.g. \"Berlin\" or \"Berlin, DE\"), or coordinates as \"lat, lon\" (e.g. \"52.52, 13.41\"). Geocoded through Open-Meteo.", string.Empty);

        RegisterParameter<int>(WeatherParameter.Code, "VRCOSC/Weather/Code", ParameterMode.Write, "Weather Code", "WMO weather code (0 clear ... 99 thunderstorm with hail)");
        RegisterParameter<float>(WeatherParameter.TempC, "VRCOSC/Weather/TempC", ParameterMode.Write, "Temperature C", "Air temperature in Celsius");
        RegisterParameter<float>(WeatherParameter.FeelsLikeC, "VRCOSC/Weather/FeelsLikeC", ParameterMode.Write, "Feels Like C", "Apparent temperature in Celsius");
        RegisterParameter<float>(WeatherParameter.WindKph, "VRCOSC/Weather/WindKph", ParameterMode.Write, "Wind km/h", "Wind speed at 10 m in km/h");
        RegisterParameter<bool>(WeatherParameter.IsDay, "VRCOSC/Weather/IsDay", ParameterMode.Write, "Is Day", "Whether it is daytime at the location");
    }

    protected override void OnPostLoad()
    {
        var emoji = CreateVariable<string>(WeatherVariable.Emoji, "Emoji")!;
        var condition = CreateVariable<string>(WeatherVariable.Condition, "Condition")!;
        var tempC = CreateVariable<float>(WeatherVariable.TempC, "Temp C")!;
        CreateVariable<float>(WeatherVariable.TempF, "Temp F");
        var feelsC = CreateVariable<float>(WeatherVariable.FeelsLikeC, "Feels Like C")!;
        CreateVariable<float>(WeatherVariable.FeelsLikeF, "Feels Like F");
        CreateVariable<int>(WeatherVariable.Humidity, "Humidity (%)");
        var wind = CreateVariable<string>(WeatherVariable.Wind, "Wind (speed + direction)")!;
        CreateVariable<float>(WeatherVariable.WindKph, "Wind km/h");
        CreateVariable<float>(WeatherVariable.WindMph, "Wind mph");
        CreateVariable<string>(WeatherVariable.WindDirection, "Wind Direction");
        CreateVariable<string>(WeatherVariable.LocationName, "Location Name");

        CreateState(WeatherState.Default, "Default", "{0} {1}\n{2}C (feels {3}C)\n{4}", new[] { emoji, condition, tempC, feelsC, wind });
        CreateState(WeatherState.Unavailable, "Unavailable", "Weather unavailable");
    }

    protected override Task<bool> OnModuleStart()
    {
        if (string.IsNullOrWhiteSpace(GetSettingValue<string>(WeatherSetting.Location)))
        {
            Log("Set a location (city or \"lat, lon\") in the module settings.");
            return Task.FromResult(false);
        }

        _lastFetch = DateTime.MinValue;
        ChangeState(WeatherState.Unavailable);
        return Task.FromResult(true);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 5000)]
    private void Update()
    {
        if (_fetching || DateTime.UtcNow - _lastFetch < RefreshInterval) return;
        _fetching = true;
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var locationSetting = GetSettingValue<string>(WeatherSetting.Location)?.Trim() ?? string.Empty;
            if (_location is null || _resolvedFor != locationSetting)
            {
                _location = await ResolveLocationAsync(locationSetting).ConfigureAwait(false);
                _resolvedFor = locationSetting;
                if (_location is null)
                {
                    Log($"Could not resolve location '{locationSetting}'.");
                    ChangeState(WeatherState.Unavailable);
                    _lastFetch = DateTime.UtcNow; // don't hammer the geocoder
                    return;
                }
                Log($"Weather location: {_location.Value.Name} ({_location.Value.Lat:0.###}, {_location.Value.Lon:0.###})");
            }

            var current = await FetchCurrentAsync(_location.Value.Lat, _location.Value.Lon).ConfigureAwait(false);
            _lastFetch = DateTime.UtcNow;
            if (current is null) { ChangeState(WeatherState.Unavailable); return; }

            Apply(current.Value, _location.Value.Name);
            ChangeState(WeatherState.Default);
        }
        catch (HttpRequestException ex) { Log($"Weather request failed: {ex.Message}"); ChangeState(WeatherState.Unavailable); _lastFetch = DateTime.UtcNow; }
        catch (TaskCanceledException) { Log("Weather request timed out."); _lastFetch = DateTime.UtcNow; }
        catch (JsonException ex) { Log($"Weather response was not valid JSON: {ex.Message}"); _lastFetch = DateTime.UtcNow; }
        finally { _fetching = false; }
    }

    private void Apply(Current w, string locationName)
    {
        var (condition, emojiDay, emojiNight) = Describe(w.Code);
        var emoji = w.IsDay ? emojiDay : emojiNight;
        var direction = Compass(w.WindDirection);

        SetVariableValue(WeatherVariable.Emoji, emoji);
        SetVariableValue(WeatherVariable.Condition, condition);
        SetVariableValue(WeatherVariable.TempC, Round1(w.TempC));
        SetVariableValue(WeatherVariable.TempF, Round1(w.TempC * 9f / 5f + 32f));
        SetVariableValue(WeatherVariable.FeelsLikeC, Round1(w.FeelsLikeC));
        SetVariableValue(WeatherVariable.FeelsLikeF, Round1(w.FeelsLikeC * 9f / 5f + 32f));
        SetVariableValue(WeatherVariable.Humidity, w.Humidity);
        SetVariableValue(WeatherVariable.Wind, $"{MathF.Round(w.WindKph)} km/h {direction}");
        SetVariableValue(WeatherVariable.WindKph, Round1(w.WindKph));
        SetVariableValue(WeatherVariable.WindMph, Round1(w.WindKph * 0.621371f));
        SetVariableValue(WeatherVariable.WindDirection, direction);
        SetVariableValue(WeatherVariable.LocationName, locationName);

        SendParameter(WeatherParameter.Code, w.Code);
        SendParameter(WeatherParameter.TempC, w.TempC);
        SendParameter(WeatherParameter.FeelsLikeC, w.FeelsLikeC);
        SendParameter(WeatherParameter.WindKph, w.WindKph);
        SendParameter(WeatherParameter.IsDay, w.IsDay);
    }

    private static float Round1(float v) => MathF.Round(v, 1);

    private static async Task<(double, double, string)?> ResolveLocationAsync(string setting)
    {
        // "lat, lon" typed directly
        var parts = setting.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180)
        {
            return (lat, lon, $"{lat:0.###}, {lon:0.###}");
        }

        // "City" or "City, CC"
        var name = parts.Length > 0 ? parts[0] : setting;
        var country = parts.Length > 1 ? parts[1].ToUpperInvariant() : null;
        var url = "https://geocoding-api.open-meteo.com/v1/search?count=5&language=en&format=json&name=" + Uri.EscapeDataString(name);

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array) return null;

        JsonElement? pick = null;
        foreach (var r in results.EnumerateArray())
        {
            if (country is null || (r.TryGetProperty("country_code", out var cc) && string.Equals(cc.GetString(), country, StringComparison.OrdinalIgnoreCase)))
            {
                pick = r;
                break;
            }
        }
        pick ??= results.GetArrayLength() > 0 ? results[0] : null;
        if (pick is null) return null;

        var e = pick.Value;
        var label = e.GetProperty("name").GetString() ?? name;
        if (e.TryGetProperty("country_code", out var code)) label += ", " + code.GetString();
        return (e.GetProperty("latitude").GetDouble(), e.GetProperty("longitude").GetDouble(), label);
    }

    private readonly record struct Current(float TempC, float FeelsLikeC, int Humidity, float WindKph, float WindDirection, int Code, bool IsDay);

    private static async Task<Current?> FetchCurrentAsync(double lat, double lon)
    {
        var url = "https://api.open-meteo.com/v1/forecast"
                  + "?latitude=" + lat.ToString("0.####", CultureInfo.InvariantCulture)
                  + "&longitude=" + lon.ToString("0.####", CultureInfo.InvariantCulture)
                  + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,wind_direction_10m,weather_code,is_day"
                  + "&wind_speed_unit=kmh&timezone=auto";

        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("current", out var c)) return null;

        static float F(JsonElement e, string key) => e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : 0f;

        return new Current(
            F(c, "temperature_2m"),
            F(c, "apparent_temperature"),
            (int)MathF.Round(F(c, "relative_humidity_2m")),
            F(c, "wind_speed_10m"),
            F(c, "wind_direction_10m"),
            (int)MathF.Round(F(c, "weather_code")),
            F(c, "is_day") >= 0.5f);
    }

    /// <summary>WMO 4677 weather interpretation codes, as used by Open-Meteo.</summary>
    private static (string Condition, string Day, string Night) Describe(int code) => code switch
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

    private static string Compass(float degrees)
    {
        string[] points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        var index = (int)MathF.Round(((degrees % 360f) + 360f) % 360f / 45f) % 8;
        return points[index];
    }

    private enum WeatherSetting { Location }

    private enum WeatherParameter { Code, TempC, FeelsLikeC, WindKph, IsDay }

    private enum WeatherVariable
    {
        Emoji, Condition, TempC, TempF, FeelsLikeC, FeelsLikeF, Humidity,
        Wind, WindKph, WindMph, WindDirection, LocationName
    }

    private enum WeatherState { Default, Unavailable }
}
