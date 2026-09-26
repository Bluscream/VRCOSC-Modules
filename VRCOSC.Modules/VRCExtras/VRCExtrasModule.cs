// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Exposes what VRCOSC already knows about the current VRChat instance (from its own log
// reader) as ChatBox variables, so world name, instance type and region can be used in
// clips without a VRChat API login or a VRCX dependency. Two facts the SDK reader does not
// surface come from a second, minimal tail of the same log (VRChatLogTail.cs): whether we
// are the instance master, and the world capacity, which the log never prints and is
// therefore looked up once per world on the public VRChat world endpoint (no login).

using System.Net.Http;
using System.Text.Json;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;

namespace Bluscream.Modules.VRCExtras;

[ModuleTitle("VRChat Extras")]
[ModuleDescription("Current world, instance type, region, player count, instance master and world capacity as ChatBox variables, from VRCOSC's VRChat log reader plus a minimal log tail")]
[ModuleType(ModuleType.Generic)]
public class VRCExtrasModule : Module
{
    private const string VRChatWorldUrl = "https://api.vrchat.cloud/api/1/worlds/{0}";

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly Dictionary<string, int> _capacityByWorld = new(StringComparer.OrdinalIgnoreCase);
    private VRChatLogTail? _logTail;
    private string? _lastInstanceId;
    private string _capacityRequestedFor = string.Empty;
    private bool _capacityFetching;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("VRCOSC-BluscreamModules/1.0 (https://github.com/Bluscream/VRCOSC-Modules)");
        return client;
    }

    protected override void OnPreLoad()
    {
        CreateTextBox(VRCExtrasSetting.MasterIcon, "Instance master icon",
            "Text shown in the Master Icon variable while you are the instance master; empty otherwise. Detected from the 'I am MASTER' lines in VRChat's log.", "\U0001F451");
        CreateTextBox(VRCExtrasSetting.VRChatLogDirectory, "VRChat log directory",
            "Override for the folder holding output_log_*.txt, as a path VRCOSC can open (Z:\\... under Wine). Leave empty to auto-detect (own prefix's LocalLow, then every Steam library's compatdata/438100).", string.Empty);

        RegisterParameter<int>(VRCExtrasParameter.InstanceType, "VRCOSC/VRChat/Instance/Type", ParameterMode.Write, "Instance Type",
            "0 public, 1 friends+, 2 friends, 3 invite+, 4 invite, 5 group, 6 group+, 7 group public");
        RegisterParameter<int>(VRCExtrasParameter.Region, "VRCOSC/VRChat/Instance/Region", ParameterMode.Write, "Instance Region",
            "0 unknown, 1 US West, 2 US East, 3 Europe, 4 Japan");
        RegisterParameter<int>(VRCExtrasParameter.PlayerCount, "VRCOSC/VRChat/Instance/PlayerCount", ParameterMode.Write, "Player Count", "Players currently in the instance");
        RegisterParameter<bool>(VRCExtrasParameter.InInstance, "VRCOSC/VRChat/Instance/Joined", ParameterMode.Write, "In Instance", "Whether you are in an instance");
        RegisterParameter<bool>(VRCExtrasParameter.AgeGated, "VRCOSC/VRChat/Instance/AgeGated", ParameterMode.Write, "Age Gated", "Whether the instance is age gated");
        RegisterParameter<bool>(VRCExtrasParameter.IsMaster, "VRCOSC/VRChat/Instance/Master", ParameterMode.Write, "Is Master", "Whether you are the instance master");
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
        CreateVariable<string>(VRCExtrasVariable.vrc_master, "Master Icon (MagicChatbox key)");
        CreateVariable<int>(VRCExtrasVariable.vrc_instance_capacity, "Instance Capacity (world capacity)");

        CreateState(VRCExtrasState.InInstance, "In Instance", "{0}\n{1} · {2} · {3} players", new[] { world, type, region, players });
        CreateState(VRCExtrasState.NotInInstance, "Not In Instance", string.Empty);

        CreateEvent(VRCExtrasEvent.WorldChanged, "World Changed", "Now in {0}", new[] { world });
    }

    protected override Task<bool> OnModuleStart()
    {
        _lastInstanceId = null;
        _capacityRequestedFor = string.Empty;
        _logTail ??= new VRChatLogTail(Log);
        _logTail.Start(GetSettingValue<string>(VRCExtrasSetting.VRChatLogDirectory));
        ChangeState(VRCExtrasState.NotInInstance);
        return Task.FromResult(true);
    }

    protected override Task OnModuleStop()
    {
        _logTail?.Stop();
        return Task.CompletedTask;
    }

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void Update()
    {
        var client = GetClient();
        var instance = client.IsInInstance ? client.Instance : null;
        _logTail?.Poll();

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
            SetMaster(string.Empty, false);
            SetVariableValue(VRCExtrasVariable.vrc_instance_capacity, 0);

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
        var isMaster = _logTail is { InInstance: true, IsMaster: true };

        SetVariableValue(VRCExtrasVariable.World, instance.World.Name);
        SetVariableValue(VRCExtrasVariable.WorldId, instance.World.Id);
        SetVariableValue(VRCExtrasVariable.InstanceType, TypeText(instance.Type));
        SetVariableValue(VRCExtrasVariable.Region, RegionText(instance.Region));
        SetVariableValue(VRCExtrasVariable.PlayerCount, playerCount);
        SetVariableValue(VRCExtrasVariable.InstanceId, instance.Id ?? string.Empty);
        SetVariableValue(VRCExtrasVariable.InstanceOwner, instance.OwnerId ?? string.Empty);
        SetVariableValue(VRCExtrasVariable.AgeGated, instance.AgeGated);
        SetVariableValue(VRCExtrasVariable.HasQueue, instance.HasQueue);
        SetMaster(isMaster ? GetSettingValue<string>(VRCExtrasSetting.MasterIcon) ?? string.Empty : string.Empty, isMaster);
        UpdateCapacity(instance.World.Id);

        SendParameter(VRCExtrasParameter.InInstance, true);
        SendParameter(VRCExtrasParameter.InstanceType, (int)instance.Type);
        SendParameter(VRCExtrasParameter.Region, (int)instance.Region);
        SendParameter(VRCExtrasParameter.PlayerCount, playerCount);
        SendParameter(VRCExtrasParameter.AgeGated, instance.AgeGated);
    }

    private void SetMaster(string icon, bool isMaster)
    {
        SetVariableValue(VRCExtrasVariable.MasterIcon, icon);
        SetVariableValue(VRCExtrasVariable.vrc_master, icon);
        SendParameter(VRCExtrasParameter.IsMaster, isMaster);
    }

    // ─────────────────────────── Capacity ───────────────────────────

    private void UpdateCapacity(string worldId)
    {
        if (worldId.Length == 0)
        {
            SetVariableValue(VRCExtrasVariable.vrc_instance_capacity, 0);
            return;
        }

        if (_capacityByWorld.TryGetValue(worldId, out var capacity))
        {
            SetVariableValue(VRCExtrasVariable.vrc_instance_capacity, capacity);
        }
        else if (!_capacityFetching && _capacityRequestedFor != worldId)
        {
            _capacityRequestedFor = worldId;
            _capacityFetching = true;
            _ = FetchCapacityAsync(worldId);
        }
    }

    private async Task FetchCapacityAsync(string worldId)
    {
        try
        {
            var url = string.Format(VRChatWorldUrl, Uri.EscapeDataString(worldId));
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                Log($"World lookup for {worldId} returned {(int)response.StatusCode}; capacity unavailable.");
                return;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (doc.RootElement.TryGetProperty("capacity", out var cap) && cap.ValueKind == JsonValueKind.Number)
            {
                _capacityByWorld[worldId] = cap.GetInt32();
            }
        }
        catch (HttpRequestException ex) { Log($"World lookup failed: {ex.Message}"); }
        catch (TaskCanceledException) { Log("World lookup timed out."); }
        catch (JsonException ex) { Log($"World lookup returned invalid JSON: {ex.Message}"); }
        finally { _capacityFetching = false; }
    }

    // ─────────────────────────── Text mapping ───────────────────────────

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

    private enum VRCExtrasSetting { MasterIcon, VRChatLogDirectory }

    private enum VRCExtrasParameter { InstanceType, Region, PlayerCount, InInstance, AgeGated, IsMaster }

    // The SDK derives the ChatBox lookup from the lower-cased member name; the two snake_case
    // members are spelled as the MagicChatbox placeholder keys on purpose. MasterIcon keeps its
    // original `mastericon` key so existing layouts stay valid; vrc_master is its alias.
    private enum VRCExtrasVariable
    {
        World, WorldId, InstanceType, Region, PlayerCount,
        InstanceId, InstanceOwner, AgeGated, HasQueue, MasterIcon,
        vrc_master, vrc_instance_capacity
    }

    private enum VRCExtrasState { InInstance, NotInInstance }

    private enum VRCExtrasEvent { WorldChanged }
}
