// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Linq;
using System.Threading.Tasks;
using IrcDotNet;

namespace Bluscream.Modules;

public class IRCClient : IDisposable
{
    private StandardIrcClient? _client;
    private bool _isConnected = false;
    private bool _isConnecting = false;

    public bool IsConnected => _isConnected && (_client?.IsConnected ?? false);
    public bool IsConnecting => _isConnecting;
    
    // Expose the underlying StandardIrcClient for direct event subscription
    public StandardIrcClient? Client => _client;

    public event Action<string>? MessageReceived;
    public event Action<string>? RawMessageSent; // For logging outgoing messages
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<Exception>? Error;

    private readonly Action<string> _logAction;

    public IRCClient(Action<string> logAction)
    {
        _logAction = logAction;
    }

    public async Task ConnectAsync(string serverAddress, int serverPort, bool useSSL, string? password = null, string nickname = "VRCOSCUser", string username = "vrcosc", string realName = "VRCOSC IRC Bridge")
    {
        if (_isConnected || _isConnecting)
        {
            return;
        }

        _isConnecting = true;

        try
        {
            _client = new StandardIrcClient();
            
            // Add flood preventer (best practice from IrcBot sample)
            _client.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
            
            // Wire up events
            _client.Connected += (sender, e) =>
            {
                _isConnected = true;
                _isConnecting = false;
                Connected?.Invoke();
            };

            _client.Disconnected += (sender, e) =>
            {
                _isConnected = false;
                _isConnecting = false;
                Disconnected?.Invoke();
            };

            _client.Error += (sender, e) =>
            {
                Error?.Invoke(e.Error);
            };

            _client.RawMessageReceived += (sender, e) =>
            {
                try
                {
                    // Reconstruct raw IRC message from IrcMessage object
                    var message = e.Message;
                    var rawMessage = string.Empty;
                    
                    // Add prefix if present
                    if (message.Prefix != null)
                    {
                        rawMessage += $":{message.Prefix} ";
                    }
                    
                    // Add command (can be null for some message types)
                    if (message.Command != null)
                    {
                        rawMessage += message.Command;
                    }
                    
                    // Add parameters
                    if (message.Parameters != null && message.Parameters.Count > 0)
                    {
                        for (int i = 0; i < message.Parameters.Count; i++)
                        {
                            var param = message.Parameters[i];
                            if (param == null) continue;
                            
                            // Last parameter that contains spaces should be prefixed with :
                            if (i == message.Parameters.Count - 1 && param.Contains(' '))
                            {
                                rawMessage += $" :{param}";
                            }
                            else
                            {
                                rawMessage += $" {param}";
                            }
                        }
                    }
                    
                    // Only invoke MessageReceived for incoming messages
                    MessageReceived?.Invoke(rawMessage);
                }
                catch (Exception ex)
                {
                    _logAction($"Error processing received message: {ex.Message}");
                }
            };

            _client.RawMessageSent += (sender, e) =>
            {
                try
                {
                    if (e?.Message == null)
                    {
                        return;
                    }

                    // Simplified: just log command and first parameter for sent messages
                    var message = e.Message;
                    if (message.Command != null)
                    {
                        var logMsg = message.Command;
                        if (message.Parameters != null && message.Parameters.Count > 0 && message.Parameters[0] != null)
                        {
                            logMsg += $" {message.Parameters[0]}";
                        }
                        RawMessageSent?.Invoke(logMsg);
                    }
                }
                catch (Exception ex)
                {
                    _logAction($"Error processing sent message: {ex.Message}");
                }
            };

            // Create registration info (IrcDotNet handles password automatically)
            var registrationInfo = new IrcUserRegistrationInfo
            {
                UserName = username,
                RealName = realName,
                NickName = nickname,
                Password = password // IrcDotNet will send PASS command automatically if password is set
            };

            // Connect (IrcDotNet handles password in registration automatically)
            await Task.Run(() =>
            {
                if (useSSL)
                {
                    _client.Connect(serverAddress, serverPort, useSsl: true, registrationInfo);
                }
                else
                {
                    _client.Connect(serverAddress, serverPort, useSsl: false, registrationInfo);
                }
            });
        }
        catch (Exception ex)
        {
            _isConnecting = false;
            _isConnected = false;
            Error?.Invoke(ex);
            throw;
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (_client == null || !_isConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        try
        {
            await Task.Run(() => _client.SendRawMessage(message));
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            throw;
        }
    }

    public void Disconnect()
    {
        if (!_isConnected && !_isConnecting)
        {
            return;
        }

        try
        {
            if (_client != null && _client.IsConnected)
            {
                _client.Quit("VRCOSC IRC Bridge disconnecting");
            }
            
            _client?.Disconnect();
            _isConnected = false;
            _isConnecting = false;
            Disconnected?.Invoke();
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }

    public void Dispose()
    {
        Disconnect();
        _client?.Dispose();
    }

    // Helper methods to access IrcDotNet features
    public IrcChannel? GetChannel(string channelName)
    {
        return _client?.Channels.FirstOrDefault(c => c.Name.Equals(channelName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task JoinChannelAsync(string channelName)
    {
        if (_client == null || !_isConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        await Task.Run(() => _client.Channels.Join(channelName));
    }

    public async Task LeaveChannelAsync(string channelName)
    {
        if (_client == null || !_isConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        var channel = GetChannel(channelName);
        if (channel != null)
        {
            await Task.Run(() => channel.Leave());
        }
    }

    public async Task SendChannelMessageAsync(string channelName, string message)
    {
        if (_client == null || !_isConnected)
        {
            throw new InvalidOperationException("Not connected to IRC server");
        }

        // Use raw message for PRIVMSG
        await SendMessageAsync($"PRIVMSG {channelName} :{message}");
    }
}
