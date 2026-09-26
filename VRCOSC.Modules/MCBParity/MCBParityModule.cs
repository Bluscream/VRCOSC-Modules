// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox parity: every MagicChatbox placeholder whose natural home would need a pull
// request against another module or against the SDK, reimplemented here so the ChatBox
// variables exist now instead of waiting on upstream. Split by concern into partial files:
//   MCBParityModule.Weather.cs   weatherapi.com (Open-Meteo fallback) feels-like / wind / emoji
//   MCBParityModule.VR.cs        reprojection / dropped-frame estimates from the shared OpenXR runtime
// Instance master and world capacity live in VRCExtras (VRChatLogTail.cs) next to the rest
// of the instance data. Variable enum members are spelled exactly like the MagicChatbox
// placeholder keys because the SDK derives the ChatBox lookup from the enum member name
// (lower-cased).

using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.MCBParity;

[ModuleTitle("MagicChatbox Parity")]
[ModuleDescription("Weather feels-like/wind/emoji and VR reprojection/dropped-frame estimates as ChatBox variables (MagicChatbox placeholder parity)")]
[ModuleType(ModuleType.Generic)]
public sealed partial class MCBParityModule : Module
{
    protected override void OnPreLoad()
    {
        CreateWeatherSettings();
        CreateVrSettings();
    }

    protected override void OnPostLoad()
    {
        var (temp, feelsLike, wind, emoji) = CreateWeatherVariables();
        var (reprojection, dropped) = CreateVrVariables();

        CreateState(MCBParityState.Default, "Default", "{0} {1} (feels {2})\n{3}\nReproj {4}% · Dropped {5}/min", new[] { emoji, temp, feelsLike, wind, reprojection, dropped });
    }

    protected override Task<bool> OnModuleStart()
    {
        StartWeather();
        StartVr();
        ChangeState(MCBParityState.Default);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        StopVr();
        return Task.CompletedTask;
    }

    private enum MCBParitySetting
    {
        WeatherApiKey, WeatherLocation, WeatherRefreshMinutes, TemperatureUnit, WindUnit,
        VrFrameStats
    }

    // Spelled as the MagicChatbox placeholder keys on purpose (the lookup is the lower-cased name).
    private enum MCBParityVariable
    {
        weather_temp, weather_feels_like, weather_wind, weather_emoji, weather_condition, weather_humidity,
        vr_reprojection, vr_dropped_frames
    }

    private enum MCBParityState { Default }
}
