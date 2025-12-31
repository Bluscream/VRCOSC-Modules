// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IrcDotNet;
using IrcDotNet.Ctcp;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using Module = VRCOSC.App.SDK.Modules.Module;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

[ModuleTitle("IRC Bridge")]
[ModuleDescription("Connect to IRC servers and receive events for channel activity")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class IRCBridgeModule : Module
{
    private IRCClient? _ircClient;
    private bool _isStopping = false;
    private string? _cachedVrcUserId;
    private string? _cachedVrcUsername;
    private string? _cachedExternalIpHash;
    private string? _cachedPcHash;
    private string? _originalNickname;
    private int _nicknameConflictCount = 0;
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // Store event handler references for proper cleanup
    private IrcLocalUser? _localUser;
    private IrcChannel? _joinedChannel;
    private CtcpClient? _ctcpClient;
    
    // Server limits from ISUPPORT (005) - parsed dynamically
    private readonly IRCISupportParser _isupportParser = new IRCISupportParser();

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
        CreateToggle(IRCBridgeSetting.RespondToCommands, "Respond To Commands", "Respond to chat commands (e.g., @bot ping, @bot time)", true);

        // OSC Parameters
        RegisterParameter<bool>(IRCBridgeParameter.Connected, "VRCOSC/IRCBridge/Connected", ParameterMode.Write, "Connected", "True when connected to IRC server");
        RegisterParameter<int>(IRCBridgeParameter.UserCount, "VRCOSC/IRCBridge/UserCount", ParameterMode.Write, "User Count", "Number of users in channel");
        RegisterParameter<bool>(IRCBridgeParameter.MessageReceived, "VRCOSC/IRCBridge/MessageReceived", ParameterMode.Write, "Message Received", "True for 1 second when message is received");
        RegisterParameter<bool>(IRCBridgeParameter.UserJoined, "VRCOSC/IRCBridge/UserJoined", ParameterMode.Write, "User Joined", "True for 1 second when user joins");
        RegisterParameter<bool>(IRCBridgeParameter.UserLeft, "VRCOSC/IRCBridge/UserLeft", ParameterMode.Write, "User Left", "True for 1 second when user leaves");

        // Groups
        CreateGroup("Server", "IRC server connection settings", IRCBridgeSetting.ServerAddress, IRCBridgeSetting.ServerPort, IRCBridgeSetting.UseSSL);
        CreateGroup("Channel", "Channel settings", IRCBridgeSetting.Channel);
        CreateGroup("Identity", "User identity settings", IRCBridgeSetting.Nickname);
        CreateGroup("Authentication", "Authentication settings", IRCBridgeSetting.Password, IRCBridgeSetting.NickServName, IRCBridgeSetting.NickServPassword);
        CreateGroup("Connection", "Connection behavior", IRCBridgeSetting.AutoReconnect, IRCBridgeSetting.ReconnectDelay);
        CreateGroup("Behavior", "Module behavior", IRCBridgeSetting.MessageCooldown, IRCBridgeSetting.LogMessages, IRCBridgeSetting.RespondToCommands);
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
        CreateEvent(IRCBridgeEvent.OnReady, "On Ready");
    }

    protected override async Task<bool> OnModuleStart()
    {
        SetVariableValue(IRCBridgeVariable.UserCount, 0);
        SetVariableValue(IRCBridgeVariable.ServerStatus, "Disconnected");
        
        await ConnectToIRC();
        return true;
    }

    protected override async Task OnModuleStop()
    {
        _isStopping = true;
        await DisconnectFromIRCAsync();
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

        // Reset server limits to defaults on new connection
        _isupportParser.ResetToDefaults();
        _isupportParser.OnLimitUpdated = (msg) => Log(msg);

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
            try
            {
                _ircClient = new IRCClient(Log);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to create IRC client: {ex.Message}", ex);
            }
            
            if (_ircClient == null)
            {
                throw new Exception("Failed to create IRC client: IRCClient instance is null");
            }
            
            var ircClient = _ircClient.Client;
            if (ircClient == null)
            {
                throw new Exception("Failed to create IRC client: StandardIrcClient is null");
            }

            // Wire up raw message logging (for debugging)
            ircClient.RawMessageSent += (sender, e) =>
            {
                if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages) && e != null)
                {
                    var rawMessage = e.RawContent ?? IRCMessageUtils.ReconstructRawMessage(e.Message);
                    if (!string.IsNullOrEmpty(rawMessage))
                    {
                        var sanitizedMessage = IRCMessageUtils.SanitizeForLogging(rawMessage);
                        Log($"IRC → {sanitizedMessage}");
                    }
                }
            };

            ircClient.RawMessageReceived += (sender, e) =>
            {
                if (e?.Message == null)
                {
                    return;
                }
                
                // Parse ISUPPORT (005) messages to get server limits
                if (e.Message.Command == "005")
                {
                    _isupportParser.ParseISupport(e.Message);
                }
                
                if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
                {
                    var rawMessage = e.RawContent ?? IRCMessageUtils.ReconstructRawMessage(e.Message);
                    if (!string.IsNullOrEmpty(rawMessage))
                    {
                        var sanitizedMessage = IRCMessageUtils.SanitizeForLogging(rawMessage);
                        Log($"IRC ← {sanitizedMessage}");
                    }
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
                if (sender is not IrcClient client || client.LocalUser == null)
                {
                    return;
                }
                
                var serverStatus = $"Connected to {serverAddress}:{serverPort}";
                SetVariableValue(IRCBridgeVariable.ServerStatus, serverStatus);
                ChangeState(IRCBridgeState.Connected);
                this.SendParameterSafe(IRCBridgeParameter.Connected, true);
                TriggerEvent(IRCBridgeEvent.OnConnected);
                
                // Update nickname from LocalUser
                var nickname = client.LocalUser.NickName;
                SetVariableValue(IRCBridgeVariable.Nickname, nickname);
                
                // Trigger pulse graph node with server status and nickname
                _ = TriggerModuleNodeAsync(typeof(OnIRCConnectedNode), new object[] { serverStatus, nickname });
                
                // Subscribe to LocalUser events (best practice from IrcBot)
                _localUser = client.LocalUser;
                
                // Initialize CTCP client for ACTION messages
                _ctcpClient = new CtcpClient(client);
                _localUser.JoinedChannel += LocalUser_JoinedChannel;
                _localUser.LeftChannel += LocalUser_LeftChannel;
                
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
                    if (sender is not IrcClient client || client.LocalUser == null)
                    {
                        return;
                    }
                    
                    // Store original nickname on first conflict
                    if (_originalNickname == null)
                    {
                        _originalNickname = client.LocalUser.NickName;
                    }
                    
                    _nicknameConflictCount++;
                    
                    // Generate new nickname based on conflict count using utility function
                    var baseNick = _originalNickname ?? "User";
                    var maxLength = _isupportParser.NickLen; // Use server's NICKLEN limit
                    var newNick = IRCNicknameUtils.GenerateAlternativeNickname(baseNick, _nicknameConflictCount, maxLength);
                    
                    Log($"Nickname already in use, trying alternative: {newNick} (attempt {_nicknameConflictCount})");
                    if (client.LocalUser != null)
                    {
                        client.LocalUser.SetNickName(newNick);
                    }
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
                        _ = AttemptReconnectAsync();
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
                    _ = AttemptReconnectAsync();
                }
            };

            // Get registration info
            var password = GetSettingValue<string>(IRCBridgeSetting.Password);
            var nickname = GetSettingValue<string>(IRCBridgeSetting.Nickname);

            // Get VRC user info for defaults (using reflection as User property is not exposed in SDK package)
            // Cache these for use in channel join announcement
            _cachedVrcUserId = null;
            _cachedVrcUsername = null;
            try
            {
                var player = GetPlayer();
                var userProperty = typeof(Player).GetProperty("User", BindingFlags.Public | BindingFlags.Instance);
                if (userProperty?.GetValue(player) is { } vrcUser)
                {
                    var userIdProperty = vrcUser.GetType().GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
                    var usernameProperty = vrcUser.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                    
                    _cachedVrcUserId = userIdProperty?.GetValue(vrcUser) as string;
                    _cachedVrcUsername = usernameProperty?.GetValue(vrcUser) as string;
                }
            }
            catch
            {
                // Ignore errors, will use fallbacks
            }
            
            // Cache PC hash for channel join announcement
            try
            {
                _cachedPcHash = Hashing.GenerateHardwareId((msg) => Log(msg));
                if (string.IsNullOrEmpty(_cachedPcHash))
                {
                    _cachedPcHash = string.Empty;
                    Log("PC Hash: (generation failed)");
                }
            }
            catch
            {
                _cachedPcHash = string.Empty;
                    Log("PC Hash: (generation failed)");
            }
            
            // Cache external IP hash (async, won't block connection)
            _ = Task.Run(async () =>
            {
                try
                {
                    _cachedExternalIpHash = await Hashing.GetExternalIpHashAsync(_httpClient, (msg) => Log(msg));
                }
                catch
                {
                    _cachedExternalIpHash = string.Empty;
                }
            });
            
            var vrcUserId = _cachedVrcUserId;
            var vrcUsername = _cachedVrcUsername;

            // Use VRC display name if nickname is empty
            if (string.IsNullOrEmpty(nickname))
            {
                nickname = !string.IsNullOrEmpty(vrcUsername) ? vrcUsername : "VRCOSCUser";
            }

            // Always use PC hash for username (hashed with CRC32 to fit IRC username limit)
            // Use server's NICKLEN if available, otherwise default to 9
            string username;
            if (!string.IsNullOrEmpty(_cachedPcHash))
            {
                // Hash the full PC hash with CRC32 to get a shorter hash (8 hex chars)
                var crc32Hash = Hashing.GenerateCrc32Hash(_cachedPcHash);
                if (!string.IsNullOrEmpty(crc32Hash))
                {
                    // Truncate CRC32 hash (8 chars) to server's NICKLEN if needed
                    username = crc32Hash.Length > _isupportParser.NickLen 
                        ? crc32Hash.Substring(0, _isupportParser.NickLen) 
                        : crc32Hash;
                }
                else
                {
                    // Fallback if CRC32 generation failed
                    username = nickname;
                }
            }
            else
            {
                // Fallback if PC hash generation failed
                username = nickname;
            }
            
            // Always use full PC hash for real name (truncated to server limit if needed)
            string realName;
            if (!string.IsNullOrEmpty(_cachedPcHash))
            {
                // Use full PC hash, but truncate if it exceeds server limit
                if (_cachedPcHash.Length > _isupportParser.RealNameLen)
                {
                    realName = _cachedPcHash.Substring(0, _isupportParser.RealNameLen);
                }
                else
                {
                    realName = _cachedPcHash;
                }
            }
            else
            {
                // Fallback if PC hash generation failed
                realName = "VRCOSC IRC Bridge";
            }

            // Reset nickname conflict tracking
            _originalNickname = nickname;
            _nicknameConflictCount = 0;
            
            // Connect to IRC server and send registration commands immediately
            await _ircClient.ConnectAsync(serverAddress, serverPort, useSSL, password, nickname, username, realName);

            SetVariableValue(IRCBridgeVariable.Nickname, nickname);
        }
        catch (Exception ex)
        {
            var errorMessage = ex.Message;
            if (ex.InnerException != null)
            {
                errorMessage += $" (Inner: {ex.InnerException.Message})";
            }
            Log($"Failed to connect to IRC: {errorMessage}");
            if (ex.StackTrace != null && GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
            {
                Log($"Stack trace: {ex.StackTrace}");
            }
            SetVariableValue(IRCBridgeVariable.ServerStatus, $"Error: {ex.Message}");
            ChangeState(IRCBridgeState.Error);
            TriggerEvent(IRCBridgeEvent.OnError);
            this.SendParameterSafe(IRCBridgeParameter.Connected, false);

            if (GetSettingValue<bool>(IRCBridgeSetting.AutoReconnect))
            {
                _ = AttemptReconnectAsync();
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
                // Sanitize control characters for readable logging
                var sanitizedMessage = IRCMessageUtils.SanitizeForLogging(message);
                Log($"IRC → {sanitizedMessage}");
            }

            await _ircClient.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Log($"Error sending IRC message: {ex.Message}");
        }
    }

    public async Task DisconnectFromIRCAsync()
    {
        if (_ircClient == null)
        {
            return;
        }

        try
        {
            Log("Stopping");
            
            // Unsubscribe from channel events first
            if (_joinedChannel != null)
            {
                try
                {
                    _joinedChannel.UserJoined -= Channel_UserJoined;
                    _joinedChannel.UserLeft -= Channel_UserLeft;
                    _joinedChannel.MessageReceived -= Channel_MessageReceived;
                }
                catch { }
                _joinedChannel = null;
            }
            
            // Unsubscribe from LocalUser events
            if (_localUser != null)
            {
                try
                {
                    _localUser.JoinedChannel -= LocalUser_JoinedChannel;
                    _localUser.LeftChannel -= LocalUser_LeftChannel;
                }
                catch { }
                _localUser = null;
                _ctcpClient = null;
            }
            
            // Disconnect and send QUIT message
            _ircClient.Disconnect();
            
            // Wait a short time for QUIT message to be sent before disposing
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            Log($"Error during disconnect: {ex.Message}");
        }
        finally
        {
            try
            {
                _ircClient?.Dispose();
            }
            catch { }
            _ircClient = null;
            Log("Stopped");
        }
    }
    
    public void DisconnectFromIRC()
    {
        // Synchronous version for backwards compatibility
        _ = DisconnectFromIRCAsync();
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

    // Public wrapper methods
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

    // Higher-level methods using IrcDotNet's API (best practices)
    public async Task SendMessageToChannelAsync(string channelName, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var client = _ircClient.Client; // Store to avoid null reference warnings
        var channel = IRCClientUtils.FindChannel(client, channelName);
        if (channel != null && client.LocalUser != null)
        {
            await Task.Run(() => client.LocalUser.SendMessage(channel, message));
        }
        else
        {
            // Channel not found, send as raw message (fallback)
            await SendIRCMessage($"PRIVMSG {channelName} :{message}");
        }
    }

    public async Task SendMessageToUserAsync(string userName, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var client = _ircClient.Client; // Store to avoid null reference warnings
        var user = IRCClientUtils.FindUser(client, userName);
        if (user != null && client.LocalUser != null)
        {
            await Task.Run(() => client.LocalUser.SendMessage(user, message));
        }
        else
        {
            // User not found, send as raw message (fallback)
            await SendIRCMessage($"PRIVMSG {userName} :{message}");
        }
    }

    public async Task SendNoticeAsync(string target, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var client = _ircClient.Client; // Store to avoid null reference warnings
        if (client.LocalUser == null)
        {
            throw new InvalidOperationException("Local user not available");
        }

        // Try to find as channel first
        var channel = IRCClientUtils.FindChannel(client, target);
        if (channel != null)
        {
            await Task.Run(() => client.LocalUser.SendNotice(channel, message));
            return;
        }

        // Try to find as user
        var user = IRCClientUtils.FindUser(client, target);
        if (user != null)
        {
            await Task.Run(() => client.LocalUser.SendNotice(user, message));
            return;
        }

        // Fallback to raw message
        await SendIRCMessage($"NOTICE {target} :{message}");
    }

    public async Task JoinChannelAsync(string channelName)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        await Task.Run(() => _ircClient.Client.Channels.Join(channelName));
    }

    public async Task LeaveChannelAsync(string channelName, string? reason = null)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var channel = IRCClientUtils.FindChannel(_ircClient?.Client, channelName);
        if (channel != null)
        {
            await Task.Run(() => channel.Leave(reason));
        }
    }

    public List<string> GetChannelUserList(string? channelName = null)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            return new List<string>();
        }

        // Use provided channel name or default to current channel
        if (string.IsNullOrEmpty(channelName))
        {
            channelName = GetChannelName();
        }

        if (string.IsNullOrEmpty(channelName))
        {
            return new List<string>();
        }

        // Ensure channel starts with #
        if (!channelName.StartsWith("#"))
        {
            channelName = "#" + channelName;
        }

        var channel = IRCClientUtils.FindChannel(_ircClient?.Client, channelName);
        if (channel == null)
        {
            return new List<string>();
        }

        // Return list of user nicknames
        return channel.Users.Select(cu => cu.User.NickName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task ChangeNicknameAsync(string newNickname)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        if (string.IsNullOrWhiteSpace(newNickname))
        {
            throw new ArgumentException("Nickname cannot be empty", nameof(newNickname));
        }

        await Task.Run(() => _ircClient.Client.LocalUser.SetNickName(newNickname));
        
        // Update variable
        SetVariableValue(IRCBridgeVariable.Nickname, newNickname);
    }

    // Event handlers for IrcDotNet higher-level events (following IrcBot best practices)
    private void LocalUser_JoinedChannel(object? sender, IrcChannelEventArgs e)
    {
        var channel = e.Channel;
        _joinedChannel = channel;
        
        // Subscribe to channel events when we join (best practice from IrcBot)
        channel.UserJoined += Channel_UserJoined;
        channel.UserLeft += Channel_UserLeft;
        channel.MessageReceived += Channel_MessageReceived;
        
        ChangeState(IRCBridgeState.Joined);
        SetVariableValue(IRCBridgeVariable.ChannelName, channel.Name);
        TriggerEvent(IRCBridgeEvent.OnChannelJoined);
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        // Trigger pulse graph node with channel name and user count
        _ = TriggerModuleNodeAsync(typeof(OnIRCChannelJoinedNode), new object[] { channel.Name, userCount });
        
        Log($"Joined channel: {channel.Name}");
        
        // Send client data as ACTION message (fire-and-forget)
        _ = SendClientDataAnnouncementWithDelayAsync(channel.Name);
    }

    private void LocalUser_LeftChannel(object? sender, IrcChannelEventArgs e)
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

    private void Channel_UserJoined(object? sender, IrcChannelUserEventArgs e)
    {
        if (sender is not IrcChannel channel)
        {
            return;
        }
        
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
        
        _ = SendParameterPulseAsync(IRCBridgeParameter.UserJoined);
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            Log($"User joined: {user.NickName}");
        }
    }

    private void Channel_UserLeft(object? sender, IrcChannelUserEventArgs e)
    {
        if (sender is not IrcChannel channel)
        {
            return;
        }
        
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
        
        _ = SendParameterPulseAsync(IRCBridgeParameter.UserLeft);
        
        // Update user count
        var userCount = channel.Users.Count;
        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            Log($"User left: {user.NickName}");
        }
    }

    private void Channel_MessageReceived(object? sender, IrcMessageEventArgs e)
    {
        if (sender is not IrcChannel channel)
        {
            return;
        }
        
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
        
        _ = SendParameterPulseAsync(IRCBridgeParameter.MessageReceived);
        
        // Handle commands: "@<botnickname> <command>" (only if enabled)
        if (GetSettingValue<bool>(IRCBridgeSetting.RespondToCommands) && _ircClient?.Client?.LocalUser != null)
        {
            var botNickname = _ircClient.Client.LocalUser.NickName;
            if (!string.IsNullOrEmpty(botNickname))
            {
                var messageLower = message.ToLowerInvariant();
                var botNickLower = botNickname.ToLowerInvariant();
                
                // Handle ping/pong command: "@<botnickname> ping" -> "@<username> pong"
                var pingPattern = $@"@{Regex.Escape(botNickLower)}\s+ping\b";
                if (Regex.IsMatch(messageLower, pingPattern, RegexOptions.IgnoreCase))
                {
                    // Respond with pong
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SendMessageToChannelAsync(channel.Name, $"@{user.NickName} pong");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error sending pong response: {ex.Message}");
                        }
                    });
                }
                
                // Handle time command: "@<botnickname> time" -> "@<username> <RFC local time>"
                var timePattern = $@"@{Regex.Escape(botNickLower)}\s+time\b";
                if (Regex.IsMatch(messageLower, timePattern, RegexOptions.IgnoreCase))
                {
                    // Respond with current local time in RFC 3339 format
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var localTime = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"); // RFC 3339 format
                            await SendMessageToChannelAsync(channel.Name, $"@{user.NickName} {localTime}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Error sending time response: {ex.Message}");
                        }
                    });
                }
            }
        }
        
        if (GetSettingValue<bool>(IRCBridgeSetting.LogMessages))
        {
            Log($"<{user.NickName}> {message}");
        }
    }
    
    private async Task SendParameterPulseAsync(IRCBridgeParameter parameter, int durationMs = 1000)
    {
        if (_isStopping) return;
        
        try
        {
            SendParameterSafePublic(parameter, true);
            await Task.Delay(durationMs);
            SendParameterSafePublic(parameter, false);
        }
        catch
        {
            // Ignore errors during shutdown
        }
    }
    
    private async Task AttemptReconnectAsync()
    {
        if (_isStopping) return;
        
        var delay = GetSettingValue<int>(IRCBridgeSetting.ReconnectDelay);
        await Task.Delay(delay);
        
        if (!_isStopping)
        {
            await ConnectToIRC();
        }
    }
    
    
    
    private async Task SendClientDataAnnouncementWithDelayAsync(string channelName)
    {
        try
        {
            // Always wait 1 second after joining channel before broadcasting hashes
            await Task.Delay(1000);
            
            await SendClientDataAnnouncementAsync(channelName);
        }
        catch (Exception ex)
        {
            Log($"Error sending client data announcement: {ex.Message}");
        }
    }
    
    private async Task SendClientDataAnnouncementAsync(string channelName)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            return;
        }
        
        try
        {
            // Get external IP hash (use empty string if not available)
            var externalIpHash = _cachedExternalIpHash ?? string.Empty;
            
            // Get PC hash (use empty string if not available)
            var pcHash = _cachedPcHash ?? string.Empty;
            
            // Get user ID hash
            var userIdHash = string.Empty;
            if (!string.IsNullOrEmpty(_cachedVrcUserId))
            {
                userIdHash = Hashing.GenerateSha256Hash(_cachedVrcUserId);
                if (!string.IsNullOrEmpty(userIdHash))
                {
                    Log($"User ID Hash: {userIdHash}");
                }
                else
                {
                    Log("User ID Hash: (generation failed)");
                }
            }
            else
            {
                Log("User ID Hash: (VRC user ID not available)");
            }
            
            // Get username (use empty string if not available)
            var username = _cachedVrcUsername ?? string.Empty;
            
            // Build CSV line using utility class
            var csvLine = ClientDataBuilder.BuildClientDataCsv(externalIpHash, pcHash, userIdHash, username);
            
            // Send as ACTION message (/me command)
            await SendActionMessageAsync(channelName, csvLine);
            
            // Trigger OnReady event 1 second after join message is sent
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000);
                    if (!_isStopping)
                    {
                        TriggerEvent(IRCBridgeEvent.OnReady);
                    }
                }
                catch
                {
                    // Ignore errors during shutdown
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Error preparing client data announcement: {ex.Message}");
        }
    }
    
    private async Task SendActionMessageAsync(string channelName, string message)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected)
        {
            return;
        }
        
        try
        {
            // Use CtcpClient for proper ACTION message formatting
            // This ensures the message is correctly formatted as a CTCP ACTION command
            var channel = IRCClientUtils.FindChannel(_ircClient?.Client, channelName);
            if (channel != null && _ctcpClient != null)
            {
                await Task.Run(() => _ctcpClient.SendAction(channel, message));
            }
            else
            {
                // Fallback: send as regular message if channel or CTCP client not found
                await SendMessageToChannelAsync(channelName, message);
            }
        }
        catch (Exception ex)
        {
            Log($"Error sending ACTION message: {ex.Message}");
        }
    }
}
