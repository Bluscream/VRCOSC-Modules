// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes.Settings;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.Providers.PiShock;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.App.Utils;
using VRCOSC.Modules.HTTP.UI;

namespace VRCOSC.Modules.HTTP;

[ModuleTitle("HTTP")]
[ModuleDescription("This module is work in progress and cannot be used yet!")] // Allows for sending and recieving HTTP requests
[ModuleType(ModuleType.Integrations)]
[ModulePrefab("Official Prefabs", "https://vrcosc.com/docs/downloads#prefabs")]
public class HTTPModule : Module
{
    private WebClient webClient = null!;

    protected override void OnPreLoad()
    {
        //CreateTextBox(HTTPSetting.Username, "Username", "Your PiShock username", string.Empty);
        //CreateCustomSetting(HTTPSetting.APIKey, new StringModuleSetting("API Key", "Your PiShock API key", typeof(PiShockAPIKeyView), string.Empty));

        //CreateTextBox(HTTPSetting.ButtonDelay, "Button Delay", "The amount of time in milliseconds the shock, vibrate, and beep parameters need to be true to execute the action. This is helpful for if you accidentally press buttons on your action menu", 0);

        //CreateCustomSetting(HTTPSetting.Shockers, new ModuleSetting());
        //CreateCustomSetting(HTTPSetting.Groups, new ShockerGroupModuleSetting());
        //CreateCustomSetting(HTTPSetting.Phrases, new PhraseModuleSetting());

        //CreateGroup("Credentials", HTTPSetting.Username, HTTPSetting.APIKey);
        //CreateGroup("Management", HTTPSetting.Shockers, HTTPSetting.Groups);
        //CreateGroup("Tweaks", HTTPSetting.ButtonDelay);
        //CreateGroup("Speech", HTTPSetting.Phrases);

        RegisterParameter<bool>(HTTPParameter.Success, "VRCOSC/HTTP/Success", ParameterMode.Write, "Success", "Becomes true for 1 second if the action has succeeded");
    }

    protected override void OnPostLoad()
    {
        //GetSetting<ModuleSetting>(HTTPSetting.).Attribute.OnCollectionChanged((_, _) =>
        //{
        //});
    }

    protected override Task<bool> OnModuleStart()
    {
        webClient = new();

        return Task.FromResult(true);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, updateImmediately: true, deltaMilliseconds: 5000)]
    private void sendRequests()
    {
        
    }

    private async void sendSuccessParameter()
    {
        var wasAcknowledged = await SendParameterAndWait(HTTPParameter.Success, true);

        if (wasAcknowledged)
            SendParameter(HTTPParameter.Success, false);
    }

    protected override void OnRegisteredParameterReceived(RegisteredParameter parameter)
    {
        switch (parameter.Lookup)
        {
            //case HTTPParameter.Group:

        }
    }

    private enum HTTPSetting
    {
    }

    private enum HTTPParameter
    {
        Success
    }
}