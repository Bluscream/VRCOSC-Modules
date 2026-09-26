// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// MagicChatbox parity: every MagicChatbox placeholder whose natural home would need a pull
// request against another module or against the SDK, reimplemented here so the ChatBox
// variables exist now instead of waiting on upstream. Split by concern into partial files:
//   MCBParityModule.Weather.cs   weatherapi.com (Open-Meteo fallback) feels-like / wind / emoji
//   MCBParityModule.Instance.cs  VRChat log tail (VRChatLogTail.cs): instance master + world capacity
// Variable enum members are spelled exactly like the MagicChatbox placeholder keys because the
// SDK derives the ChatBox lookup from the enum member name (lower-cased).

using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.MCBParity;

[ModuleTitle("MagicChatbox Parity")]
[ModuleDescription("Weather feels-like/wind/emoji plus VRChat instance master and capacity as ChatBox variables (MagicChatbox placeholder parity)")]
[ModuleType(ModuleType.Generic)]
public sealed partial class MCBParityModule : Module
{
    protected override void OnPreLoad()
    {
        CreateWeatherSettings();
        CreateInstanceSettings();
    }

    protected override void OnPostLoad()
    {
        var (temp, feelsLike, wind, emoji) = CreateWeatherVariables();
        var (capacity, master) = CreateInstanceVariables();

        CreateState(MCBParityState.Default, "Default", "{0} {1} (feels {2})\n{3}", new[] { emoji, temp, feelsLike, wind });
        CreateState(MCBParityState.InInstance, "In Instance", "{0} {1} (feels {2})\n{3} cap {4}", new[] { emoji, temp, feelsLike, master, capacity });
        CreateState(MCBParityState.NotInInstance, "Not In Instance", "{0} {1} (feels {2})\n{3}", new[] { emoji, temp, feelsLike, wind });
    }

    protected override Task<bool> OnModuleStart()
    {
        StartWeather();
        StartInstance();
        ChangeState(MCBParityState.NotInInstance);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        StopInstance();
        return Task.CompletedTask;
    }

    private enum MCBParitySetting
    {
        WeatherApiKey, WeatherLocation, WeatherRefreshMinutes, TemperatureUnit, WindUnit,
        MasterIcon, VRChatLogDirectory
    }

    // Spelled as the MagicChatbox placeholder keys on purpose (the lookup is the lower-cased name).
    private enum MCBParityVariable
    {
        weather_temp, weather_feels_like, weather_wind, weather_emoji, weather_condition, weather_humidity,
        vrc_instance_capacity, vrc_master
    }

    private enum MCBParityState { Default, InInstance, NotInInstance }
}
