// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes.Types;
using VRCOSC.App.SDK.Parameters;
using Bluscream;

namespace Bluscream.Modules;

[ModuleTitle("HomeAssistant")]
[ModuleDescription("Integrate Home Assistant entity states, Jinja templates, avatar parameters, and flow nodes")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class HomeAssistantModule : Module
{
    private HomeAssistantClient? _client;
    private readonly HashSet<string> _registeredDynamicVars = new();
    private readonly Dictionary<int, string> _wsTemplateVarMap = new();

    private static readonly string[] RecognizedDomains = new[]
    {
        "light", "switch", "binary_sensor", "sensor", "cover", "climate", "media_player",
        "number", "input_boolean", "input_number", "select", "input_select", "lock",
        "fan", "scene", "script", "automation", "button"
    };

    [ModulePersistent("ha_states_cache")]
    public Dictionary<string, string> CachedStates { get; set; } = new();

    private readonly Dictionary<string, HomeAssistant.HAEntityStateSnapshot> _entityStatesSnapshot = new(StringComparer.OrdinalIgnoreCase);

    private void UpdateEntityStateSnapshot(string entityId, string state, Dictionary<string, object?>? attributes = null)
    {
        if (string.IsNullOrWhiteSpace(entityId)) return;
        var key = entityId.Trim().ToLowerInvariant();
        if (!_entityStatesSnapshot.TryGetValue(key, out var snapshot))
        {
            snapshot = new HomeAssistant.HAEntityStateSnapshot();
            _entityStatesSnapshot[key] = snapshot;
        }
        snapshot.State = state;
        if (attributes != null)
        {
            foreach (var pair in attributes)
            {
                snapshot.Attributes[pair.Key] = pair.Value;
            }
        }
        SetVariableValue(HomeAssistantVariable.EntityState, _entityStatesSnapshot);
    }

    protected override void OnPreLoad()
    {
        // Settings
        CreateTextBox(HomeAssistantSetting.ServerUrl, "Server URL", "Home Assistant base URL (e.g. http://192.168.1.100:8123)", "http://homeassistant.local:8123");
        CreateTextBox(HomeAssistantSetting.AccessToken, "Access Token", "Long-Lived Access Token generated in Home Assistant profile", string.Empty);
        CreateTextBox(HomeAssistantSetting.OscPrefix, "OSC Prefix", "Prefix for Home Assistant avatar parameters (e.g. HomeAssistant/)", "HomeAssistant/");
        CreateToggle(HomeAssistantSetting.AllowAnywhereOscPrefix, "Match OSC Prefix Anywhere", "Allow matching the OSC prefix anywhere in parameter paths to support generator prefixes (e.g. VRCFury's VF52_..._OSC/HomeAssistant/). If disabled, parameters must start with the exact prefix.", true);
        CreateToggle(HomeAssistantSetting.EnableWebSocket, "Enable Realtime WebSocket", "Enable real-time state change updates via WebSocket API", true);
        CreateToggle(HomeAssistantSetting.LogDebug, "Log Debug", "Log detailed Home Assistant debug messages to console", false);
        CreateToggle(HomeAssistantSetting.LogOscParams, "Log OSC Parameters", "Log incoming/outgoing Home Assistant OSC parameters to console", false);
        CreateTextBox(HomeAssistantSetting.EntityFilter, "Entity Filter", "Comma-separated list of entity IDs or domains to track (leave empty for all)", string.Empty);

        // Opt-in toggle to register all HA entities as individual ChatBox variables
        CreateToggle(HomeAssistantSetting.RegisterAllEntityVariables, "Register All Entity Variables", "Register every HA entity state as an individual ChatBox variable (HAState.{entity_id}). Disabled by default to prevent cluttering.", false);

        // Custom Key-Value Pair List for Jinja Template ChatBox Variables
        CreateKeyValuePairList(
            HomeAssistantSetting.TemplateVariables,
            "Custom ChatBox Template Variables",
            "Configure custom ChatBox variables mapped to Jinja templates or entity states.\nKey: Variable Name (e.g. LivingRoomTemp)\nValue: Jinja Template (e.g. {{ states('sensor.living_room_temp') }}°C)",
            Array.Empty<MutableKeyValuePair>(),
            "Variable Name",
            "Jinja Template / Entity ID"
        );

        // Parameters
        RegisterParameter<bool>(HomeAssistantParameter.Connected, "VRCOSC/HomeAssistant/Connected", ParameterMode.Write, "Connected", "True when connected to Home Assistant");
        RegisterParameter<bool>(HomeAssistantParameter.EventReceived, "VRCOSC/HomeAssistant/EventReceived", ParameterMode.Write, "Event Received", "True for 1 second when a state change event is received");
        RegisterParameter<bool>(HomeAssistantParameter.Failed, "VRCOSC/HomeAssistant/Failed", ParameterMode.Write, "Failed", "True for 1 second when a request or connection fails");

        // Settings Groups
        CreateGroup("Connection", "Home Assistant Connection Settings", HomeAssistantSetting.ServerUrl, HomeAssistantSetting.AccessToken, HomeAssistantSetting.EnableWebSocket);
        CreateGroup("Custom Variables", "Custom Jinja Template ChatBox Variables", HomeAssistantSetting.RegisterAllEntityVariables, HomeAssistantSetting.TemplateVariables);
        CreateGroup("OSC Configuration", "OSC Parameter Integration", HomeAssistantSetting.OscPrefix, HomeAssistantSetting.AllowAnywhereOscPrefix, HomeAssistantSetting.EntityFilter);
        CreateGroup("Debug", "Debug & Logging Options", HomeAssistantSetting.LogDebug, HomeAssistantSetting.LogOscParams);
    }

    protected override void OnPostLoad()
    {
        // Static Clip Variables
        CreateVariable<bool>(HomeAssistantVariable.Connected, "Connected");
        CreateVariable<string>(HomeAssistantVariable.LastEntity, "Last Entity");
        CreateVariable<string>(HomeAssistantVariable.LastState, "Last State");
        CreateVariable<int>(HomeAssistantVariable.StatesCount, "States Count");
        CreateVariable<object>(HomeAssistantVariable.EntityState, "Entity State / Attribute", typeof(HomeAssistant.HomeAssistantEntityClipVariable));

        // Register Template Variables so ChatBoxManager recognizes them before loading timeline clips
        var templateVars = GetSettingValue<List<MutableKeyValuePair>>(HomeAssistantSetting.TemplateVariables);
        if (templateVars != null)
        {
            foreach (var item in templateVars)
            {
                var varName = item.Key?.Value?.Trim();
                if (!string.IsNullOrEmpty(varName))
                {
                    EnsureCustomVariable($"HATemplate.{varName}", varName, string.Empty);
                }
            }
        }

        // Pre-register HAState entity variables so ChatBoxManager recognizes them before timeline clips deserialize
        string[] preRegisterEntities = new[]
        {
            "sensor.room_temperature",
            "sensor.room_humidity",
            "weather.home",
            "sensor.home_temperature",
            "sensor.home_humidity",
            "sensor.toy"
        };
        foreach (var entityId in preRegisterEntities)
        {
            EnsureDynamicVariable(entityId, string.Empty);
        }

        // Module States
        CreateState(HomeAssistantState.Disconnected, "Disconnected", "HA Disconnected");
        CreateState(HomeAssistantState.Connecting, "Connecting", "HA Connecting...");
        CreateState(HomeAssistantState.Connected, "Connected", "HA Connected ({0})", new[] { GetVariable(HomeAssistantVariable.StatesCount)! });
        CreateState(HomeAssistantState.Error, "Error", "HA Error");

        // Events
        CreateEvent(HomeAssistantEvent.OnStateChanged, "On State Changed", "HA {0} = {1}", new[] { GetVariable(HomeAssistantVariable.LastEntity)!, GetVariable(HomeAssistantVariable.LastState)! });
        CreateEvent(HomeAssistantEvent.OnServiceExecuted, "On Service Executed");
        CreateEvent(HomeAssistantEvent.OnError, "On Error");
    }

    protected override async Task<bool> OnModuleStart()
    {
        ChangeState(HomeAssistantState.Connecting);

        var serverUrl = GetSettingValue<string>(HomeAssistantSetting.ServerUrl);
        var token = GetSettingValue<string>(HomeAssistantSetting.AccessToken);

        if (serverUrl.IsNullOrEmpty() || token.IsNullOrEmpty())
        {
            Log("Server URL or Access Token is not set. Please configure the module settings.");
            ChangeState(HomeAssistantState.Error);
            SendParameter(HomeAssistantParameter.Connected, false);
            SetVariableValue(HomeAssistantVariable.Connected, false);
            return false;
        }

        _client = new HomeAssistantClient(Log, LogDebug);
        _client.OnStateChanged += HandleStateChanged;
        _client.OnTemplateRendered += HandleTemplateRendered;
        _client.OnConnectionStatusChanged += HandleConnectionStatusChanged;

        if (!_client.Initialize(serverUrl, token))
        {
            ChangeState(HomeAssistantState.Error);
            SendParameter(HomeAssistantParameter.Connected, false);
            SetVariableValue(HomeAssistantVariable.Connected, false);
            return false;
        }

        // Fetch initial states REST
        var initialStates = await _client.GetStatesAsync();
        if (initialStates != null)
        {
            int count = 0;
            bool registerAll = GetSettingValue<bool>(HomeAssistantSetting.RegisterAllEntityVariables);
            foreach (var state in initialStates)
            {
                if (state != null && !state.EntityId.IsNullOrEmpty())
                {
                    CachedStates[state.EntityId] = state.State ?? string.Empty;
                    UpdateEntityStateSnapshot(state.EntityId, state.State ?? string.Empty, state.Attributes);
                    if (registerAll)
                    {
                        EnsureDynamicVariable(state.EntityId, state.State ?? string.Empty);
                    }
                    count++;
                }
            }
            SetVariableValue(HomeAssistantVariable.StatesCount, count);
        }

        if (GetSettingValue<bool>(HomeAssistantSetting.EnableWebSocket))
        {
            await _client.StartWebSocketAsync(serverUrl, token);
        }
        else
        {
            HandleConnectionStatusChanged(true);
        }

        // Initialize Custom Key-Value Template Variables
        await InitializeTemplateVariables();

        return true;
    }

    protected override Task OnModuleStop()
    {
        if (_client != null)
        {
            _client.OnStateChanged -= HandleStateChanged;
            _client.OnTemplateRendered -= HandleTemplateRendered;
            _client.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            _client.StopWebSocket();
            _client = null;
        }

        _wsTemplateVarMap.Clear();
        SendParameter(HomeAssistantParameter.Connected, false);
        SetVariableValue(HomeAssistantVariable.Connected, false);
        ChangeState(HomeAssistantState.Disconnected);

        return Task.CompletedTask;
    }

    private async Task InitializeTemplateVariables()
    {
        if (_client == null) return;

        var templateVars = GetSettingValue<List<MutableKeyValuePair>>(HomeAssistantSetting.TemplateVariables);
        if (templateVars == null || templateVars.Count == 0) return;

        foreach (var item in templateVars)
        {
            var varName = item.Key?.Value?.Trim();
            var template = item.Value?.Value?.Trim();

            if (string.IsNullOrEmpty(varName) || string.IsNullOrEmpty(template)) continue;

            // Register ChatBox variable
            var varKey = $"HATemplate.{varName}";
            EnsureCustomVariable(varKey, varName, string.Empty);

            // Initial REST render
            var initialRender = await _client.RenderTemplateAsync(template, varName);
            SetVariableValue(varKey, initialRender ?? string.Empty);

            // Subscribe live WebSocket render if WebSocket is active
            if (_client.IsConnected)
            {
                int subId = await _client.SubscribeRenderTemplateAsync(template);
                if (subId > 0)
                {
                    _wsTemplateVarMap[subId] = varKey;
                    LogDebug($"Subscribed custom variable '{varName}' (WS ID: {subId}) to template '{template}'");
                }
            }
        }
    }

    private void HandleConnectionStatusChanged(bool connected)
    {
        SendParameter(HomeAssistantParameter.Connected, connected);
        SetVariableValue(HomeAssistantVariable.Connected, connected);

        if (connected)
        {
            ChangeState(HomeAssistantState.Connected);
            Log("Home Assistant Module Connected.");
        }
        else
        {
            ChangeState(HomeAssistantState.Disconnected);
            Log("Home Assistant Module Disconnected.");
        }
    }

    private void HandleStateChanged(string entityId, string newState, JsonElement attributes)
    {
        if (!IsEntityAllowed(entityId)) return;

        CachedStates[entityId] = newState;

        SetVariableValue(HomeAssistantVariable.LastEntity, entityId);
        SetVariableValue(HomeAssistantVariable.LastState, newState);

        Dictionary<string, object?>? attrDict = null;
        if (attributes.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            attrDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in attributes.EnumerateObject())
            {
                attrDict[prop.Name] = prop.Value.ToString();
            }
        }
        UpdateEntityStateSnapshot(entityId, newState, attrDict);

        if (GetSettingValue<bool>(HomeAssistantSetting.RegisterAllEntityVariables))
        {
            EnsureDynamicVariable(entityId, newState);
        }

        TriggerEvent(HomeAssistantEvent.OnStateChanged);

        // Push update to VRChat OSC Parameter
        PushEntityToOscParameter(entityId, newState, attributes);
    }

    private void HandleTemplateRendered(int subId, string renderedText)
    {
        if (_wsTemplateVarMap.TryGetValue(subId, out var varKey))
        {
            SetVariableValue(varKey, renderedText);
            LogDebug($"Updated template variable '{varKey}' = '{renderedText}'");
        }
    }

    private void PushEntityToOscParameter(string entityId, string state, JsonElement attributes)
    {
        var prefix = (GetSettingValue<string>(HomeAssistantSetting.OscPrefix) ?? string.Empty).TrimEnd('/') + "/";
        var paramNameUnderscore = prefix + entityId.Replace('.', '_');
        var paramNameSlash = prefix + entityId.Replace('.', '/');

        bool isOn = string.Equals(state, "on", StringComparison.OrdinalIgnoreCase);
        SendParameter(paramNameUnderscore, isOn);
        SendParameter(paramNameSlash, isOn);

        if (attributes.ValueKind == JsonValueKind.Object)
        {
            // Brightness (0..255)
            if (attributes.TryGetProperty("brightness", out var brightProp) && brightProp.TryGetInt32(out int brightness))
            {
                float floatBright = Math.Clamp(brightness / 255.0f, 0.0f, 1.0f);
                SendParameter(paramNameUnderscore + "/brightness", floatBright);
                SendParameter(paramNameUnderscore + "/brightness_int", brightness);
                SendParameter(paramNameSlash + "/brightness", floatBright);
                SendParameter(paramNameSlash + "/brightness_int", brightness);
            }
            // Volume Level (0.0..1.0)
            if (attributes.TryGetProperty("volume_level", out var volProp) && volProp.TryGetSingle(out float volume))
            {
                SendParameter(paramNameUnderscore + "/volume", volume);
                SendParameter(paramNameSlash + "/volume", volume);
            }
            // Position (0..100)
            if (attributes.TryGetProperty("current_position", out var posProp) && posProp.TryGetInt32(out int position))
            {
                float floatPos = Math.Clamp(position / 100.0f, 0.0f, 1.0f);
                SendParameter(paramNameUnderscore + "/position", floatPos);
                SendParameter(paramNameSlash + "/position", floatPos);
            }
        }

        if (GetSettingValue<bool>(HomeAssistantSetting.LogOscParams))
        {
            LogDebug($"Pushed HA -> OSC: {paramNameSlash} & {paramNameUnderscore} = {isOn} ({state})");
        }
    }

    protected override void OnAnyParameterReceived(VRChatParameter parameter)
    {
        if (!Bluscream.ModuleUtils.IsStarted() || _client == null) return;

        var prefix = (GetSettingValue<string>(HomeAssistantSetting.OscPrefix) ?? string.Empty).TrimEnd('/');
        var rawName = parameter.Name;

        // Check if parameter matches HA prefix
        string path = string.Empty;
        bool allowAnywhere = GetSettingValue<bool>(HomeAssistantSetting.AllowAnywhereOscPrefix);

        if (allowAnywhere)
        {
            var idx = rawName.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                path = rawName[(idx + prefix.Length)..].TrimStart('/');
            }
            else
            {
                return;
            }
        }
        else
        {
            if (rawName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = rawName[prefix.Length..].TrimStart('/');
            }
            else if (rawName.StartsWith("/avatar/parameters/" + prefix, StringComparison.OrdinalIgnoreCase))
            {
                path = rawName[("/avatar/parameters/" + prefix).Length..].TrimStart('/');
            }
            else
            {
                return;
            }
        }

        if (path.IsNullOrEmpty()) return;

        if (GetSettingValue<bool>(HomeAssistantSetting.LogOscParams))
        {
            LogDebug($"Received OSC Parameter: {rawName} = {parameter.Value}");
        }

        _ = Task.Run(() => ProcessOscParameterInput(path, parameter));
    }

    private async Task ProcessOscParameterInput(string path, VRChatParameter parameter)
    {
        try
        {
            var (entityId, domain, attribute) = ParseOscPath(path);
            if (entityId.IsNullOrEmpty() || domain.IsNullOrEmpty()) return;

            // Handle specific sub-attributes (e.g. HomeAssistant/light/closet_led/brightness)
            if (!attribute.IsNullOrEmpty())
            {
                var attrLower = attribute.ToLowerInvariant();

                if (attrLower == "brightness" || attrLower == "brightness_int")
                {
                    int brightness = 0;
                    if (parameter.Value is float fVal)
                    {
                        brightness = attrLower == "brightness_int" ? (int)Math.Clamp(fVal, 0, 255) : (int)Math.Round(Math.Clamp(fVal, 0.0f, 1.0f) * 255);
                    }
                    else if (parameter.Value is int iVal)
                    {
                        brightness = Math.Clamp(iVal, 0, 255);
                    }
                    else if (parameter.Value is bool bVal)
                    {
                        if (!bVal)
                        {
                            await CallService(domain, "turn_off", entityId);
                        }
                        else
                        {
                            var data = new Dictionary<string, object> { { "brightness", 255 } };
                            await CallService(domain, "turn_on", entityId, data);
                        }
                        return;
                    }

                    if (brightness <= 0)
                    {
                        await CallService(domain, "turn_off", entityId);
                    }
                    else
                    {
                        var data = new Dictionary<string, object> { { "brightness", brightness } };
                        await CallService(domain, "turn_on", entityId, data);
                    }
                    return;
                }
                else if (attrLower == "volume" || attrLower == "volume_level")
                {
                    float volume = 0f;
                    if (parameter.Value is float fVal) volume = Math.Clamp(fVal, 0.0f, 1.0f);
                    else if (parameter.Value is int iVal) volume = Math.Clamp(iVal / 100.0f, 0.0f, 1.0f);

                    var data = new Dictionary<string, object> { { "volume_level", volume } };
                    await CallService(domain, "volume_set", entityId, data);
                    return;
                }
                else if (attrLower == "position")
                {
                    int position = 0;
                    if (parameter.Value is float fVal) position = (int)Math.Round(Math.Clamp(fVal, 0.0f, 1.0f) * 100);
                    else if (parameter.Value is int iVal) position = Math.Clamp(iVal, 0, 100);

                    var data = new Dictionary<string, object> { { "position", position } };
                    await CallService(domain, "set_cover_position", entityId, data);
                    return;
                }
            }

            // Default entity-level parameter handling
            if (parameter.Value is bool boolVal)
            {
                string service = boolVal ? "turn_on" : "turn_off";
                if (domain == "lock") service = boolVal ? "lock" : "unlock";
                if (domain == "cover") service = boolVal ? "open_cover" : "close_cover";

                await CallService(domain, service, entityId);
            }
            else if (parameter.Value is float floatVal)
            {
                if (domain == "light")
                {
                    if (floatVal <= 0.0f)
                    {
                        await CallService("light", "turn_off", entityId);
                    }
                    else
                    {
                        int brightness = (int)Math.Round(Math.Clamp(floatVal, 0.0f, 1.0f) * 255);
                        var data = new Dictionary<string, object> { { "brightness", brightness } };
                        await CallService("light", "turn_on", entityId, data);
                    }
                }
                else if (domain == "cover")
                {
                    int position = (int)Math.Round(Math.Clamp(floatVal, 0.0f, 1.0f) * 100);
                    var data = new Dictionary<string, object> { { "position", position } };
                    await CallService("cover", "set_cover_position", entityId, data);
                }
                else if (domain == "media_player")
                {
                    var data = new Dictionary<string, object> { { "volume_level", floatVal } };
                    await CallService("media_player", "volume_set", entityId, data);
                }
                else
                {
                    string service = floatVal > 0.0f ? "turn_on" : "turn_off";
                    await CallService(domain, service, entityId);
                }
            }
            else if (parameter.Value is int intVal)
            {
                if (domain == "light")
                {
                    if (intVal <= 0)
                    {
                        await CallService("light", "turn_off", entityId);
                    }
                    else
                    {
                        int brightness = Math.Clamp(intVal, 0, 255);
                        var data = new Dictionary<string, object> { { "brightness", brightness } };
                        await CallService("light", "turn_on", entityId, data);
                    }
                }
                else
                {
                    string service = intVal > 0 ? "turn_on" : "turn_off";
                    await CallService(domain, service, entityId);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error processing OSC parameter input for {path}: {ex.Message}");
        }
    }

    private (string EntityId, string Domain, string? Attribute) ParseOscPath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (string.Empty, string.Empty, null);

        // Case 1: Slash format domain/object_id[/attribute] (e.g. switch/esphome_blus_room_flood_light_relais or light/desk_lamp/brightness)
        if (parts.Length >= 2 && RecognizedDomains.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
        {
            var dom = parts[0];
            var objId = parts[1];
            var entityId = $"{dom}.{objId}";
            string? attr = parts.Length >= 3 ? parts[2] : null;
            return (entityId, dom, attr);
        }

        // Case 2: Dot format or underscore format with potential attribute suffix (e.g. switch.my_switch or light_desk_lamp/brightness)
        string entityPath = parts[0];
        string? attribute = parts.Length >= 2 ? parts[1] : null;

        var (resolvedId, resolvedDom) = ResolveEntityIdAndDomain(entityPath);
        return (resolvedId, resolvedDom, attribute);
    }

    private (string EntityId, string Domain) ResolveEntityIdAndDomain(string entityPath)
    {
        if (entityPath.Contains('.'))
        {
            var parts = entityPath.Split('.', 2);
            var dom = RecognizedDomains.FirstOrDefault(d => d.Equals(parts[0], StringComparison.OrdinalIgnoreCase)) ?? parts[0];
            return (entityPath, dom);
        }

        foreach (var dom in RecognizedDomains)
        {
            if (entityPath.StartsWith(dom + "_", StringComparison.OrdinalIgnoreCase))
            {
                string objId = entityPath[(dom.Length + 1)..];
                return ($"{dom}.{objId}", dom);
            }
        }

        return (string.Empty, string.Empty);
    }

    private void EnsureDynamicVariable(string entityId, string stateValue)
    {
        var varKey = $"HAState.{entityId}";
        EnsureCustomVariable(varKey, $"HA State {entityId}", stateValue);
    }

    private void EnsureCustomVariable(string varKey, string displayName, string initialValue)
    {
        if (!_registeredDynamicVars.Contains(varKey))
        {
            CreateVariable<string>(varKey, displayName);
            _registeredDynamicVars.Add(varKey);
        }
        SetVariableValue(varKey, initialValue);
    }

    private bool IsEntityAllowed(string entityId)
    {
        var filter = GetSettingValue<string>(HomeAssistantSetting.EntityFilter);
        if (filter.IsNullOrEmpty()) return true;

        var items = filter.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return items.Any(item => entityId.Equals(item, StringComparison.OrdinalIgnoreCase) || entityId.StartsWith(item + ".", StringComparison.OrdinalIgnoreCase));
    }

    #region Public Helper API for Flow Nodes

    public async Task<bool> CallService(string domain, string service, string? entityId = null, object? serviceData = null)
    {
        if (_client == null) return false;
        var success = await _client.CallServiceAsync(domain, service, entityId, serviceData);
        if (success)
            TriggerEvent(HomeAssistantEvent.OnServiceExecuted);
        else
            TriggerEvent(HomeAssistantEvent.OnError);
        return success;
    }

    public async Task<(string State, bool IsOn, Dictionary<string, object> Attributes, string AttributesJson)> GetEntityStateDetails(string entityId)
    {
        var emptyDict = new Dictionary<string, object>();
        if (_client == null) return (string.Empty, false, emptyDict, "{}");

        var stateObj = await _client.GetStateAsync(entityId);
        if (stateObj == null) return (string.Empty, false, emptyDict, "{}");

        string stateStr = stateObj.State ?? string.Empty;
        bool isOn = string.Equals(stateStr, "on", StringComparison.OrdinalIgnoreCase);
        var attrDict = stateObj.Attributes ?? emptyDict;
        string attrJson = JsonSerializer.Serialize(attrDict);

        return (stateStr, isOn, attrDict, attrJson);
    }

    public async Task<string> RenderTemplate(string template)
    {
        if (_client == null) return "[Error: Client not initialized]";
        return await _client.RenderTemplateAsync(template);
    }

    #endregion
}
