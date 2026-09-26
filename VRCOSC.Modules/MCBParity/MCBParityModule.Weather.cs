// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Weather part: the official Weather module only exposes temperature, humidity and condition
// text (feels-like / wind / emoji PRs are pending upstream). This re-does the weatherapi.com
// current.json fetch the SDK's WeatherProvider performs, reads the extra fields it discards
// and falls back to Open-Meteo (keyless) when no API key is configured.

using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.MCBParity;

public sealed partial class MCBParityModule
{
    private static readonly HttpClient Http = CreateHttpClient();

    // weatherapi.com: same endpoint and query the SDK WeatherProvider uses (current.json?key=&q=).
    private const string WeatherApiCurrentUrl = "https://api.weatherapi.com/v1/current.json?key={0}&q={1}";
    private const string OpenMeteoGeocodeUrl = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=en&format=json&name={0}";
    private const string OpenMeteoCurrentUrl = "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}"
                                               + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,wind_direction_10m,weather_code,is_day"
                                               + "&wind_speed_unit=kmh&timezone=auto";

    private DateTime _weatherLastFetch = DateTime.MinValue;
    private bool _weatherFetching;
    private bool _weatherLoggedNoKey;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VRCOSC-BluscreamModules/1.0 (https://github.com/Bluscream/VRCOSC-Modules)");
        return client;
    }

    private void CreateWeatherSettings()
    {
        CreateTextBox(MCBParitySetting.WeatherApiKey, "weatherapi.com API key",
            "Free key from https://www.weatherapi.com. Leave empty to use Open-Meteo instead (no key needed, but no weatherapi condition texts).", string.Empty);
        CreateTextBox(MCBParitySetting.WeatherLocation, "Weather location",
            "City name, UK/US/Canada postcode or \"lat, lon\". Passed to weatherapi.com as-is; geocoded through Open-Meteo on the fallback path.", string.Empty);
        CreateSlider(MCBParitySetting.WeatherRefreshMinutes, "Weather refresh (minutes)", "How often the weather is re-fetched.", 10, 1, 60, 1);
        CreateDropdown(MCBParitySetting.TemperatureUnit, "Temperature unit", "Unit for the temperature and feels-like variables.", TemperatureUnit.Celsius);
        CreateDropdown(MCBParitySetting.WindUnit, "Wind speed unit", "Unit for the wind variable.", WindUnit.Kmh);
    }

    private (ClipVariableReference Temp, ClipVariableReference FeelsLike, ClipVariableReference Wind, ClipVariableReference Emoji) CreateWeatherVariables()
    {
        var temp = CreateVariable<string>(MCBParityVariable.weather_temp, "Weather Temperature (with unit)")!;
        var feelsLike = CreateVariable<string>(MCBParityVariable.weather_feels_like, "Weather Feels Like (with unit)")!;
        var wind = CreateVariable<string>(MCBParityVariable.weather_wind, "Weather Wind (speed + direction)")!;
        var emoji = CreateVariable<string>(MCBParityVariable.weather_emoji, "Weather Emoji")!;
        CreateVariable<string>(MCBParityVariable.weather_condition, "Weather Condition");
        CreateVariable<int>(MCBParityVariable.weather_humidity, "Weather Humidity (%)");
        return (temp, feelsLike, wind, emoji);
    }

    private void StartWeather()
    {
        _weatherLastFetch = DateTime.MinValue;
        _weatherLoggedNoKey = false;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 5000)]
    private void UpdateWeather()
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, GetSettingValue<int>(MCBParitySetting.WeatherRefreshMinutes)));
        if (_weatherFetching || DateTime.UtcNow - _weatherLastFetch < interval) return;
        if (string.IsNullOrWhiteSpace(GetSettingValue<string>(MCBParitySetting.WeatherLocation))) return;

        _weatherFetching = true;
        _ = RefreshWeatherAsync();
    }

    private async Task RefreshWeatherAsync()
    {
        try
        {
            var location = GetSettingValue<string>(MCBParitySetting.WeatherLocation)!.Trim();
            var apiKey = GetSettingValue<string>(MCBParitySetting.WeatherApiKey)?.Trim() ?? string.Empty;

            WeatherReading? reading;
            if (apiKey.Length > 0)
            {
                reading = await FetchWeatherApiAsync(apiKey, location).ConfigureAwait(false);
            }
            else
            {
                if (!_weatherLoggedNoKey)
                {
                    _weatherLoggedNoKey = true;
                    Log("No weatherapi.com key set; using Open-Meteo for weather.");
                }
                reading = await FetchOpenMeteoAsync(location).ConfigureAwait(false);
            }

            if (reading is not null) ApplyWeather(reading.Value);
        }
        catch (HttpRequestException ex) { Log($"Weather request failed: {ex.Message}"); }
        catch (TaskCanceledException) { Log("Weather request timed out."); }
        catch (JsonException ex) { Log($"Weather response was not valid JSON: {ex.Message}"); }
        finally
        {
            _weatherLastFetch = DateTime.UtcNow;
            _weatherFetching = false;
        }
    }

    private void ApplyWeather(WeatherReading w)
    {
        var fahrenheit = GetSettingValue<TemperatureUnit>(MCBParitySetting.TemperatureUnit) == TemperatureUnit.Fahrenheit;
        var mph = GetSettingValue<WindUnit>(MCBParitySetting.WindUnit) == WindUnit.Mph;

        SetVariableValue(MCBParityVariable.weather_temp, FormatTemperature(w.TempC, fahrenheit));
        SetVariableValue(MCBParityVariable.weather_feels_like, FormatTemperature(w.FeelsLikeC, fahrenheit));
        SetVariableValue(MCBParityVariable.weather_wind, FormatWind(w.WindKph, w.WindDirection, mph));
        SetVariableValue(MCBParityVariable.weather_emoji, w.Emoji);
        SetVariableValue(MCBParityVariable.weather_condition, w.Condition);
        SetVariableValue(MCBParityVariable.weather_humidity, w.Humidity);
    }

    private static string FormatTemperature(float celsius, bool fahrenheit)
    {
        var value = fahrenheit ? celsius * 9f / 5f + 32f : celsius;
        return MathF.Round(value).ToString("0", CultureInfo.InvariantCulture) + (fahrenheit ? "°F" : "°C");
    }

    private static string FormatWind(float kph, string direction, bool mph)
    {
        var speed = MathF.Round(mph ? kph * 0.621371f : kph).ToString("0", CultureInfo.InvariantCulture);
        return $"{speed} {(mph ? "mph" : "km/h")} {direction}".TrimEnd();
    }

    private readonly record struct WeatherReading(float TempC, float FeelsLikeC, int Humidity, float WindKph, string WindDirection, string Condition, string Emoji);

    // ─────────────────────────── weatherapi.com ───────────────────────────

    private async Task<WeatherReading?> FetchWeatherApiAsync(string apiKey, string location)
    {
        var url = string.Format(CultureInfo.InvariantCulture, WeatherApiCurrentUrl, Uri.EscapeDataString(apiKey), Uri.EscapeDataString(location));
        using var response = await Http.GetAsync(url).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("error", out var error))
        {
            var message = error.TryGetProperty("message", out var m) ? m.GetString() : response.StatusCode.ToString();
            Log($"weatherapi.com: {message}");
            return null;
        }

        if (!doc.RootElement.TryGetProperty("current", out var c)) return null;

        var condition = c.TryGetProperty("condition", out var cond) ? cond : default;
        var code = condition.ValueKind == JsonValueKind.Object && condition.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : 0;
        var text = condition.ValueKind == JsonValueKind.Object && condition.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? string.Empty : string.Empty;
        var isDay = JsonNumber(c, "is_day") >= 0.5f;

        return new WeatherReading(
            JsonNumber(c, "temp_c"),
            JsonNumber(c, "feelslike_c"),
            (int)MathF.Round(JsonNumber(c, "humidity")),
            JsonNumber(c, "wind_kph"),
            c.TryGetProperty("wind_dir", out var dir) ? dir.GetString() ?? string.Empty : string.Empty,
            text,
            WmoWeatherCodes.Emoji(WeatherApiCodeToWmo(code), isDay));
    }

    /// <summary>
    /// weatherapi.com condition codes (https://www.weatherapi.com/docs/weather_conditions.json)
    /// onto the nearest WMO 4677 code so the shared emoji table serves both providers.
    /// </summary>
    private static int WeatherApiCodeToWmo(int code) => code switch
    {
        1000 => 0,                                       // Sunny / Clear
        1003 => 2,                                       // Partly cloudy
        1006 => 3,                                       // Cloudy
        1009 => 3,                                       // Overcast
        1012 or 1015 or 1018 or 1021 or 1024 or 1027 => 45, // Haze, dust, sandstorm
        1030 or 1033 or 1036 or 1039 or 1042 or 1045 or 1048 => 45, // Mist, smoke, smog, dust
        1135 or 1147 => 45,                              // Fog, freezing fog
        1063 => 80,                                      // Patchy rain possible
        1066 => 85,                                      // Patchy snow possible
        1069 => 85,                                      // Patchy sleet possible
        1072 => 56,                                      // Patchy freezing drizzle possible
        1087 => 95,                                      // Thundery outbreaks possible
        1114 or 1117 => 75,                              // Blowing snow, blizzard
        1150 or 1153 => 51,                              // Drizzle
        1168 or 1171 => 56,                              // Freezing drizzle
        1180 or 1183 or 1186 or 1189 => 61,              // Light / moderate rain
        1192 or 1195 => 65,                              // Heavy rain
        1198 or 1201 => 66,                              // Freezing rain
        1204 or 1207 => 71,                              // Sleet
        1210 or 1213 or 1216 or 1219 => 71,              // Light / moderate snow
        1222 or 1225 => 75,                              // Heavy snow
        1237 => 77,                                      // Ice pellets
        1240 or 1243 or 1246 => 80,                      // Rain showers
        1249 or 1252 => 85,                              // Sleet showers
        1255 or 1258 => 85,                              // Snow showers
        1261 or 1264 => 85,                              // Ice pellet showers
        1273 or 1276 => 95,                              // Rain with thunder
        1279 or 1282 => 96,                              // Snow with thunder
        _ => -1
    };

    // ─────────────────────────── Open-Meteo fallback ───────────────────────────

    private async Task<WeatherReading?> FetchOpenMeteoAsync(string location)
    {
        var coords = await ResolveOpenMeteoLocationAsync(location).ConfigureAwait(false);
        if (coords is null)
        {
            Log($"Open-Meteo could not resolve location '{location}'.");
            return null;
        }

        var (lat, lon) = coords.Value;
        var url = string.Format(CultureInfo.InvariantCulture, OpenMeteoCurrentUrl, lat.ToString("0.####", CultureInfo.InvariantCulture), lon.ToString("0.####", CultureInfo.InvariantCulture));
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("current", out var c)) return null;

        var code = (int)MathF.Round(JsonNumber(c, "weather_code"));
        var isDay = JsonNumber(c, "is_day") >= 0.5f;

        return new WeatherReading(
            JsonNumber(c, "temperature_2m"),
            JsonNumber(c, "apparent_temperature"),
            (int)MathF.Round(JsonNumber(c, "relative_humidity_2m")),
            JsonNumber(c, "wind_speed_10m"),
            WmoWeatherCodes.Compass(JsonNumber(c, "wind_direction_10m")),
            WmoWeatherCodes.Describe(code).Condition,
            WmoWeatherCodes.Emoji(code, isDay));
    }

    private static async Task<(double Lat, double Lon)?> ResolveOpenMeteoLocationAsync(string setting)
    {
        var parts = setting.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
            && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180)
        {
            return (lat, lon);
        }

        var name = parts.Length > 0 ? parts[0] : setting;
        var url = string.Format(CultureInfo.InvariantCulture, OpenMeteoGeocodeUrl, Uri.EscapeDataString(name));
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(url).ConfigureAwait(false));
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;

        var first = results[0];
        return (first.GetProperty("latitude").GetDouble(), first.GetProperty("longitude").GetDouble());
    }

    private static float JsonNumber(JsonElement element, string key)
        => element.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? (float)v.GetDouble() : 0f;

    private enum TemperatureUnit { Celsius, Fahrenheit }

    private enum WindUnit { Kmh, Mph }
}
