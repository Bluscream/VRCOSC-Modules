// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSCModule = VRCOSC.App.SDK.Modules.Module;

namespace Bluscream.Modules.VRCXBridge;

[ModuleTitle("VRCX Bridge")]
[ModuleDescription("Bidirectional bridge between VRCOSC and VRCX for OSC + VRChat API integration")]
[ModuleType(ModuleType.Integrations)]
public class VRCXBridgeModule : VRCOSCModule
{
    private NamedPipeClientStream? _pipeClient;
    private StreamWriter? _pipeWriter;
    private StreamReader? _pipeReader;
    private Task? _readTask;
    private CancellationTokenSource? _cancellationSource;
    private bool _isConnected;
    private bool _hasLoggedDisconnection;
    private readonly Dictionary<string, TaskCompletionSource<JsonNode>> _pendingRequests = new();
    private readonly List<OscEvent> _eventBuffer = new();
    private readonly object _bufferLock = new object();
    private System.Timers.Timer? _flushTimer;
    private readonly Dictionary<string, object> _chatVariables = new();
    private readonly Dictionary<string, Type> _variableTypes = new();

    [ModulePersistent("vrcx_variables")]
    public Dictionary<string, VariableInfo> PersistedVariables { get; set; } = new();

    protected override void OnPostLoad()
    {
        // Recreate all previously created variables from persistent storage
        // This ensures ChatBox won't fail if VRCX isn't started yet
        foreach (var kvp in PersistedVariables)
        {
            var varKey = $"vrcx_{kvp.Key}";
            var varInfo = kvp.Value;
            
            try
            {
                // Create variable with correct type, initialize as empty
                switch (varInfo.TypeName)
                {
                    case "Boolean":
                        CreateVariable<bool>(varKey, string.Empty);
                        _variableTypes[kvp.Key] = typeof(bool);
                        _chatVariables[kvp.Key] = false;
                        break;
                    case "Int32":
                        CreateVariable<int>(varKey, string.Empty);
                        _variableTypes[kvp.Key] = typeof(int);
                        _chatVariables[kvp.Key] = 0;
                        break;
                    case "Single":
                        CreateVariable<float>(varKey, string.Empty);
                        _variableTypes[kvp.Key] = typeof(float);
                        _chatVariables[kvp.Key] = 0f;
                        break;
                    default: // String
                        CreateVariable<string>(varKey, string.Empty);
                        _variableTypes[kvp.Key] = typeof(string);
                        _chatVariables[kvp.Key] = string.Empty;
                        break;
                }
                
                // Set display name after creation (like Counter module does)
                GetVariable(varKey)!.DisplayName.Value = varInfo.DisplayName;
                
            }
            catch (Exception ex)
            {
                Log($"Failed to restore variable {varKey}: {ex.Message}");
            }
        }
        
        Log($"Restored {PersistedVariables.Count} variables from persistent storage");
    }

    protected override void OnPreLoad()
    {
        CreateToggle(VRCXBridgeSetting.Enabled, "Enabled", "Enable VRCX bridge", true);
        CreateToggle(VRCXBridgeSetting.AutoReconnect, "Auto Reconnect", "Automatically reconnect if connection lost", true);
        CreateTextBox(VRCXBridgeSetting.ReconnectDelay, "Reconnect Delay (ms)", "Delay before reconnect attempt", 5000);
        CreateTextBox(VRCXBridgeSetting.BatchInterval, "Batch Interval (ms)", "Collect events and send in bulk every X ms", 2000);
        CreateToggle(VRCXBridgeSetting.DeduplicateEvents, "Deduplicate Events", "Only send latest value per parameter (discard intermediate values)", true);
        CreateTextBox(VRCXBridgeSetting.IpcMessageType, "IPC Message Type", "Type wrapper for OSC bulk events (Event7List=silent, VrcxMessage=verbose)", "Event7List");
        CreateToggle(VRCXBridgeSetting.LogOscParams, "Log OSC Parameters", "Log OSC parameter changes to console", false);
        CreateToggle(VRCXBridgeSetting.LogCommands, "Log VRCX Commands", "Log commands to/from VRCX", false);
        CreateToggle(VRCXBridgeSetting.LogRawIpc, "Log Raw IPC", "Log raw IPC message traffic (very verbose)", false);

        RegisterParameter<bool>(VRCXBridgeParameter.Connected, "VRCOSC/VRCXBridge/Connected", ParameterMode.Write, "Connected", "True when connected to VRCX");

        CreateGroup("Connection", "Connection settings", VRCXBridgeSetting.Enabled, VRCXBridgeSetting.AutoReconnect, VRCXBridgeSetting.ReconnectDelay);
        CreateGroup("Performance", "Performance settings", VRCXBridgeSetting.BatchInterval, VRCXBridgeSetting.DeduplicateEvents, VRCXBridgeSetting.IpcMessageType);
        CreateGroup("Debug", "Debug logging options", VRCXBridgeSetting.LogOscParams, VRCXBridgeSetting.LogCommands, VRCXBridgeSetting.LogRawIpc);
    }

    protected override async Task<bool> OnModuleStart()
    {
        if (!GetSettingValue<bool>(VRCXBridgeSetting.Enabled))
        {
            Log("VRCX Bridge disabled in settings");
            return true;
        }

        StartFlushTimer();
        await ConnectToVRCX();
        return true;
    }

    protected override Task OnModuleStop()
    {
        StopFlushTimer();
        FlushEventBuffer();
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

            // Only log initial connection attempts, not retries
            if (!_hasLoggedDisconnection)
            {
                Log($"Connecting to VRCX IPC ({pipeName})...");
            }
            
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

            // Setup streams with error protection (UTF8 without BOM)
            try
            {
                var utf8NoBom = new UTF8Encoding(false);
                _pipeWriter = new StreamWriter(_pipeClient, utf8NoBom) { AutoFlush = false };
                _pipeReader = new StreamReader(_pipeClient, utf8NoBom);
            }
            catch (Exception streamEx)
            {
                throw new Exception($"Failed to create streams: {streamEx.Message}", streamEx);
            }

            _isConnected = true;
            SendParameter(VRCXBridgeParameter.Connected, true);
            
            // Log successful connection (especially important if we were reconnecting)
            if (_hasLoggedDisconnection)
            {
                Log("✓ Reconnected to VRCX");
                _hasLoggedDisconnection = false;
            }
            else
            {
                Log("✓ Connected to VRCX");
            }

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
            // Only log the first disconnection/failure
            if (!_hasLoggedDisconnection)
            {
                Log($"Failed to connect to VRCX: {ex.Message}");
                _hasLoggedDisconnection = true;
            }
            
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
            
            // Only log reconnect delay on first failure
            if (!_hasLoggedDisconnection)
            {
                Log($"Trying to reconnect every {delay}ms...");
                _hasLoggedDisconnection = true;
            }
            
            _ = Task.Delay(delay).ContinueWith(async _ =>
            {
                try
                {
                    await ConnectToVRCX();
                }
                catch (Exception)
                {
                    // Silently retry - errors are already logged in ConnectToVRCX
                }
            });
        }
    }

    private void DisconnectFromVRCX()
    {
        try
        {
            var wasConnected = _isConnected;
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

            // Only log intentional disconnections, not during cleanup/errors
            if (wasConnected)
            {
                Log("Disconnected from VRCX");
            }
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

        if (GetSettingValue<bool>(VRCXBridgeSetting.LogRawIpc))
        {
            var preview = message.Length > 100 ? message.Substring(0, 100) + "..." : message;
            Log($"IPC ← VRCX: {preview}");
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
            // Try MsgType first (VrcxMessage format), fall back to Type (direct IPC)
            msgType = json["MsgType"]?.ToString() ?? json["Type"]?.ToString();
            dataStr = json["Data"]?.ToString();
        }
        catch (Exception parseEx)
        {
            if (GetSettingValue<bool>(VRCXBridgeSetting.LogRawIpc))
            {
                Log($"Error extracting message fields: {parseEx.Message}");
            }
            return;
        }

        if (string.IsNullOrEmpty(msgType))
        {
            if (GetSettingValue<bool>(VRCXBridgeSetting.LogRawIpc))
            {
                Log("Message missing MsgType/Type");
            }
            return;
        }
        
        // Silently ignore non-OSC related messages
        if (!msgType.StartsWith("OSC_") && msgType != "PluginEvent" && msgType != "VRCXLaunch")
        {
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
                if (GetSettingValue<bool>(VRCXBridgeSetting.LogRawIpc))
                {
                    Log($"Error parsing Data field: {dataEx.Message}");
                }
                return; // Skip messages with invalid data
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
                        }
                        catch (Exception oscEx)
                        {
                            Log($"Error sending OSC to VRChat: {oscEx.Message}");
                        }
                    }
                    break;

                case "OSC_COMMAND":
                    // Command FROM VRCX to control VRCOSC
                    var command = data?["command"]?.ToString();
                    var cmdArgs = data?["args"];
                    var cmdRequestId = data?["requestId"]?.ToString();

                    if (!string.IsNullOrEmpty(command))
                    {
                        try
                        {
                            await HandleVRCXCommand(command, cmdArgs, cmdRequestId);
                        }
                        catch (Exception cmdEx)
                        {
                            Log($"Error handling command {command}: {cmdEx.Message}");
                        }
                    }
                    break;

                case "OSC_RESPONSE":
                    // Response to our command sent to VRCX
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

                case "PluginEvent":
                    // Plugin event broadcast from VRCX
                    if (GetSettingValue<bool>(VRCXBridgeSetting.LogCommands))
                    {
                        var pluginName = data?["pluginName"]?.ToString();
                        var eventName = data?["eventName"]?.ToString();
                        Log($"Plugin Event: {pluginName} -> {eventName}");
                    }
                    break;

                case "VRCXLaunch":
                    // VRCX startup notification
                    Log("VRCX launched");
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
            // Handle ChatBox specially
            if (address == "/chatbox/input")
            {
                if (value is JsonArray chatboxArray && chatboxArray.Count > 0)
                {
                    var text = chatboxArray[0]?.ToString() ?? "";
                    var minimalBg = chatboxArray.Count > 1 && chatboxArray[1]?.GetValue<bool>() == false;
                    
                    SendChatBox(text, minimalBg);
                    
                    if (GetSettingValue<bool>(VRCXBridgeSetting.LogCommands))
                    {
                        Log($"ChatBox: {text} (minimal: {minimalBg})");
                    }
                }
                return Task.CompletedTask;
            }

            // Handle different message types
            if (value is JsonArray array)
            {
                // Array of values - convert to object array
                var args = array.Select(ParseJsonValue).ToArray();
                
                // For avatar parameters, strip prefix and use single value
                if (address.StartsWith("/avatar/parameters/"))
                {
                    var paramName = address.Substring("/avatar/parameters/".Length);
                    SendParameter(paramName, args.Length == 1 ? args[0] : args);
                }
                else
                {
                    // For other multi-param messages, use raw OSC client
                    SendRawOSC(address, args);
                }

                if (GetSettingValue<bool>(VRCXBridgeSetting.LogCommands))
                {
                    Log($"OSC → VRChat: {address} = {JsonSerializer.Serialize(args)}");
                }
            }
            else
            {
                // Single value
                var oscValue = ParseJsonValue(value);
                
                // For avatar parameters, strip prefix
                if (address.StartsWith("/avatar/parameters/"))
                {
                    var paramName = address.Substring("/avatar/parameters/".Length);
                    SendParameter(paramName, oscValue);
                }
                else
                {
                    SendRawOSC(address, oscValue);
                }

                if (GetSettingValue<bool>(VRCXBridgeSetting.LogCommands))
                {
                    Log($"OSC → VRChat: {address} = {JsonSerializer.Serialize(oscValue)}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error sending OSC: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    private void SendChatBox(string text, bool minimalBackground)
    {
        try
        {
            // Use reflection to access ChatBoxManager.GetInstance()
            var chatBoxManagerType = System.Type.GetType("VRCOSC.App.ChatBox.ChatBoxManager, VRCOSC.App");
            if (chatBoxManagerType == null)
            {
                Log("Failed to get ChatBoxManager type - falling back to raw OSC");
                SendRawOSC("/chatbox/input", text, true, false);
                return;
            }

            var getInstanceMethod = chatBoxManagerType.GetMethod("GetInstance", BindingFlags.NonPublic | BindingFlags.Static);
            if (getInstanceMethod == null)
            {
                Log("Failed to get ChatBoxManager.GetInstance - falling back to raw OSC");
                SendRawOSC("/chatbox/input", text, true, false);
                return;
            }

            var chatBoxManager = getInstanceMethod.Invoke(null, null);
            if (chatBoxManager == null)
            {
                Log("ChatBoxManager instance is null - falling back to raw OSC");
                SendRawOSC("/chatbox/input", text, true, false);
                return;
            }

            // Set PulseText property
            var pulseTextProp = chatBoxManagerType.GetProperty("PulseText");
            if (pulseTextProp != null)
            {
                pulseTextProp.SetValue(chatBoxManager, text);
            }

            // Set PulseMinimalBackground property
            var pulseMinimalBgProp = chatBoxManagerType.GetProperty("PulseMinimalBackground");
            if (pulseMinimalBgProp != null)
            {
                pulseMinimalBgProp.SetValue(chatBoxManager, minimalBackground);
            }
        }
        catch (Exception ex)
        {
            Log($"Failed to send chatbox via ChatBoxManager: {ex.Message}");
            SendRawOSC("/chatbox/input", text, true, false);
        }
    }

    private void SendRawOSC(string address, params object[] args)
    {
        try
        {
            // Use reflection to access AppManager.GetInstance().VRChatOscClient.Send
            var appManagerType = System.Type.GetType("VRCOSC.App.Modules.AppManager, VRCOSC.App");
            if (appManagerType == null)
            {
                Log("Failed to get AppManager type");
                return;
            }

            var getInstanceMethod = appManagerType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
            if (getInstanceMethod == null)
            {
                Log("Failed to get GetInstance method");
                return;
            }

            var appManager = getInstanceMethod.Invoke(null, null);
            if (appManager == null)
            {
                Log("AppManager instance is null");
                return;
            }

            var oscClientProp = appManagerType.GetProperty("VRChatOscClient");
            if (oscClientProp == null)
            {
                Log("Failed to get VRChatOscClient property");
                return;
            }

            var oscClient = oscClientProp.GetValue(appManager);
            if (oscClient == null)
            {
                Log("OSC client is null");
                return;
            }

            // Call Send method
            var sendMethod = oscClient.GetType().GetMethod("Send", BindingFlags.Public | BindingFlags.Instance);
            if (sendMethod == null)
            {
                Log("Failed to get Send method");
                return;
            }

            // Combine address with args
            var allArgs = new object[args.Length + 1];
            allArgs[0] = address;
            Array.Copy(args, 0, allArgs, 1, args.Length);

            sendMethod.Invoke(oscClient, allArgs);
        }
        catch (Exception ex)
        {
            Log($"Failed to send raw OSC: {ex.Message}");
        }
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
        try
        {
            var paramValue = parameter.GetValue<object>();
            
            string oscType = paramValue switch
            {
                bool _ => "bool",
                int _ => "int",
                float _ => "float",
                double _ => "float",
                _ => "string"
            };
            
            var oscEvent = new OscEvent
            {
                Address = parameter.Name,
                Type = oscType,
                Value = paramValue,
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
            };

            lock (_bufferLock)
            {
                _eventBuffer.Add(oscEvent);
            }

            if (GetSettingValue<bool>(VRCXBridgeSetting.LogOscParams))
            {
                Log($"OSC ← VRChat: {parameter.Name} = {paramValue} ({oscType}) [buffered]");
            }
        }
        catch (Exception ex)
        {
            Log($"Error buffering OSC event: {ex.Message}");
        }
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
            // Use configurable message type for OSC events (Event7List=silent, VrcxMessage=verbose)
            var messageType = msgType == "OSC_RECEIVED_BULK" 
                ? (GetSettingValue<string>(VRCXBridgeSetting.IpcMessageType) ?? "Event7List")
                : "VrcxMessage";
            
            // Fallback to Event7List if empty
            if (string.IsNullOrWhiteSpace(messageType))
            {
                messageType = "Event7List";
            }
            
            var ipcMessage = new
            {
                Type = messageType,
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

    private async Task<object?> HandleVRCXCommand(string command, JsonNode? args, string? requestId)
    {
        object? result = null;

        try
        {
            switch (command)
            {
                case "GET_VARIABLE":
                    var getVarName = args?["name"]?.ToString();
                    if (!string.IsNullOrEmpty(getVarName) && _chatVariables.TryGetValue(getVarName, out var value))
                    {
                        result = new { success = true, value };
                    }
                    else
                    {
                        result = new { success = false, error = "Variable not found" };
                    }
                    break;

                case "SET_VARIABLE":
                    var setVarName = args?["name"]?.ToString();
                    var setValue = args?["value"];
                    
                    if (!string.IsNullOrEmpty(setVarName) && setValue != null)
                    {
                        var valueKind = setValue.GetValueKind();
                        var varKey = $"vrcx_{setVarName}";
                        
                        // Create variable if it doesn't exist yet
                        if (!_chatVariables.ContainsKey(setVarName))
                        {
                            try
                            {
                                Type varType;
                                string typeName;
                                
                                // Determine type from JsonValueKind
                                switch (valueKind)
                                {
                                    case JsonValueKind.True:
                                    case JsonValueKind.False:
                                        CreateVariable<bool>(varKey, string.Empty);
                                        varType = typeof(bool);
                                        typeName = "Boolean";
                                        break;
                                    case JsonValueKind.Number:
                                        if (setValue.ToString().Contains('.'))
                                        {
                                            CreateVariable<float>(varKey, string.Empty);
                                            varType = typeof(float);
                                            typeName = "Single";
                                        }
                                        else
                                        {
                                            CreateVariable<int>(varKey, string.Empty);
                                            varType = typeof(int);
                                            typeName = "Int32";
                                        }
                                        break;
                                    default:
                                        CreateVariable<string>(varKey, string.Empty);
                                        varType = typeof(string);
                                        typeName = "String";
                                        break;
                                }
                                
                                // Set display name after creation (like Counter module does)
                                GetVariable(varKey)!.DisplayName.Value = setVarName;
                                
                                _variableTypes[setVarName] = varType;
                                
                                // Persist variable info for recreation on next load
                                PersistedVariables[setVarName] = new VariableInfo
                                {
                                    DisplayName = setVarName,
                                    TypeName = typeName
                                };
                                
                                Log($"Created new ChatBox variable: {varKey} ({typeName})");
                            }
                            catch (Exception createEx)
                            {
                                Log($"Failed to create variable {varKey}: {createEx.Message}");
                                result = new { success = false, error = createEx.Message };
                                break;
                            }
                        }
                        
                        // Parse value with correct type based on JsonValueKind (not parsed type)
                        object varValue;
                        
                        switch (valueKind)
                        {
                            case JsonValueKind.True:
                            case JsonValueKind.False:
                                varValue = setValue.GetValue<bool>();
                                break;
                            case JsonValueKind.Number:
                                if (setValue.ToString().Contains('.'))
                                {
                                    varValue = setValue.GetValue<float>();
                                }
                                else
                                {
                                    varValue = setValue.GetValue<int>();
                                }
                                break;
                            default:
                                varValue = setValue.GetValue<string>();
                                break;
                        }
                        
                        _chatVariables[setVarName] = varValue;
                        
                        try
                        {
                            // Set variable with proper type casting
                            switch (valueKind)
                            {
                                case JsonValueKind.True:
                                case JsonValueKind.False:
                                    SetVariableValue(varKey, (bool)varValue);
                                    break;
                                case JsonValueKind.Number:
                                    if (setValue.ToString().Contains('.'))
                                    {
                                        SetVariableValue(varKey, (float)varValue);
                                    }
                                    else
                                    {
                                        SetVariableValue(varKey, (int)varValue);
                                    }
                                    break;
                                default:
                                    SetVariableValue(varKey, (string)varValue);
                                    break;
                            }
                            result = new { success = true };
                        }
                        catch (Exception setEx)
                        {
                            Log($"Error setting variable {varKey}: {setEx.Message}");
                            result = new { success = false, error = setEx.Message };
                        }
                    }
                    else
                    {
                        result = new { success = false, error = "Missing name or value" };
                    }
                    break;

                default:
                    result = new { success = false, error = $"Unknown command: {command}" };
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Error processing command {command}: {ex.Message}");
            result = new { success = false, error = ex.Message };
        }

        if (!string.IsNullOrEmpty(requestId))
        {
            await SendToVRCX("OSC_RESPONSE", new { requestId, result });
        }

        return result;
    }

    private void StartFlushTimer()
    {
        var interval = GetSettingValue<int>(VRCXBridgeSetting.BatchInterval);
        if (interval <= 0) interval = 2000;

        _flushTimer = new System.Timers.Timer(interval);
        _flushTimer.Elapsed += (sender, e) => FlushEventBuffer();
        _flushTimer.AutoReset = true;
        _flushTimer.Start();
        
        Log($"Started event batching (interval: {interval}ms)");
    }

    private void StopFlushTimer()
    {
        if (_flushTimer != null)
        {
            _flushTimer.Stop();
            _flushTimer.Dispose();
            _flushTimer = null;
        }
    }

    private void FlushEventBuffer()
    {
        List<OscEvent> eventsToSend;
        int originalCount;
        
        lock (_bufferLock)
        {
            if (_eventBuffer.Count == 0) return;
            
            originalCount = _eventBuffer.Count;
            
            if (GetSettingValue<bool>(VRCXBridgeSetting.DeduplicateEvents))
            {
                var deduplicated = new Dictionary<string, OscEvent>();
                foreach (var evt in _eventBuffer)
                {
                    deduplicated[evt.Address] = evt;
                }
                eventsToSend = deduplicated.Values.ToList();
            }
            else
            {
                eventsToSend = new List<OscEvent>(_eventBuffer);
            }
            
            _eventBuffer.Clear();
        }

        if (!_isConnected || eventsToSend.Count == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                if (GetSettingValue<bool>(VRCXBridgeSetting.LogOscParams))
                {
                    var dedupEnabled = GetSettingValue<bool>(VRCXBridgeSetting.DeduplicateEvents);
                    if (dedupEnabled && originalCount != eventsToSend.Count)
                    {
                        Log($"Flushing {eventsToSend.Count} OSC events to VRCX ({originalCount - eventsToSend.Count} duplicates removed)");
                    }
                    else
                    {
                        Log($"Flushing {eventsToSend.Count} OSC events to VRCX");
                    }
                }

                await SendToVRCX("OSC_RECEIVED_BULK", new { events = eventsToSend });
            }
            catch (Exception ex)
            {
                Log($"Error flushing events to VRCX: {ex.Message}");
            }
        });
    }

    private class OscEvent
    {
        public string Address { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public object? Value { get; set; }
        public long Timestamp { get; set; }
    }

    public enum VRCXBridgeSetting
    {
        Enabled,
        AutoReconnect,
        ReconnectDelay,
        BatchInterval,
        DeduplicateEvents,
        IpcMessageType,
        LogOscParams,
        LogCommands,
        LogRawIpc
    }

    public enum VRCXBridgeParameter
    {
        Connected
    }

    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]
    public class VariableInfo
    {
        [Newtonsoft.Json.JsonProperty("DisplayName")]
        public string DisplayName { get; set; } = string.Empty;
        
        [Newtonsoft.Json.JsonProperty("TypeName")]
        public string TypeName { get; set; } = "String";

        [Newtonsoft.Json.JsonConstructor]
        public VariableInfo()
        {
        }
    }
}
