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

        CreateGroup("Connection", "Connection settings", VRCXBridgeSetting.Enabled, VRCXBridgeSetting.AutoReconnect, VRCXBridgeSetting.ReconnectDelay);
        CreateGroup("Debug", "Debug logging options", VRCXBridgeSetting.LogOscParams, VRCXBridgeSetting.LogCommands);
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
        // Cleanup any existing connection first
        try
        {
            if (_isConnected)
            {
                DisconnectFromVRCX();
                await Task.Delay(500); // Brief delay before reconnecting
            }
        }
        catch (Exception cleanupEx)
        {
            Log($"Error during cleanup: {cleanupEx.Message}");
        }

        try
        {
            var pipeName = GetVRCXPipeName();
            _cancellationSource = new CancellationTokenSource();
            
            // Create pipe with error protection
            try
            {
                _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            }
            catch (Exception pipeEx)
            {
                throw new Exception($"Failed to create pipe client: {pipeEx.Message}", pipeEx);
            }

            Log($"Connecting to VRCX IPC ({pipeName})...");
            
            // Connect with timeout
            try
            {
                await _pipeClient.ConnectAsync(5000, _cancellationSource.Token);
            }
            catch (TimeoutException)
            {
                throw new Exception("Connection timeout - is VRCX running?");
            }
            catch (OperationCanceledException)
            {
                throw new Exception("Connection cancelled");
            }

            // Setup streams with error protection
            try
            {
                _pipeWriter = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = false };
                _pipeReader = new StreamReader(_pipeClient, Encoding.UTF8);
            }
            catch (Exception streamEx)
            {
                throw new Exception($"Failed to create streams: {streamEx.Message}", streamEx);
            }

            _isConnected = true;
            SendParameter(VRCXBridgeParameter.Connected, true);
            Log("✓ Connected to VRCX");

            // Start read task with error protection
            _readTask = Task.Run(async () =>
            {
                try
                {
                    await ReadMessages();
                }
                catch (Exception readEx)
                {
                    Log($"Read task error: {readEx.Message}");
                    if (_isConnected)
                    {
                        DisconnectFromVRCX();
                        TryReconnect();
                    }
                }
            }, _cancellationSource.Token);

            // Send INIT message to VRCX
            try
            {
                await SendToVRCX("OSC_READY", new { timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
            }
            catch (Exception initEx)
            {
                Log($"Failed to send INIT message: {initEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to connect to VRCX: {ex.Message}");
            SendParameter(VRCXBridgeParameter.Connected, false);
            _isConnected = false;

            // Cleanup on failure
            try
            {
                _pipeWriter?.Dispose();
                _pipeReader?.Dispose();
                _pipeClient?.Dispose();
                _cancellationSource?.Dispose();
            }
            catch { /* Ignore cleanup errors */ }

            TryReconnect();
        }
    }

    private void TryReconnect()
    {
        // Auto-reconnect
        if (GetSettingValue<bool>(VRCXBridgeSetting.AutoReconnect))
        {
            var delay = GetSettingValue<int>(VRCXBridgeSetting.ReconnectDelay);
            Log($"Reconnecting in {delay}ms...");
            _ = Task.Delay(delay).ContinueWith(async _ =>
            {
                try
                {
                    await ConnectToVRCX();
                }
                catch (Exception reconnectEx)
                {
                    Log($"Reconnection attempt failed: {reconnectEx.Message}");
                }
            });
        }
    }

    private void DisconnectFromVRCX()
    {
        try
        {
            _isConnected = false;
            SendParameter(VRCXBridgeParameter.Connected, false);

            // Cancel operations
            try
            {
                _cancellationSource?.Cancel();
            }
            catch (Exception cancelEx)
            {
                Log($"Error cancelling operations: {cancelEx.Message}");
            }

            // Dispose resources in reverse order
            try
            {
                _pipeWriter?.Dispose();
            }
            catch { /* Ignore */ }

            try
            {
                _pipeReader?.Dispose();
            }
            catch { /* Ignore */ }

            try
            {
                _pipeClient?.Dispose();
            }
            catch { /* Ignore */ }

            try
            {
                _cancellationSource?.Dispose();
            }
            catch { /* Ignore */ }

            _pipeWriter = null;
            _pipeReader = null;
            _pipeClient = null;
            _cancellationSource = null;

            Log("Disconnected from VRCX");
        }
        catch (Exception ex)
        {
            Log($"Error during disconnect: {ex.Message}");
        }
    }

    private async Task ReadMessages()
    {
        try
        {
            var buffer = new StringBuilder();
            var charBuffer = new char[1024];
            
            while (_isConnected && _pipeReader != null && !_cancellationSource!.Token.IsCancellationRequested)
            {
                int charsRead = 0;
                
                try
                {
                    charsRead = await _pipeReader.ReadAsync(charBuffer.AsMemory(0, charBuffer.Length), _cancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ioEx)
                {
                    Log($"Pipe read error: {ioEx.Message}");
                    break;
                }
                catch (Exception readEx)
                {
                    Log($"Unexpected read error: {readEx.Message}");
                    continue;
                }

                if (charsRead == 0)
                {
                    if (_isConnected)
                    {
                        Log("Pipe closed by VRCX");
                        break;
                    }
                    continue;
                }

                // Process characters and split on null terminators
                for (int i = 0; i < charsRead; i++)
                {
                    char c = charBuffer[i];
                    
                    if (c == '\0')
                    {
                        // End of message
                        if (buffer.Length > 0)
                        {
                            var message = buffer.ToString();
                            buffer.Clear();
                            
                            try
                            {
                                await HandleVRCXMessage(message);
                            }
                            catch (Exception handleEx)
                            {
                                Log($"Error handling message: {handleEx.Message}");
                            }
                        }
                    }
                    else
                    {
                        buffer.Append(c);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (_isConnected)
            {
                Log($"Read loop error: {ex.Message}");
            }
        }
        finally
        {
            if (_isConnected)
            {
                Log("Read loop ended");
                DisconnectFromVRCX();
                TryReconnect();
            }
        }
    }

    private async Task HandleVRCXMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Log("Received empty message");
            return;
        }

        JsonNode? json = null;
        try
        {
            json = JsonNode.Parse(message);
        }
        catch (JsonException jsonEx)
        {
            Log($"Invalid JSON received: {jsonEx.Message}");
            return;
        }

        if (json == null)
        {
            Log("Null JSON after parse");
            return;
        }

        string? msgType = null;
        string? dataStr = null;

        try
        {
            msgType = json["MsgType"]?.ToString();
            dataStr = json["Data"]?.ToString();
        }
        catch (Exception parseEx)
        {
            Log($"Error extracting message fields: {parseEx.Message}");
            return;
        }

        if (string.IsNullOrEmpty(msgType))
        {
            Log("Message missing MsgType");
            return;
        }

        JsonNode? data = null;
        if (!string.IsNullOrEmpty(dataStr))
        {
            try
            {
                data = JsonNode.Parse(dataStr);
            }
            catch (JsonException dataEx)
            {
                Log($"Error parsing Data field: {dataEx.Message}");
            }
        }

        try
        {
            switch (msgType)
            {
                case "OSC_INIT":
                    // VRCX plugin initialized
                    Log("Received OSC_INIT from VRCX");
                    await SendToVRCX("OSC_READY", new { timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds() });
                    break;

                case "OSC_SEND":
                    // Send OSC to VRChat from VRCX
                    var address = data?["address"]?.ToString();
                    var valueNode = data?["value"];

                    if (!string.IsNullOrEmpty(address) && valueNode != null)
                    {
                        try
                        {
                            await SendOSCToVRChat(address, valueNode);
                            
                            if (GetSettingValue<bool>(VRCXBridgeSetting.LogOscParams))
                            {
                                Log($"OSC → VRChat: {address} = {valueNode}");
                            }
                        }
                        catch (Exception oscEx)
                        {
                            Log($"Error sending OSC to VRChat: {oscEx.Message}");
                        }
                    }
                    break;

                case "OSC_RESPONSE":
                    // Response to our command
                    var requestId = data?["requestId"]?.ToString();
                    var result = data?["result"];

                    if (!string.IsNullOrEmpty(requestId))
                    {
                        if (_pendingRequests.TryGetValue(requestId, out var tcs))
                        {
                            try
                            {
                                tcs.SetResult(result!);
                                _pendingRequests.Remove(requestId);
                            }
                            catch (Exception tcsEx)
                            {
                                Log($"Error setting result for {requestId}: {tcsEx.Message}");
                            }
                        }
                    }
                    break;

                case "OSC_SHUTDOWN":
                    // VRCX shutting down
                    Log("VRCX shutting down");
                    DisconnectFromVRCX();
                    break;

                default:
                    Log($"Unknown message type: {msgType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Error processing {msgType}: {ex.Message}");
        }
    }

    private Task SendOSCToVRChat(string address, JsonNode value)
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
        
        return Task.CompletedTask;
    }

    private static object ParseJsonValue(JsonNode? node)
    {
        if (node == null) return string.Empty;
        
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
        if (!_isConnected || _pipeWriter == null)
        {
            Log($"Cannot send {msgType}: Not connected");
            return;
        }

        try
        {
            var ipcMessage = new
            {
                Type = "VrcxMessage",
                MsgType = msgType,
                Data = JsonSerializer.Serialize(data)
            };

            var json = JsonSerializer.Serialize(ipcMessage);
            
            // Write with null terminator (VRCX IPC format)
            var writeTask = Task.Run(async () =>
            {
                await _pipeWriter.WriteAsync(json);
                await _pipeWriter.WriteAsync('\0'); // Null terminator
                await _pipeWriter.FlushAsync();
            });
            
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(writeTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"Send timeout for {msgType}");
            }

            await writeTask;
        }
        catch (TimeoutException timeoutEx)
        {
            Log($"Send timeout: {timeoutEx.Message}");
            DisconnectFromVRCX();
            TryReconnect();
        }
        catch (IOException ioEx)
        {
            Log($"Pipe write error: {ioEx.Message}");
            DisconnectFromVRCX();
            TryReconnect();
        }
        catch (ObjectDisposedException)
        {
            Log("Pipe already disposed");
            _isConnected = false;
        }
        catch (Exception ex)
        {
            Log($"Error sending to VRCX ({msgType}): {ex.Message}");
            DisconnectFromVRCX();
            TryReconnect();
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
