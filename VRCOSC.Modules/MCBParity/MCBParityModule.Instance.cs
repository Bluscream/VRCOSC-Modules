// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Instance part: master status and world capacity. The SDK's VRChatLogReader parses joins,
// leaves and players but neither the "I am MASTER" lines nor a capacity, and the VRChat log
// itself never prints a capacity, so the world id from the join line is looked up on the
// public world endpoint (no login needed) and cached per world.

using System.Net.Http;
using System.Text.Json;
using VRCOSC.App.ChatBox.Clips.Variables;
using VRCOSC.App.SDK.Modules;

namespace Bluscream.Modules.MCBParity;

public sealed partial class MCBParityModule
{
    private const string VRChatWorldUrl = "https://api.vrchat.cloud/api/1/worlds/{0}";

    private VRChatLogTail? _logTail;
    private readonly Dictionary<string, int> _capacityByWorld = new(StringComparer.OrdinalIgnoreCase);
    private string _capacityRequestedFor = string.Empty;
    private bool _capacityFetching;
    private bool _wasInInstance;

    private void CreateInstanceSettings()
    {
        CreateTextBox(MCBParitySetting.MasterIcon, "Instance master icon", "Value of the master variable while you are the instance master; empty otherwise.", "\U0001F451");
        CreateTextBox(MCBParitySetting.VRChatLogDirectory, "VRChat log directory",
            "Override for the folder holding output_log_*.txt, as a path VRCOSC can open (Z:\\... under Wine). Leave empty to auto-detect (own prefix's LocalLow, then every Steam library's compatdata/438100).", string.Empty);
    }

    private (ClipVariableReference Capacity, ClipVariableReference Master) CreateInstanceVariables()
    {
        var capacity = CreateVariable<int>(MCBParityVariable.vrc_instance_capacity, "Instance Capacity (world capacity)")!;
        var master = CreateVariable<string>(MCBParityVariable.vrc_master, "Instance Master Icon")!;
        return (capacity, master);
    }

    private void StartInstance()
    {
        _logTail ??= new VRChatLogTail(Log);
        _logTail.Start(GetSettingValue<string>(MCBParitySetting.VRChatLogDirectory));
        _capacityRequestedFor = string.Empty;
        _wasInInstance = false;
    }

    private void StopInstance() => _logTail?.Stop();

    [ModuleUpdate(ModuleUpdateMode.Custom, true, 1000)]
    private void UpdateInstance()
    {
        if (_logTail is null) return;
        _logTail.Poll();

        var inInstance = _logTail.InInstance;
        if (inInstance != _wasInInstance)
        {
            _wasInInstance = inInstance;
            ChangeState(inInstance ? MCBParityState.InInstance : MCBParityState.NotInInstance);
        }

        SetVariableValue(MCBParityVariable.vrc_master, inInstance && _logTail.IsMaster ? GetSettingValue<string>(MCBParitySetting.MasterIcon) ?? string.Empty : string.Empty);

        var worldId = _logTail.WorldId;
        if (worldId.Length == 0)
        {
            SetVariableValue(MCBParityVariable.vrc_instance_capacity, 0);
            return;
        }

        if (_capacityByWorld.TryGetValue(worldId, out var capacity))
        {
            SetVariableValue(MCBParityVariable.vrc_instance_capacity, capacity);
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
}
