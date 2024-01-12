﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using VRCOSC.Graphics;
using VRCOSC.SDK;
using VRCOSC.SDK.Attributes.Settings;
using VRCOSC.SDK.Attributes.Settings.Addons;
using VRCOSC.SDK.Avatars;
using VRCOSC.SDK.Parameters;
using VRCOSC.SDK.Providers.PiShock;

namespace VRCOSC.Modules.PiShock;

[ModuleTitle("PiShock")]
[ModuleDescription("Allows for controlling PiShock shockers")]
[ModuleType(ModuleType.NSFW)]
[ModulePrefab("VRCOSC-PiShock", "https://github.com/VolcanicArts/VRCOSC/releases/download/latest/VRCOSC-PiShock.unitypackage")]
public class PiShockModule : AvatarModule
{
    private PiShockProvider? piShockProvider;

    private int group;
    private float duration;
    private float intensity;

    private (DateTimeOffset, int?)? shock;
    private (DateTimeOffset, int?)? vibrate;
    private (DateTimeOffset, int?)? beep;
    private bool shockExecuted;
    private bool vibrateExecuted;
    private bool beepExecuted;

    protected override void OnLoad()
    {
        CreateTextBox(PiShockSetting.Username, "Username", "Your PiShock username", string.Empty);
        CreateTextBox(PiShockSetting.APIKey, "API Key", "Your PiShock API key", string.Empty);

        CreateTextBox(PiShockSetting.Delay, "Button Delay", "The amount of time in milliseconds the shock, vibrate, and beep parameters need to be true to execute the action. This is helpful for if you accidentally press buttons on your action menu", 0);
        CreateSlider(PiShockSetting.MaxDuration, "Max Duration", "The maximum value the duration can be in seconds. This is the upper limit of 100% duration and is local only", 15, 1, 15);
        CreateSlider(PiShockSetting.MaxIntensity, "Max Intensity", "The maximum value the intensity can be in percent. This is the upper limit of 100% intensity and is local only", 100, 1, 100);

        CreateCustom(PiShockSetting.Shockers, new ShockerListModuleSetting(new ListModuleSettingMetadata("Shockers", "Each instance represents a single shocker using a sharecode. The name is used as a readable reference and can be anything you like", typeof(DrawableShockerListModuleSetting), typeof(DrawableShocker)), Array.Empty<Shocker>()));

        CreateCustom(PiShockSetting.Groups, new GroupListModuleSetting(new ListModuleSettingMetadata("Groups", "Each instance should contain one or more shocker names separated by a comma. A group can be chosen by setting the Group parameter to the left number", typeof(DrawableGroupListModuleSetting), typeof(DrawableGroup)), Array.Empty<Group>()));

        CreateGroup("Credentials", PiShockSetting.Username, PiShockSetting.APIKey);
        CreateGroup("Tweaks", PiShockSetting.Delay, PiShockSetting.MaxDuration, PiShockSetting.MaxIntensity);
        CreateGroup("Shockers", PiShockSetting.Shockers);
        CreateGroup("Groups", PiShockSetting.Groups);

        RegisterParameter<float>(PiShockParameter.Duration, "VRCOSC/PiShock/Duration", ParameterMode.Read, "Duration", "The duration of the action as a 0-1 float mapped between 1 and Max Duration");
        RegisterParameter<float>(PiShockParameter.Intensity, "VRCOSC/PiShock/Intensity", ParameterMode.Read, "Intensity", "The intensity of the action as a 0-1 float mapped between 1 and Max Intensity");
        RegisterParameter<int>(PiShockParameter.Group, "VRCOSC/PiShock/Group", ParameterMode.Read, "Group", "Sets the specific group to use when using the non-specific action parameters");
        RegisterParameter<bool>(PiShockParameter.Shock, "VRCOSC/PiShock/Shock", ParameterMode.Read, "Shock", "Shock the group set by the Group parameter");
        RegisterParameter<bool>(PiShockParameter.Vibrate, "VRCOSC/PiShock/Vibrate", ParameterMode.Read, "Vibrate", "Vibrate the group set by the Group parameter");
        RegisterParameter<bool>(PiShockParameter.Beep, "VRCOSC/PiShock/Beep", ParameterMode.Read, "Beep", "Beep the group set by the Group parameter");
        RegisterParameter<bool>(PiShockParameter.ShockGroup, "VRCOSC/PiShock/Shock/*", ParameterMode.Read, "Shock Group", "Shock a specific group\nE.G. VRCOSC/PiShock/Shock/0");
        RegisterParameter<bool>(PiShockParameter.VibrateGroup, "VRCOSC/PiShock/Vibrate/*", ParameterMode.Read, "Vibrate Group", "Vibrate a specific group\nE.G. VRCOSC/PiShock/Vibrate/0");
        RegisterParameter<bool>(PiShockParameter.BeepGroup, "VRCOSC/PiShock/Beep/*", ParameterMode.Read, "Beep Group", "Beep a specific group\nE.G. VRCOSC/PiShock/Beep/0");
    }

    protected override void OnPostLoad()
    {
        GetSetting(PiShockSetting.APIKey)!
            .AddAddon(new ButtonModuleSettingAddon("Generate API Key", Colours.BLUE0, () => OpenUrlExternally("https://pishock.com/#/account")));
    }

    protected override Task<bool> OnModuleStart()
    {
        reset();

        piShockProvider = new PiShockProvider();

        return Task.FromResult(true);
    }

    protected override void OnAvatarChange()
    {
        reset();
    }

    private void reset()
    {
        group = 0;
        duration = 0f;
        intensity = 0f;
        shock = null;
        vibrate = null;
        beep = null;
        shockExecuted = false;
        vibrateExecuted = false;
        beepExecuted = false;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom)]
    private void checkForExecutions()
    {
        var delay = TimeSpan.FromMilliseconds(GetSettingValue<int>(PiShockSetting.Delay));

        if (shock is not null && shock.Value.Item1 + delay <= DateTimeOffset.Now && !shockExecuted)
        {
            executePiShockMode(PiShockMode.Shock, shock.Value.Item2 ?? group);
            shockExecuted = true;
        }

        if (shock is null) shockExecuted = false;

        if (vibrate is not null && vibrate.Value.Item1 + delay <= DateTimeOffset.Now && !vibrateExecuted)
        {
            executePiShockMode(PiShockMode.Vibrate, vibrate.Value.Item2 ?? group);
            vibrateExecuted = true;
        }

        if (vibrate is null) vibrateExecuted = false;

        if (beep is not null && beep.Value.Item1 + delay <= DateTimeOffset.Now && !beepExecuted)
        {
            executePiShockMode(PiShockMode.Beep, beep.Value.Item2 ?? group);
            beepExecuted = true;
        }

        if (beep is null) beepExecuted = false;
    }

    private void executePiShockMode(PiShockMode mode, int group)
    {
        var groupData = GetSettingValue<List<Group>>(PiShockSetting.Groups)!.ElementAtOrDefault(group);

        if (groupData is null)
        {
            Log($"No group with ID {group}");
            return;
        }

        var shockerKeys = groupData.Names.Value.Split(',').Where(key => !string.IsNullOrEmpty(key)).Select(key => key.Trim());

        foreach (var shockerKey in shockerKeys)
        {
            var shockerInstance = getShockerInstanceFromKey(shockerKey);
            if (shockerInstance is null) continue;

            sendPiShockData(mode, shockerInstance);
        }
    }

    private async void sendPiShockData(PiShockMode mode, Shocker instance)
    {
        Log($"Executing {mode} on {instance.Name.Value}");

        var convertedDuration = (int)Math.Round(Map(duration, 0, 1, 1, GetSettingValue<int>(PiShockSetting.MaxDuration)));
        var convertedIntensity = (int)Math.Round(Map(intensity, 0, 1, 1, GetSettingValue<int>(PiShockSetting.MaxIntensity)));

        var response = await piShockProvider!.Execute(GetSettingValue<string>(PiShockSetting.Username)!, GetSettingValue<string>(PiShockSetting.APIKey)!, instance.Sharecode.Value, mode, convertedDuration, convertedIntensity);
        Log(response.Success ? $"{instance.Name.Value} succeeded" : $"{instance.Name.Value} failed - {response.Message}");
    }

    private Shocker? getShockerInstanceFromKey(string key)
    {
        var instance = GetSettingValue<List<Shocker>>(PiShockSetting.Shockers)!.SingleOrDefault(shockerInstance => shockerInstance.Name.Value == key);

        if (instance is not null) return instance;

        Log($"No shocker with key '{key}'");
        return null;
    }

    protected override void OnRegisteredParameterReceived(AvatarParameter parameter)
    {
        switch (parameter.Lookup)
        {
            case PiShockParameter.Group:
                group = parameter.GetValue<int>();
                break;

            case PiShockParameter.Duration:
                duration = Math.Clamp(parameter.GetValue<float>(), 0f, 1f);
                break;

            case PiShockParameter.Intensity:
                intensity = Math.Clamp(parameter.GetValue<float>(), 0f, 1f);
                break;

            case PiShockParameter.Shock:
                shock = parameter.GetValue<bool>() ? (DateTimeOffset.Now, null) : null;
                break;

            case PiShockParameter.Vibrate:
                vibrate = parameter.GetValue<bool>() ? (DateTimeOffset.Now, null) : null;
                break;

            case PiShockParameter.Beep:
                beep = parameter.GetValue<bool>() ? (DateTimeOffset.Now, null) : null;
                break;

            case PiShockParameter.ShockGroup:
                shock = parameter.GetValue<bool>() ? (DateTimeOffset.Now, parameter.WildcardAs<int>(0)) : null;
                break;

            case PiShockParameter.VibrateGroup:
                vibrate = parameter.GetValue<bool>() ? (DateTimeOffset.Now, parameter.WildcardAs<int>(0)) : null;
                break;

            case PiShockParameter.BeepGroup:
                beep = parameter.GetValue<bool>() ? (DateTimeOffset.Now, parameter.WildcardAs<int>(0)) : null;
                break;
        }
    }

    private enum PiShockSetting
    {
        Username,
        APIKey,
        MaxDuration,
        MaxIntensity,
        Shockers,
        Groups,
        Delay
    }

    private enum PiShockParameter
    {
        Group,
        Duration,
        Intensity,
        Shock,
        Vibrate,
        Beep,
        ShockGroup,
        VibrateGroup,
        BeepGroup
    }
}
