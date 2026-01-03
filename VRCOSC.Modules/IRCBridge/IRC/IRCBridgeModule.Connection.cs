// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using IrcDotNet;
using VRCOSC.App.SDK.Parameters;
using VRCOSC.App.SDK.VRChat;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

public partial class IRCBridgeModule
{
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
                if (e == null) return;
                
                var rawMessage = e.RawContent ?? IRCMessageUtils.ReconstructRawMessage(e.Message);
                if (string.IsNullOrEmpty(rawMessage)) return;
                
                var category = IRCMessageUtils.CategorizeMessage(rawMessage, e.Message);
                bool shouldLog = category switch
                {
                    IRCMessageCategory.Chat => GetSettingValue<bool>(IRCBridgeSetting.LogChatMessages),
                    IRCMessageCategory.System => GetSettingValue<bool>(IRCBridgeSetting.LogSystemMessages),
                    IRCMessageCategory.Event => GetSettingValue<bool>(IRCBridgeSetting.LogEvents),
                    _ => false
                };
                
                if (shouldLog)
                {
                    var sanitizedMessage = IRCMessageUtils.SanitizeForLogging(rawMessage);
                    Log($"IRC → {sanitizedMessage}");
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
                
                // Handle 366 (RPL_ENDOFNAMES) - End of /NAMES list - update user count when list is complete
                if (e.Message.Command == "366" && e.Message.Parameters != null && e.Message.Parameters.Count >= 2)
                {
                    var channelName = e.Message.Parameters[1];
                    if (!string.IsNullOrEmpty(channelName) && _joinedChannel != null && _joinedChannel.Name == channelName)
                    {
                        // NAMES list is complete, update user count
                        var userCount = _joinedChannel.Users.Count;
                        SetVariableValue(IRCBridgeVariable.UserCount, userCount);
                        SendParameterSafePublic(IRCBridgeParameter.UserCount, userCount);
                    }
                }
                
                var rawMessage = e.RawContent ?? IRCMessageUtils.ReconstructRawMessage(e.Message);
                if (string.IsNullOrEmpty(rawMessage)) return;
                
                var category = IRCMessageUtils.CategorizeMessage(rawMessage, e.Message);
                bool shouldLog = category switch
                {
                    IRCMessageCategory.Chat => GetSettingValue<bool>(IRCBridgeSetting.LogChatMessages),
                    IRCMessageCategory.System => GetSettingValue<bool>(IRCBridgeSetting.LogSystemMessages),
                    IRCMessageCategory.Event => GetSettingValue<bool>(IRCBridgeSetting.LogEvents),
                    _ => false
                };
                
                if (shouldLog)
                {
                    var sanitizedMessage = IRCMessageUtils.SanitizeForLogging(rawMessage);
                    Log($"IRC ← {sanitizedMessage}");
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
                _ctcpClient = new IrcDotNet.Ctcp.CtcpClient(client);
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

            // Get VRC user info from VRChat instance
            var vrcUserId = _vrchat?.UserId;
            var vrcUsername = _vrchat?.Username;

            // Use VRC display name if nickname is empty
            if (string.IsNullOrEmpty(nickname))
            {
                nickname = !string.IsNullOrEmpty(vrcUsername) ? vrcUsername : "VRCOSCUser";
            }

            // Always use PC hash for username (hashed with CRC32 to fit IRC username limit)
            // Use server's NICKLEN if available, otherwise default to 9
            string username;
            var pcHash = _hashing?.PcHash;
            if (!string.IsNullOrEmpty(pcHash))
            {
                // Hash the full PC hash with CRC32 to get a shorter hash (8 hex chars)
                var crc32Hash = HashingUtils.GenerateCrc32Hash(pcHash);
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
            if (!string.IsNullOrEmpty(pcHash))
            {
                // Use full PC hash, but truncate if it exceeds server limit
                if (pcHash.Length > _isupportParser.RealNameLen)
                {
                    realName = pcHash.Substring(0, _isupportParser.RealNameLen);
                }
                else
                {
                    realName = pcHash;
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
            if (ex.StackTrace != null && GetSettingValue<bool>(IRCBridgeSetting.LogSystemMessages))
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
            if (GetSettingValue<bool>(IRCBridgeSetting.LogChatMessages))
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

    public async Task DisconnectFromIRCAsync(string? quitReason = null)
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
            
            // Disconnect and send QUIT message with reason
            _ircClient.Disconnect(quitReason);
            
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
    
    public void DisconnectFromIRC(string? quitReason = null)
    {
        // Synchronous version for backwards compatibility
        _ = DisconnectFromIRCAsync(quitReason ?? "Manual disconnect");
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
}
