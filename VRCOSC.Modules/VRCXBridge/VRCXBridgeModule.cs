// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.VRCXBridge;

[ModuleTitle("VRCX Bridge")]
[ModuleDescription("Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration")]
[ModuleType(ModuleType.Integrations)]
public class VRCXBridgeModule : Module
{
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _pipeWriter;
    private StreamReader? _pipeReader;
    private Task? _readTask;
    private CancellationTokenSource? _cancellationSource;
    private bool _isConnected;
    private readonly Dictionary<string, TaskCompletionSource<JsonNode>> _pendingRequests = new();

    protected override void OnPreLoad()
    {
        CreateToggle(VRCXBridgeSetting.Enabled, "Enabled", "Enable VRCX bridge", true);
        CreateToggle(VRCXBridgeSetting.AutoReconnect, "Auto Reconnect", "Automatically reconnect if connection lost", true);
        CreateTextBox(VRCXBridgeSetting.ReconnectDelay, "Reconnect Delay (ms)", "Delay before reconnect attempt", 5000);
        CreateToggle(VRCXBridgeSetting.LogOscParams, "Log OSC Parameters", "Log OSC parameter changes to console", false);
        CreateToggle(VRCXBridgeSetting.LogCommands, "Log VRCX Commands", "Log commands to/from VRCX", false);

        RegisterParameter<bool>(VRCXBridgeParameter.Connected, "VRCOSC/VRCXBridge/Connected", ParameterMode.Write, "Connected", "True when connected to VRCX");

        CreateGroup("Connection", VRCXBridgeSetting.Enabled, VRCXBridgeSetting.AutoReconnect, VRCXBridgeSetting.ReconnectDelay);
        CreateGroup("Debug", VRCXBridgeSetting.LogOscParams, VRCXBridgeSetting.LogCommands);
    }

    protected override async Task<bool> OnModuleStart()
    {
        if (!GetSettingValue<bool>(VRCXBridgeSetting.Enabled))
        {
            Log("VRCX Bridge disabled in settings");
            return true;
        }

        await ConnectToVRCX();
        return true;
    }

    protected override Task OnModuleStop()
    {
        DisconnectFromVRCX();
        return Task.CompletedTask;
    }

    private static string GetVRCXPipeName()
    {
        // VRCX uses username hash for pipe name: vrcx-ipc-{hash}
        var hash = 0;
        foreach (var c in Environment.UserName)
        {
            hash += c;
        }
        return $"vrcx-ipc-{hash}";
    }

    private async Task ConnectToVRCX()
    {
        try
        {
            var pipeName = GetVRCXPipeName();
            _cancellationSource = new CancellationTokenSource();
            _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            Log($"Connecting to VRCX IPC ({pipeName})...");
            await _pipeClient.ConnectAsync(5000, _cancellationSource.Token);

            _pipeWriter = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = true };
            _pipeReader = new StreamReader(_pipeClient, Encoding.UTF8);

            _isConnected = true;
            SendParameter(VRCXBridgeParameter.Connected, true);
            Log("✓ Connected to VRCX");

            // Start read task
            _readTask = Task.Run(ReadMessages, _cancellationSource.Token);

            // Send INIT message to VRCX
            await SendToVRCX("OSC_READY", new { timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
        }
        catch (Exception ex)
        {
            Log($"Failed to connect to VRCX: {ex.Message}");
            SendParameter(VRCXBridgeParameter.Connected, false);
            _isConnected = false;

            // Auto-reconnect
            if (GetSettingValue<bool>(VRCXBridgeSetting.AutoReconnect))
            {
                var delay = GetSettingValue<int>(VRCXBridgeSetting.ReconnectDelay);
                _ = Task.Delay(delay).ContinueWith(_ => ConnectToVRCX());
            }
        }
    }

    private void DisconnectFromVRCX()
    {
        _cancellationSource?.Cancel();
        _pipeWriter?.Dispose();
        _pipeReader?.Dispose();
        _pipeClient?.Dispose();
        _isConnected = false;
        SendParameter(VRCXBridgeParameter.Connected, false);
        Log("Disconnected from VRCX");
    }

    private async Task ReadMessages()
    {
        try
        {
            while (_isConnected && _pipeReader != null && !_cancellationSource!.Token.IsCancellationRequested)
            {
                var line = await _pipeReader.ReadLineAsync(_cancellationSource.Token);
                if (string.IsNullOrEmpty(line)) continue;

                await HandleVRCXMessage(line);
            }
        }
        catch (Exception ex)
        {
            if (_isConnected)
            {
                Log($"Read error: {ex.Message}");
                DisconnectFromVRCX();

                if (GetSettingValue<bool>(VRCXBridgeSetting.AutoReconnect))
                {
                    await Task.Delay(GetSettingValue<int>(VRCXBridgeSetting.ReconnectDelay));
                    await ConnectToVRCX();
                }
            }
        }
    }

    private async Task HandleVRCXMessage(string message)
    {
        try
        {
            var json = JsonNode.Parse(message);
            if (json == null) return;

            var msgType = json["MsgType"]?.ToString();
            var dataStr = json["Data"]?.ToString();

            if (string.IsNullOrEmpty(msgType) || string.IsNullOrEmpty(dataStr)) return;

            var data = JsonNode.Parse(dataStr);

            switch (msgType)
            {
                case "OSC_INIT":
                    // VRCX plugin initialized
                    await SendToVRCX("OSC_READY", new { timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                    break;

                case "OSC_SEND":
                    // Send OSC to VRChat from VRCX
                    var address = data?["address"]?.ToString();
                    var valueNode = data?["value"];

                    if (!string.IsNullOrEmpty(address) && valueNode != null)
                    {
                        await SendOSCToVRChat(address, valueNode);
                        
                        if (GetSettingValue<bool>(VRCXBridgeSetting.LogOscParams))
                        {
                            Log($"OSC → VRChat: {address} = {valueNode}");
                        }
                    }
                    break;

                case "OSC_RESPONSE":
                    // Response to our command
                    var requestId = data?["requestId"]?.ToString();
                    var result = data?["result"];

                    if (!string.IsNullOrEmpty(requestId) && _pendingRequests.TryGetValue(requestId, out var tcs))
                    {
                        tcs.SetResult(result!);
                        _pendingRequests.Remove(requestId);
                    }
                    break;

                case "OSC_SHUTDOWN":
                    // VRCX shutting down
                    DisconnectFromVRCX();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Error handling VRCX message: {ex.Message}");
        }
    }

    private async Task SendOSCToVRChat(string address, JsonNode value)
    {
        try
        {
            // Parse value based on type
            object oscValue;

            if (value is JsonArray array)
            {
                // Array of values
                oscValue = array.Select(ParseJsonValue).ToArray();
            }
            else
            {
                // Single value
                oscValue = ParseJsonValue(value);
            }

            // Send via VRCOSC's OSC system
            SendParameter(address, oscValue);

            if (GetSettingValue<bool>(VRCXBridgeSetting.LogCommands))
            {
                Log($"OSC → VRChat: {address} = {JsonSerializer.Serialize(oscValue)}");
            }
        }
        catch (Exception ex)
        {
            Log($"Error sending OSC: {ex.Message}");
        }
    }

    private static object ParseJsonValue(JsonNode node)
    {
        return node.GetValueKind() switch
        {
            JsonValueKind.String => node.ToString(),
            JsonValueKind.Number => node.GetValue<float>(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => node.ToString()
        };
    }

    protected override void OnAnyParameterReceived(VRChatParameter parameter)
    {
        // Forward all OSC parameters from VRChat to VRCX
        _ = Task.Run(async () =>
        {
            try
            {
                var paramValue = parameter.GetValue<object>();
                
                // Determine OSC type
                string oscType = paramValue switch
                {
                    bool _ => "bool",
                    int _ => "int",
                    float _ => "float",
                    double _ => "float",
                    _ => "string"
                };
                
                if (GetSettingValue<bool>(VRCXBridgeSetting.LogOscParams))
                {
                    Log($"OSC ← VRChat: {parameter.Name} = {paramValue} ({oscType})");
                }

                await SendToVRCX("OSC_RECEIVED", new
                {
                    payload = new
                    {
                        address = parameter.Name,
                        type = oscType,
                        value = paramValue
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"Error forwarding OSC to VRCX: {ex.Message}");
            }
        });
    }

    private async Task SendToVRCX(string msgType, object data)
    {
        if (!_isConnected || _pipeWriter == null) return;

        try
        {
            var ipcMessage = new
            {
                Type = "VrcxMessage",
                MsgType = msgType,
                Data = JsonSerializer.Serialize(data)
            };

            var json = JsonSerializer.Serialize(ipcMessage);
            await _pipeWriter.WriteLineAsync(json);
        }
        catch (Exception ex)
        {
            Log($"Error sending to VRCX: {ex.Message}");
            DisconnectFromVRCX();
        }
    }

    // Public API for other modules to use
    public async Task<JsonNode?> SendCommandToVRCX(string command, object args, int timeoutMs = 5000)
    {
        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<JsonNode>();
        _pendingRequests[requestId] = tcs;

        await SendToVRCX("OSC_COMMAND", new
        {
            command,
            args,
            requestId
        });

        // Wait for response with timeout
        var timeoutTask = Task.Delay(timeoutMs);
        var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _pendingRequests.Remove(requestId);
            throw new TimeoutException($"Command {command} timed out after {timeoutMs}ms");
        }

        return await tcs.Task;
    }

    // Helper methods for common VRCX operations
    public async Task<JsonNode?> GetOnlineFriends()
    {
        return await SendCommandToVRCX("GET_ONLINE_FRIENDS", new { });
    }

    public async Task<JsonNode?> SendInvite(string userId, string instanceId, string worldId, string worldName, string? message = null)
    {
        return await SendCommandToVRCX("SEND_INVITE", new { userId, instanceId, worldId, worldName, message });
    }

    public async Task<JsonNode?> GetUserInfo(string userId)
    {
        return await SendCommandToVRCX("GET_USER_INFO", new { userId });
    }

    public async Task<JsonNode?> GetCurrentLocation()
    {
        return await SendCommandToVRCX("GET_CURRENT_LOCATION", new { });
    }

    public async Task<JsonNode?> ShowVRCXToast(string message, string type = "info")
    {
        return await SendCommandToVRCX("SHOW_TOAST", new { message, type });
    }

    public enum VRCXBridgeSetting
    {
        Enabled,
        AutoReconnect,
        ReconnectDelay,
        LogOscParams,
        LogCommands
    }

    public enum VRCXBridgeParameter
    {
        Connected
    }
}
