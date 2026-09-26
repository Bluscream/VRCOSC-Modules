// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Exposes what VRCOSC already knows about the current VRChat instance (from its own log
// reader) as ChatBox variables, so world name, instance type and region can be used in
// clips without a VRChat API login or a VRCX dependency.

using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;

namespace Bluscream.Modules;

[ModuleTitle("VRChat Extras")]
[ModuleDescription("Current world, instance type, region and player count as ChatBox variables, straight from VRCOSC's VRChat log reader")]
[ModuleType(ModuleType.Generic)]
public class VRCExtrasModule : Module
{
    private string? _lastInstanceId;

    protected override void OnPreLoad()
    {
        CreateTextBox(VRCExtrasSetting.MasterIcon, "Instance master icon",
            "Text shown in the Master Icon variable while you are the instance master. VRCOSC cannot tell who the master is, so this stays empty; the setting exists so the variable can be mapped now and filled in later.", "\U0001F451");

        RegisterParameter<int>(VRCExtrasParameter.InstanceType, "VRCOSC/VRChat/Instance/Type", ParameterMode.Write, "Instance Type",
            "0 public, 1 friends+, 2 friends, 3 invite+, 4 invite, 5 group, 6 group+, 7 group public");
        RegisterParameter<int>(VRCExtrasParameter.Region, "VRCOSC/VRChat/Instance/Region", ParameterMode.Write, "Instance Region",
            "0 unknown, 1 US West, 2 US East, 3 Europe, 4 Japan");
        RegisterParameter<int>(VRCExtrasParameter.PlayerCount, "VRCOSC/VRChat/Instance/PlayerCount", ParameterMode.Write, "Player Count", "Players currently in the instance");
        RegisterParameter<bool>(VRCExtrasParameter.InInstance, "VRCOSC/VRChat/Instance/Joined", ParameterMode.Write, "In Instance", "Whether you are in an instance");
        RegisterParameter<bool>(VRCExtrasParameter.AgeGated, "VRCOSC/VRChat/Instance/AgeGated", ParameterMode.Write, "Age Gated", "Whether the instance is age gated");
    }

    protected override void OnPostLoad()
    {
        var world = CreateVariable<string>(VRCExtrasVariable.World, "World Name")!;
        CreateVariable<string>(VRCExtrasVariable.WorldId, "World ID");
        var type = CreateVariable<string>(VRCExtrasVariable.InstanceType, "Instance Type")!;
        var region = CreateVariable<string>(VRCExtrasVariable.Region, "Instance Region")!;
        var players = CreateVariable<int>(VRCExtrasVariable.PlayerCount, "Player Count")!;
        CreateVariable<string>(VRCExtrasVariable.InstanceId, "Instance ID");
        CreateVariable<string>(VRCExtrasVariable.InstanceOwner, "Instance Owner ID");
        CreateVariable<bool>(VRCExtrasVariable.AgeGated, "Age Gated");
        CreateVariable<bool>(VRCExtrasVariable.HasQueue, "Has Queue");
        CreateVariable<string>(VRCExtrasVariable.MasterIcon, "Master Icon");

        CreateState(VRCExtrasState.InInstance, "In Instance", "{0}\n{1} · {2} · {3} players", new[] { world, type, region, players });
        CreateState(VRCExtrasState.NotInInstance, "Not In Instance", string.Empty);

        CreateEvent(VRCExtrasEvent.WorldChanged, "World Changed", "Now in {0}", new[] { world });
    }

    protected override Task<bool> OnModuleStart()
    {
        _lastInstanceId = null;
        ChangeState(VRCExtrasState.NotInInstance);
        return Task.FromResult(true);
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void Update()
    {
        var client = GetClient();
        var instance = client.IsInInstance ? client.Instance : null;

        if (instance is null)
        {
            if (_lastInstanceId is not null)
            {
                _lastInstanceId = null;
                ChangeState(VRCExtrasState.NotInInstance);
            }

            SetVariableValue(VRCExtrasVariable.World, string.Empty);
            SetVariableValue(VRCExtrasVariable.WorldId, string.Empty);
            SetVariableValue(VRCExtrasVariable.InstanceType, string.Empty);
            SetVariableValue(VRCExtrasVariable.Region, string.Empty);
            SetVariableValue(VRCExtrasVariable.PlayerCount, 0);
            SetVariableValue(VRCExtrasVariable.InstanceId, string.Empty);
            SetVariableValue(VRCExtrasVariable.InstanceOwner, string.Empty);
            SetVariableValue(VRCExtrasVariable.AgeGated, false);
            SetVariableValue(VRCExtrasVariable.HasQueue, false);
            SetVariableValue(VRCExtrasVariable.MasterIcon, string.Empty);

            SendParameter(VRCExtrasParameter.InInstance, false);
            SendParameter(VRCExtrasParameter.PlayerCount, 0);
            return;
        }

        if (instance.Id != _lastInstanceId)
        {
            _lastInstanceId = instance.Id;
            ChangeState(VRCExtrasState.InInstance);
            TriggerEvent(VRCExtrasEvent.WorldChanged);
        }

        var playerCount = instance.Users.Count;

        SetVariableValue(VRCExtrasVariable.World, instance.World.Name);
        SetVariableValue(VRCExtrasVariable.WorldId, instance.World.Id);
        SetVariableValue(VRCExtrasVariable.InstanceType, TypeText(instance.Type));
        SetVariableValue(VRCExtrasVariable.Region, RegionText(instance.Region));
        SetVariableValue(VRCExtrasVariable.PlayerCount, playerCount);
        SetVariableValue(VRCExtrasVariable.InstanceId, instance.Id ?? string.Empty);
        SetVariableValue(VRCExtrasVariable.InstanceOwner, instance.OwnerId ?? string.Empty);
        SetVariableValue(VRCExtrasVariable.AgeGated, instance.AgeGated);
        SetVariableValue(VRCExtrasVariable.HasQueue, instance.HasQueue);
        // Master status is not observable through VRCOSC's log reader; keep the variable so
        // layouts can reference it, but it never resolves to the icon today.
        SetVariableValue(VRCExtrasVariable.MasterIcon, string.Empty);

        SendParameter(VRCExtrasParameter.InInstance, true);
        SendParameter(VRCExtrasParameter.InstanceType, (int)instance.Type);
        SendParameter(VRCExtrasParameter.Region, (int)instance.Region);
        SendParameter(VRCExtrasParameter.PlayerCount, playerCount);
        SendParameter(VRCExtrasParameter.AgeGated, instance.AgeGated);
    }

    /// <summary>Same spellings MagicChatbox and VRChat's own UI use.</summary>
    private static string TypeText(InstanceType type) => type switch
    {
        InstanceType.Public => "public",
        InstanceType.FriendsPlus => "friends+",
        InstanceType.Friends => "friends",
        InstanceType.InvitePlus => "invite+",
        InstanceType.Invite => "invite",
        InstanceType.Group => "group",
        InstanceType.GroupPlus => "group+",
        InstanceType.GroupPublic => "group public",
        _ => string.Empty
    };

    private static string RegionText(InstanceRegion region) => region switch
    {
        InstanceRegion.USWest => "us",
        InstanceRegion.USEast => "use",
        InstanceRegion.Europe => "eu",
        InstanceRegion.Japan => "jp",
        _ => string.Empty
    };

    private enum VRCExtrasSetting { MasterIcon }

    private enum VRCExtrasParameter { InstanceType, Region, PlayerCount, InInstance, AgeGated }

    private enum VRCExtrasVariable
    {
        World, WorldId, InstanceType, Region, PlayerCount,
        InstanceId, InstanceOwner, AgeGated, HasQueue, MasterIcon
    }

    private enum VRCExtrasState { InInstance, NotInInstance }

    private enum VRCExtrasEvent { WorldChanged }
}
