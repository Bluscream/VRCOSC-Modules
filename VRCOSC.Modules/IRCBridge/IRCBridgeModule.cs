// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using IrcDotNet;
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
    private bool _isStopping = false;

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
        CreateTextBox(IRCBridgeSetting.Username, "Username", "Your IRC username (ident)", "");
        CreateTextBox(IRCBridgeSetting.RealName, "Real Name", "Your real name for IRC", "");
        
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
        _isStopping = true;
        DisconnectFromIRC();
        return Task.CompletedTask;
    }

    public async Task ConnectToIRC()
    {
        if (_ircClient != null && (_ircClient.IsConnected || _ircClient.IsConnecting))
        {
            return;
        }

        // Dispose old client if it exists (shouldn't happen, but safety check)
        if (_ircClient != null)
        {
            try
            {
                _ircClient.Dispose();
            }
            catch
            {
                // Ignore errors during disposal
            }
            _ircClient = null;
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

            // Create IRC client
            _ircClient = new IRCClient(Log);
            var ircClient = _ircClient.Client;
            if (ircClient == null)
            {
                throw new Exception("Failed to create IRC client");
            }

            // Wire up raw message logging (for debugging)
            ircClient.RawMessageSent += (sender, e) =>
            {
                if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages) && e?.Message != null)
                {
                    var logMsg = e.Message.Command ?? "";
                    if (e.Message.Parameters != null && e.Message.Parameters.Count > 0 && e.Message.Parameters[0] != null)
                    {
                        logMsg += $" {e.Message.Parameters[0]}";
                    }
                    Log($"IRC → {logMsg}");
                }
            };

            ircClient.RawMessageReceived += (sender, e) =>
            {
                if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages) && e?.Message != null)
                {
                    var logMsg = e.Message.Command ?? "";
                    if (e.Message.Parameters != null && e.Message.Parameters.Count > 0 && e.Message.Parameters[0] != null)
                    {
                        logMsg += $" {e.Message.Parameters[0]}";
                    }
                    Log($"IRC ← {logMsg}");
                }
            };

            // Wire up connection events
            ircClient.Connected += (sender, e) =>
            {
                SetVariableValue(IRCBridgeVariable.ServerStatus, $"Connected to {serverAddress}:{serverPort}");
                ChangeState(IRCBridgeState.Connected);
                Log($"Connected to IRC server {serverAddress}:{serverPort}");
            };

            // Subscribe to Registered event (best practice: subscribe to LocalUser events here)
            ircClient.Registered += (sender, e) =>
            {
                var client = (IrcClient)sender;
                SetVariableValue(IRCBridgeVariable.ServerStatus, $"Connected to {serverAddress}:{serverPort}");
                ChangeState(IRCBridgeState.Connected);
                this.SendParameterSafe(IRCBridgeParameter.Connected, true);
                TriggerEvent(IRCBridgeEvent.OnConnected);
                _ = TriggerModuleNodeAsync(typeof(OnIRCConnectedNode), new object[] { serverAddress, serverPort });
                
                // Update nickname from LocalUser
                SetVariableValue(IRCBridgeVariable.Nickname, client.LocalUser.NickName);
                
                // Subscribe to LocalUser events (best practice from IrcBot)
                client.LocalUser.JoinedChannel += LocalUser_JoinedChannel;
                client.LocalUser.LeftChannel += LocalUser_LeftChannel;
                
                // Authenticate with NickServ if configured
                var nickServName = GetSettingValue<string>(IRCBridgeSetting.NickServName);
                var nickServPassword = GetSettingValue<string>(IRCBridgeSetting.NickServPassword);
                
                if (!string.IsNullOrEmpty(nickServName) && !string.IsNullOrEmpty(nickServPassword))
                {
                    // Wait a bit before authenticating (best practice)
                    _ = Task.Delay(2000).ContinueWith(_ =>
                    {
                        try
                        {
                            client.LocalUser.SendMessage("NickServ", $"IDENTIFY {nickServName} {nickServPassword}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error authenticating with NickServ: {ex.Message}");
                        }
                    });
                }
                
                // Join channel if configured
                var channel = GetSettingValue<string>(IRCBridgeSetting.Channel);
                if (!string.IsNullOrEmpty(channel))
                {
                    ChangeState(IRCBridgeState.Joining);
                    SetVariableValue(IRCBridgeVariable.ChannelName, channel);
                    Log($"Joining channel {channel}...");
                    client.Channels.Join(channel);
                }
            };

            // Handle nickname conflicts (433 error) using ProtocolError event
            ircClient.ProtocolError += (sender, e) =>
            {
                if (e.Code == 433) // ERR_NICKNAMEINUSE
                {
                    var client = (IrcClient)sender;
                    var currentNick = client.LocalUser.NickName;
                    var newNick = $"{currentNick}_";
                    
                    // Ensure nickname doesn't exceed IRC limit (typically 9 characters)
                    if (newNick.Length > 9)
                    {
                        newNick = newNick.Substring(0, 9);
                    }
                    
                    Log($"Nickname already in use, trying alternative: {newNick}");
                    client.LocalUser.SetNickName(newNick);
                    SetVariableValue(IRCBridgeVariable.Nickname, newNick);
                }
            };

            _ircClient.Disconnected += () =>
            {
                // Prevent duplicate disconnect handling
                if (!_isStopping)
                {
                    var status = "Disconnected";
                    SetVariableValue(IRCBridgeVariable.ServerStatus, status);
                    this.SendParameterSafe(IRCBridgeParameter.Connected, false);
                    ChangeState(IRCBridgeState.Disconnected);
                    TriggerEvent(IRCBridgeEvent.OnDisconnected);
                    
                    // Trigger pulse graph node
                    _ = TriggerModuleNodeAsync(typeof(OnIRCDisconnectedNode), new object[] { status });
                    
                    Log("Disconnected from IRC server");

                    // Auto-reconnect if enabled
                    if (GetSettingValue<bool>(IRCBridgeSetting.AutoReconnect))
                    {
                        var delay = GetSettingValue<int>(IRCBridgeSetting.ReconnectDelay);
                        _ = Task.Delay(delay).ContinueWith(async _ =>
                        {
                            if (!_isStopping)
                            {
                                await ConnectToIRC();
                            }
                        });
                    }
                }
            };

            _ircClient.Error += (ex) =>
            {
                var errorMessage = ex.Message;
                var status = $"Error: {errorMessage}";
                
                Log($"IRC error: {errorMessage}");
                SetVariableValue(IRCBridgeVariable.ServerStatus, status);
                ChangeState(IRCBridgeState.Error);
                TriggerEvent(IRCBridgeEvent.OnError);
                
                // Trigger pulse graph node
                _ = TriggerModuleNodeAsync(typeof(OnIRCErrorNode), new object[] { errorMessage, status });
                
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
                        var hardwareId = HardwareIdGenerator.GenerateHardwareId((msg) => Log($"[HardwareIdGenerator] {msg}"));
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
            Log("Stopping");
            _ircClient.Disconnect();
        }
        catch (Exception ex)
        {
            Log($"Error during disconnect: {ex.Message}");
        }
        finally
        {
            _ircClient?.Dispose();
            _ircClient = null;
            Log("Stopped");
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

    public async Task TriggerModuleNodeAsync(Type nodeType, object[] data)
    {
        try
        {
            await TriggerModuleNode(nodeType, data);
        }
        catch
        {
            // Ignore errors if node doesn't exist or module is stopping
        }
    }

    public void SendParameterPublic(IRCBridgeParameter parameter, object value)
    {
        SendParameter(parameter, value);
    }

    public void SendParameterSafePublic(IRCBridgeParameter parameter, object value)
    {
        // Don't send parameters if module is stopping or stopped
        if (!_isStopping)
        {
            try
            {
                this.SendParameterSafe(parameter, value);
            }
            catch (InvalidOperationException)
            {
                // OSC not connected, ignore during shutdown
            }
        }
    }

    public T PublicGetSettingValue<T>(IRCBridgeSetting setting) => GetSettingValue<T>(setting);

    // Event handlers for IrcDotNet higher-level events (following IrcBot best practices)
    private void LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
    {
        var channel = e.Channel;
        
        // Subscribe to channel events when we join (best practice from IrcBot)
        channel.UserJoined += Channel_UserJoined;
        channel.UserLeft += Channel_UserLeft;
        channel.MessageReceived += Channel_MessageReceived;
        
        ChangeState(IRCBridgeState.Joined);
        SetVariableValue(IRCBridgeVariable.ChannelName, channel.Name);
        TriggerEvent(IRCBridgeEvent.OnChannelJoined);
        _ = TriggerModuleNodeAsync(typeof(OnIRCChannelJoinedNode), new object[] { channel.Name });
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        Log($"Joined channel: {channel.Name}");
    }

    private void LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
    {
        var channel = e.Channel;
        
        // Unsubscribe from channel events when we leave (best practice from IrcBot)
        channel.UserJoined -= Channel_UserJoined;
        channel.UserLeft -= Channel_UserLeft;
        channel.MessageReceived -= Channel_MessageReceived;
        
        ChangeState(IRCBridgeState.Connected);
        TriggerEvent(IRCBridgeEvent.OnChannelLeft);
        Log($"Left channel: {channel.Name}");
    }

    private void Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
    {
        var channel = (IrcChannel)sender;
        var user = e.ChannelUser.User;
        
        // Skip if it's us
        if (user is IrcLocalUser)
        {
            return;
        }
        
        var eventTime = DateTime.Now.ToString("HH:mm:ss");
        
        SetVariableValue(IRCBridgeVariable.LastJoinedUser, user.NickName);
        SetVariableValue(IRCBridgeVariable.LastEventTime, eventTime);
        TriggerEvent(IRCBridgeEvent.OnUserJoined);
        _ = TriggerModuleNodeAsync(typeof(OnIRCUserJoinedNode), new object[] { user.NickName, channel.Name, eventTime });
        
        try
        {
            SendParameterSafePublic(IRCBridgeParameter.UserJoined, true);
            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                try
                {
                    SendParameterSafePublic(IRCBridgeParameter.UserJoined, false);
                }
                catch { }
            });
        }
        catch { }
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            Log($"User joined: {user.NickName}");
        }
    }

    private void Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
    {
        var channel = (IrcChannel)sender;
        var user = e.ChannelUser.User;
        
        // Skip if it's us
        if (user is IrcLocalUser)
        {
            return;
        }
        
        var eventTime = DateTime.Now.ToString("HH:mm:ss");
        
        SetVariableValue(IRCBridgeVariable.LastLeftUser, user.NickName);
        SetVariableValue(IRCBridgeVariable.LastEventTime, eventTime);
        TriggerEvent(IRCBridgeEvent.OnUserLeft);
        _ = TriggerModuleNodeAsync(typeof(OnIRCUserLeftNode), new object[] { user.NickName, channel.Name, eventTime });
        
        try
        {
            SendParameterSafePublic(IRCBridgeParameter.UserLeft, true);
            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                try
                {
                    SendParameterSafePublic(IRCBridgeParameter.UserLeft, false);
                }
                catch { }
            });
        }
        catch { }
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            Log($"User left: {user.NickName}");
        }
    }

    private void Channel_MessageReceived(object sender, IrcMessageEventArgs e)
    {
        var channel = (IrcChannel)sender;
        var user = e.Source as IrcUser;
        
        if (user == null)
        {
            return;
        }
        
        var message = e.Text;
        var eventTime = DateTime.Now.ToString("HH:mm:ss");
        
        SetVariableValue(IRCBridgeVariable.LastMessage, message);
        SetVariableValue(IRCBridgeVariable.LastMessageUser, user.NickName);
        SetVariableValue(IRCBridgeVariable.LastEventTime, eventTime);
        TriggerEvent(IRCBridgeEvent.OnMessageReceived);
        _ = TriggerModuleNodeAsync(typeof(OnIRCMessageReceivedNode), new object[] { message, user.NickName, channel.Name, eventTime });
        
        try
        {
            SendParameterSafePublic(IRCBridgeParameter.MessageReceived, true);
            _ = Task.Delay(1000).ContinueWith(_ =>
            {
                try
                {
                    SendParameterSafePublic(IRCBridgeParameter.MessageReceived, false);
                }
                catch { }
            });
        }
        catch { }
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            Log($"<{user.NickName}> {message}");
        }
    }
}
