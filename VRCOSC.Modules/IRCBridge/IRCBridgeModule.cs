// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using Module = VRCOSC.App.SDK.Modules.Module;

namespace Bluscream.Modules;

[ModuleTitle("IRC Bridge")]
[ModuleDescription("Connect to IRC servers and receive events for channel activity")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class IRCBridgeModule : Module
{
    private IRCClient? _ircClient;
    private IRCMessageHandler? _messageHandler;

    protected override void OnPreLoad()
    {
        // Server configuration
        CreateTextBox(IRCBridgeSetting.ServerAddress, "Server Address", "IRC server address (e.g., irc.example.com)", "irc.efnet.org");
        CreateTextBox(IRCBridgeSetting.ServerPort, "Server Port", "IRC server port (typically 6667 for non-SSL, 6697 for SSL)", 6667);
        CreateToggle(IRCBridgeSetting.UseSSL, "Use SSL/TLS", "Enable SSL/TLS encryption", false);
        
        // Channel configuration
        CreateTextBox(IRCBridgeSetting.Channel, "Channel", "IRC channel to join (include # prefix)", "#test");
        
        // User configuration
        CreateTextBox(IRCBridgeSetting.Nickname, "Nickname", "Your IRC nickname (leave empty to use VRC display name)", string.Empty);
        CreateTextBox(IRCBridgeSetting.Username, "Username", "Your IRC username (ident)", "vrcosc");
        CreateTextBox(IRCBridgeSetting.RealName, "Real Name", "Your real name for IRC", "VRCOSC IRC Bridge");
        
        // Authentication
        CreateTextBox(IRCBridgeSetting.Password, "Server Password", "IRC server password (if required, leave empty if not)", string.Empty);
        CreateTextBox(IRCBridgeSetting.NickServName, "NickServ Name", "NickServ account name (for authentication)", string.Empty);
        CreateTextBox(IRCBridgeSetting.NickServPassword, "NickServ Password", "NickServ account password (for authentication)", string.Empty);
        
        // Connection settings
        CreateToggle(IRCBridgeSetting.AutoReconnect, "Auto Reconnect", "Automatically reconnect if connection is lost", true);
        CreateTextBox(IRCBridgeSetting.ReconnectDelay, "Reconnect Delay (ms)", "Delay before reconnect attempt", 5000);
        
        // Behavior settings
        CreateTextBox(IRCBridgeSetting.MessageCooldown, "Message Cooldown (ms)", "Minimum time between processing same event type", 100);
        CreateToggle(IRCBridgeSetting.LogMessages, "Log Messages", "Log all IRC messages to console", false);

        // OSC Parameters
        RegisterParameter<bool>(IRCBridgeParameter.Connected, "VRCOSC/IRCBridge/Connected", ParameterMode.Write, "Connected", "True when connected to IRC server");
        RegisterParameter<int>(IRCBridgeParameter.UserCount, "VRCOSC/IRCBridge/UserCount", ParameterMode.Write, "User Count", "Number of users in channel");
        RegisterParameter<bool>(IRCBridgeParameter.MessageReceived, "VRCOSC/IRCBridge/MessageReceived", ParameterMode.Write, "Message Received", "True for 1 second when message is received");
        RegisterParameter<bool>(IRCBridgeParameter.UserJoined, "VRCOSC/IRCBridge/UserJoined", ParameterMode.Write, "User Joined", "True for 1 second when user joins");
        RegisterParameter<bool>(IRCBridgeParameter.UserLeft, "VRCOSC/IRCBridge/UserLeft", ParameterMode.Write, "User Left", "True for 1 second when user leaves");

        // Groups
        CreateGroup("Server", "IRC server connection settings", IRCBridgeSetting.ServerAddress, IRCBridgeSetting.ServerPort, IRCBridgeSetting.UseSSL);
        CreateGroup("Channel", "Channel settings", IRCBridgeSetting.Channel);
        CreateGroup("Identity", "User identity settings", IRCBridgeSetting.Nickname, IRCBridgeSetting.Username, IRCBridgeSetting.RealName);
        CreateGroup("Authentication", "Authentication settings", IRCBridgeSetting.Password, IRCBridgeSetting.NickServName, IRCBridgeSetting.NickServPassword);
        CreateGroup("Connection", "Connection behavior", IRCBridgeSetting.AutoReconnect, IRCBridgeSetting.ReconnectDelay);
        CreateGroup("Behavior", "Module behavior", IRCBridgeSetting.MessageCooldown, IRCBridgeSetting.LogMessages);
    }

    protected override void OnPostLoad()
    {
        // Variables
        var statusRef = CreateVariable<string>(IRCBridgeVariable.ServerStatus, "Server Status");
        var channelRef = CreateVariable<string>(IRCBridgeVariable.ChannelName, "Channel Name");
        var nickRef = CreateVariable<string>(IRCBridgeVariable.Nickname, "Nickname");
        CreateVariable<string>(IRCBridgeVariable.LastMessage, "Last Message");
        CreateVariable<string>(IRCBridgeVariable.LastMessageUser, "Last Message User");
        CreateVariable<string>(IRCBridgeVariable.LastJoinedUser, "Last Joined User");
        CreateVariable<string>(IRCBridgeVariable.LastLeftUser, "Last Left User");
        CreateVariable<int>(IRCBridgeVariable.UserCount, "User Count");
        CreateVariable<string>(IRCBridgeVariable.LastEventTime, "Last Event Time");

        // States
        CreateState(IRCBridgeState.Disconnected, "Disconnected", "IRC Bridge: Disconnected");
        CreateState(IRCBridgeState.Connecting, "Connecting", "IRC Bridge: Connecting...");
        CreateState(IRCBridgeState.Connected, "Connected", "IRC Bridge: Connected\nServer: {0}", statusRef != null ? new[] { statusRef } : null);
        CreateState(IRCBridgeState.Joining, "Joining", "IRC Bridge: Joining channel...");
        CreateState(IRCBridgeState.Joined, "Joined", "IRC Bridge: Joined\nChannel: {0}", channelRef != null ? new[] { channelRef } : null);
        CreateState(IRCBridgeState.Error, "Error", "IRC Bridge: Error\n{0}", statusRef != null ? new[] { statusRef } : null);

        // Events
        CreateEvent(IRCBridgeEvent.OnConnected, "On Connected");
        CreateEvent(IRCBridgeEvent.OnDisconnected, "On Disconnected");
        CreateEvent(IRCBridgeEvent.OnChannelJoined, "On Channel Joined");
        CreateEvent(IRCBridgeEvent.OnChannelLeft, "On Channel Left");
        CreateEvent(IRCBridgeEvent.OnUserJoined, "On User Joined");
        CreateEvent(IRCBridgeEvent.OnUserLeft, "On User Left");
        CreateEvent(IRCBridgeEvent.OnMessageReceived, "On Message Received");
        CreateEvent(IRCBridgeEvent.OnError, "On Error");
    }

    protected override async Task<bool> OnModuleStart()
    {
        SetVariableValue(IRCBridgeVariable.UserCount, 0);
        SetVariableValue(IRCBridgeVariable.ServerStatus, "Disconnected");
        
        await ConnectToIRC();
        return true;
    }

    protected override Task OnModuleStop()
    {
        DisconnectFromIRC();
        return Task.CompletedTask;
    }

    public async Task ConnectToIRC()
    {
        if (_ircClient != null && (_ircClient.IsConnected || _ircClient.IsConnecting))
        {
            return;
        }

        ChangeState(IRCBridgeState.Connecting);

        try
        {
            var serverAddress = GetSettingValue<string>(IRCBridgeSetting.ServerAddress);
            var serverPort = GetSettingValue<int>(IRCBridgeSetting.ServerPort);
            var useSSL = GetSettingValue<bool>(IRCBridgeSetting.UseSSL);

            if (string.IsNullOrEmpty(serverAddress))
            {
                throw new Exception("Server address is required");
            }

            if (serverPort < 1 || serverPort > 65535)
            {
                throw new Exception($"Invalid port number: {serverPort}");
            }

            SetVariableValue(IRCBridgeVariable.ServerStatus, $"Connecting to {serverAddress}:{serverPort}...");

            // Create IRC client and message handler
            _ircClient = new IRCClient(Log);
            _messageHandler = new IRCMessageHandler(this);

            // Wire up events
            _ircClient.RawMessageSent += (message) =>
            {
                if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    Log($"IRC → {message}");
                }
            };

            _ircClient.MessageReceived += async (message) =>
            {
                if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    Log($"IRC ← {message}");
                }
                await _messageHandler.ProcessMessageAsync(message, _ircClient);
            };

            _ircClient.Connected += () =>
            {
                SetVariableValue(IRCBridgeVariable.ServerStatus, $"Connected to {serverAddress}:{serverPort}");
                ChangeState(IRCBridgeState.Connected);
                this.SendParameterSafe(IRCBridgeParameter.Connected, true);
                TriggerEvent(IRCBridgeEvent.OnConnected);
                var channel = GetSettingValue<string>(IRCBridgeSetting.Channel);
                Log($"Connected to IRC server {serverAddress}:{serverPort}" + (!string.IsNullOrEmpty(channel) ? $", joining channel {channel}..." : ""));
            };

            _ircClient.Disconnected += () =>
            {
                SetVariableValue(IRCBridgeVariable.ServerStatus, "Disconnected");
                this.SendParameterSafe(IRCBridgeParameter.Connected, false);
                ChangeState(IRCBridgeState.Disconnected);
                TriggerEvent(IRCBridgeEvent.OnDisconnected);
                Log("Disconnected from IRC server");

                // Auto-reconnect if enabled
                if (GetSettingValue<bool>(IRCBridgeSetting.AutoReconnect))
                {
                    var delay = GetSettingValue<int>(IRCBridgeSetting.ReconnectDelay);
                    _ = Task.Delay(delay).ContinueWith(async _ =>
                    {
                        await ConnectToIRC();
                    });
                }
            };

            _ircClient.Error += (ex) =>
            {
                Log($"IRC error: {ex.Message}");
                SetVariableValue(IRCBridgeVariable.ServerStatus, $"Error: {ex.Message}");
                ChangeState(IRCBridgeState.Error);
                TriggerEvent(IRCBridgeEvent.OnError);
                this.SendParameterSafe(IRCBridgeParameter.Connected, false);

                // Auto-reconnect if enabled
                if (GetSettingValue<bool>(IRCBridgeSetting.AutoReconnect))
                {
                    var delay = GetSettingValue<int>(IRCBridgeSetting.ReconnectDelay);
                    _ = Task.Delay(delay).ContinueWith(async _ =>
                    {
                        await ConnectToIRC();
                    });
                }
            };

            // Get registration info
            var password = GetSettingValue<string>(IRCBridgeSetting.Password);
            var nickname = GetSettingValue<string>(IRCBridgeSetting.Nickname);
            var username = GetSettingValue<string>(IRCBridgeSetting.Username);
            var realName = GetSettingValue<string>(IRCBridgeSetting.RealName);

            // Get VRC user info for defaults (using reflection as User property is not exposed in SDK package)
            string? vrcUserId = null;
            string? vrcUsername = null;
            try
            {
                var player = GetPlayer();
                var userProperty = typeof(Player).GetProperty("User", BindingFlags.Public | BindingFlags.Instance);
                if (userProperty?.GetValue(player) is { } vrcUser)
                {
                    var userIdProperty = vrcUser.GetType().GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
                    var usernameProperty = vrcUser.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                    
                    vrcUserId = userIdProperty?.GetValue(vrcUser) as string;
                    vrcUsername = usernameProperty?.GetValue(vrcUser) as string;
                }
            }
            catch
            {
                // Ignore errors, will use fallbacks
            }

            // Use VRC display name if nickname is empty
            if (string.IsNullOrEmpty(nickname))
            {
                nickname = !string.IsNullOrEmpty(vrcUsername) ? vrcUsername : "VRCOSCUser";
            }

            // Use SHA256 hash of UserId if username is empty
            if (string.IsNullOrEmpty(username))
            {
                if (!string.IsNullOrEmpty(vrcUserId))
                {
                    try
                    {
                        using var sha256 = SHA256.Create();
                        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(vrcUserId));
                        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        // Use full hash - server will enforce its own limits via ISUPPORT
                        username = hashString;
                    }
                    catch
                    {
                        username = nickname;
                    }
                }
                else
                {
                    // Fallback: hash hardware IDs (CPU, motherboard, GPU) if UserId is not available
                    try
                    {
                        var hardwareId = HardwareIdGenerator.GenerateHardwareId();
                        if (!string.IsNullOrEmpty(hardwareId))
                        {
                            username = hardwareId;
                        }
                        else
                        {
                            username = nickname;
                        }
                    }
                    catch
                    {
                        username = nickname;
                    }
                }
            }
            if (string.IsNullOrEmpty(realName))
            {
                realName = "VRCOSC IRC Bridge";
            }

            // Connect to IRC server and send registration commands immediately
            await _ircClient.ConnectAsync(serverAddress, serverPort, useSSL, password, nickname, username, realName);

            SetVariableValue(IRCBridgeVariable.Nickname, nickname);
        }
        catch (Exception ex)
        {
            Log($"Failed to connect to IRC: {ex.Message}");
            SetVariableValue(IRCBridgeVariable.ServerStatus, $"Error: {ex.Message}");
            ChangeState(IRCBridgeState.Error);
            TriggerEvent(IRCBridgeEvent.OnError);
            this.SendParameterSafe(IRCBridgeParameter.Connected, false);

            if (GetSettingValue<bool>(IRCBridgeSetting.AutoReconnect))
            {
                var delay = GetSettingValue<int>(IRCBridgeSetting.ReconnectDelay);
                _ = Task.Delay(delay).ContinueWith(async _ =>
                {
                    await ConnectToIRC();
                });
            }
        }
    }

    public async Task SendIRCMessage(string message)
    {
        if (_ircClient == null || !_ircClient.IsConnected)
        {
            return;
        }

        try
        {
            if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
            {
                Log($"IRC → {message}");
            }

            await _ircClient.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Log($"Error sending IRC message: {ex.Message}");
        }
    }

    public void DisconnectFromIRC()
    {
        if (_ircClient == null)
        {
            return;
        }

        try
        {
            _ircClient.Disconnect();
            _ircClient.Dispose();
            _ircClient = null;
            _messageHandler = null;
        }
        catch (Exception ex)
        {
            Log($"Error disconnecting from IRC: {ex.Message}");
        }
    }

    public bool IsConnected => _ircClient?.IsConnected ?? false;
    public string GetChannelName() => GetVariableValue<string>(IRCBridgeVariable.ChannelName) ?? string.Empty;
    public string GetNickname() => GetVariableValue<string>(IRCBridgeVariable.Nickname) ?? string.Empty;
    public int GetUserCount() => GetVariableValue<int>(IRCBridgeVariable.UserCount);
    
    // Public accessor for GetVariableValue to be used by nodes
    public T? GetVariableValue<T>(IRCBridgeVariable variable) where T : notnull
    {
        // Use reflection to call the protected GetVariableValue<T>(Enum) method
        var method = typeof(Module).GetMethod("GetVariableValue", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null, 
            new[] { typeof(Enum) }, 
            null);
        if (method != null)
        {
            var genericMethod = method.MakeGenericMethod(typeof(T));
            return (T?)genericMethod.Invoke(this, new object[] { variable });
        }
        return default;
    }

    // Public wrapper methods for IRCMessageHandler
    public void SetVariableValuePublic<T>(IRCBridgeVariable variable, T value) where T : notnull
    {
        SetVariableValue(variable, value);
    }

    public void ChangeStatePublic(IRCBridgeState state)
    {
        ChangeState(state);
    }

    public void TriggerEventPublic(IRCBridgeEvent evt)
    {
        TriggerEvent(evt);
    }

    public void SendParameterPublic(IRCBridgeParameter parameter, object value)
    {
        SendParameter(parameter, value);
    }

    public void SendParameterSafePublic(IRCBridgeParameter parameter, object value)
    {
        this.SendParameterSafe(parameter, value);
    }

    public T PublicGetSettingValue<T>(IRCBridgeSetting setting) => GetSettingValue<T>(setting);
}
