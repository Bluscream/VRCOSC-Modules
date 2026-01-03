// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Net.Http;
using System.Threading.Tasks;
using IrcDotNet;
using IrcDotNet.Ctcp;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using Module = VRCOSC.App.SDK.Modules.Module;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

[ModuleTitle("IRC Bridge")]
[ModuleDescription("Connect to IRC servers and receive events for channel activity")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public partial class IRCBridgeModule : Module, IVRCClientEventHandler
{
    // Connection state
    protected IRCClient? _ircClient;
    protected bool _isStopping = false;
    protected static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    
    // Store event handler references for proper cleanup
    protected IrcLocalUser? _localUser;
    protected IrcChannel? _joinedChannel;
    protected CtcpClient? _ctcpClient;
    
    // Server limits from ISUPPORT (005) - parsed dynamically
    protected readonly IRCISupportParser _isupportParser = new IRCISupportParser();
    
    // Hash and VRChat management
    protected Hashing? _hashing;
    protected VRChat? _vrchat;
    
    // Nickname management
    protected string? _originalNickname;
    protected int _nicknameConflictCount = 0;
    
    // Hash tracking for change detection
    protected string? _lastUserIdHash;
    protected string? _lastUsernameHash;
    protected string? _lastPcHash;
    protected string? _lastExternalIpHash;

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
        CreateToggle(IRCBridgeSetting.LogChatMessages, "Log Chat Messages", "Log incoming and outgoing channel/private messages from users", false);
        CreateToggle(IRCBridgeSetting.LogSystemMessages, "Log System Messages", "Log server responses and system messages (numeric codes, etc.)", false);
        CreateToggle(IRCBridgeSetting.LogEvents, "Log Events", "Log IRC events (JOIN, NICK, MODE, etc.)", false);
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
        CreateGroup("Behavior", "Module behavior", IRCBridgeSetting.MessageCooldown, IRCBridgeSetting.LogChatMessages, IRCBridgeSetting.LogSystemMessages, IRCBridgeSetting.LogEvents, IRCBridgeSetting.RespondToCommands);
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
        
        // Initialize VRChat instance
        _vrchat = new VRChat(GetPlayer());
        
        // Subscribe to VRChat change events
        _vrchat.OnUsernameChanged += OnVRChatUsernameChanged;
        _vrchat.OnUserIdChanged += OnVRChatUserIdChanged;
        
        // Initialize VRChat
        await _vrchat.InitializeAsync();
        
        // Initialize hashing instance
        _hashing = new Hashing(_httpClient);
        
        // Subscribe to hash change events
        _hashing.OnExternalIpChanged += OnExternalIpChanged;
        _hashing.OnPcHashChanged += OnPcHashChanged;
        
        // Initialize hashing
        await _hashing.InitializeAsync();
        
        await ConnectToIRC();
        return true;
    }

    protected override async Task OnModuleStop()
    {
        _isStopping = true;
        
        // Unsubscribe from events
        if (_vrchat != null)
        {
            _vrchat.OnUsernameChanged -= OnVRChatUsernameChanged;
            _vrchat.OnUserIdChanged -= OnVRChatUserIdChanged;
            _vrchat.Dispose();
            _vrchat = null;
        }
        
        if (_hashing != null)
        {
            _hashing.OnExternalIpChanged -= OnExternalIpChanged;
            _hashing.OnPcHashChanged -= OnPcHashChanged;
            _hashing.Dispose();
            _hashing = null;
        }
        
        await DisconnectFromIRCAsync("Module stopping");
    }
    
    private async void OnVRChatUsernameChanged(string? oldUsername, string? newUsername)
    {
        if (_ircClient?.Client?.LocalUser != null && _ircClient.IsConnected)
        {
            var nicknameSetting = GetSettingValue<string>(IRCBridgeSetting.Nickname);
            if (string.IsNullOrEmpty(nicknameSetting) && !string.IsNullOrEmpty(newUsername))
            {
                var currentNick = _ircClient.Client.LocalUser.NickName;
                if (currentNick != newUsername)
                {
                    Log($"VRC username changed from '{oldUsername}' to '{newUsername}', updating IRC nick");
                    try
                    {
                        await ChangeNicknameAsync(newUsername);
                    }
                    catch (Exception ex)
                    {
                        Log($"Error changing nickname: {ex.Message}");
                    }
                }
            }
        }
        
        // Resend welcome message if connected
        if (_ircClient?.Client != null && _ircClient.IsConnected && _joinedChannel != null)
        {
            await Task.Delay(500); // Wait a bit for other updates
            await SendClientDataAnnouncementAsync(_joinedChannel.Name);
        }
    }
    
    private async void OnVRChatUserIdChanged(string? oldUserId, string? newUserId)
    {
        // Resend welcome message if connected
        if (_ircClient?.Client != null && _ircClient.IsConnected && _joinedChannel != null)
        {
            await Task.Delay(500); // Wait a bit for other updates
            await SendClientDataAnnouncementAsync(_joinedChannel.Name);
        }
    }
    
    private async void OnExternalIpChanged(System.Net.IPAddress? oldIp, System.Net.IPAddress? newIp)
    {
        // Resend welcome message if connected
        if (_ircClient?.Client != null && _ircClient.IsConnected && _joinedChannel != null)
        {
            await Task.Delay(500); // Wait a bit for other updates
            await SendClientDataAnnouncementAsync(_joinedChannel.Name);
        }
    }
    
    private async void OnPcHashChanged(string? oldHash, string? newHash)
    {
        // Resend welcome message if connected
        if (_ircClient?.Client != null && _ircClient.IsConnected && _joinedChannel != null)
        {
            await Task.Delay(500); // Wait a bit for other updates
            await SendClientDataAnnouncementAsync(_joinedChannel.Name);
        }
    }
    
    #region IVRCClientEventHandler
    
    public void OnUserAuthenticated(VRChatClientEventUserAuthenticated eventArgs)
    {
        // Forward to VRChat instance
        _vrchat?.OnUserAuthenticated(eventArgs);
    }
    
    #endregion
}
