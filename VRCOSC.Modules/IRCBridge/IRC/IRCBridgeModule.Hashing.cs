// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using Bluscream;
using Bluscream.Modules.IRCBridge.Utils;

namespace Bluscream.Modules;

public partial class IRCBridgeModule
{
    private async Task SendClientDataAnnouncementWithDelayAsync(string channelName)
    {
        try
        {
            // Always wait at least 1 second after joining channel before broadcasting hashes
            await Task.Delay(1000);
            
            // Initialize last known hashes before first send
            if (string.IsNullOrEmpty(_lastPcHash))
            {
                // Wait a bit for external IP hash
                await Task.Delay(500);
                
                // Initialize tracking hashes (hash on demand)
                if (_vrchat != null && _hashing != null)
                {
                    _lastUserIdHash = !string.IsNullOrEmpty(_vrchat.UserId) ? HashingUtils.GenerateSha256Hash(_vrchat.UserId) : string.Empty;
                    _lastUsernameHash = !string.IsNullOrEmpty(_vrchat.Username) ? HashingUtils.GenerateSha256Hash(_vrchat.Username) : string.Empty;
                    _lastPcHash = _hashing.PcHash ?? string.Empty;
                    _lastExternalIpHash = _hashing.ExternalIpHash ?? string.Empty;
                }
            }
            
            await SendClientDataAnnouncementAsync(channelName);
        }
        catch (Exception ex)
        {
            Log($"Error sending client data announcement: {ex.Message}");
        }
    }
    
    private async Task SendClientDataAnnouncementAsync(string channelName)
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected || _hashing == null || _vrchat == null)
        {
            return;
        }
        
        try
        {
            // Get hashes (hash on demand from cached values)
            var externalIpHash = _hashing.ExternalIpHash ?? string.Empty;
            var pcHash = _hashing.PcHash ?? string.Empty;
            var userIdHash = !string.IsNullOrEmpty(_vrchat.UserId) ? HashingUtils.GenerateSha256Hash(_vrchat.UserId) : string.Empty;
            var username = _vrchat.Username ?? string.Empty;
            
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
    
    [ModuleUpdate(ModuleUpdateMode.Custom, false, 60000)] // Every 60 seconds
    private async void CheckHashChanges()
    {
        if (_ircClient?.Client == null || !_ircClient.IsConnected || _joinedChannel == null || _isStopping || _hashing == null || _vrchat == null)
        {
            return;
        }
        
        try
        {
            // Wait a bit for external IP hash to be fetched if needed
            await Task.Delay(500);
            
            // Calculate current hashes (hash on demand from cached values)
            var currentUserIdHash = !string.IsNullOrEmpty(_vrchat?.UserId) ? HashingUtils.GenerateSha256Hash(_vrchat.UserId) : string.Empty;
            var currentUsernameHash = !string.IsNullOrEmpty(_vrchat?.Username) ? HashingUtils.GenerateSha256Hash(_vrchat.Username) : string.Empty;
            var currentPcHash = _hashing.PcHash ?? string.Empty;
            var currentExternalIpHash = _hashing.ExternalIpHash ?? string.Empty;
            
            // Check if any hash changed
            bool hashChanged = false;
            if (currentUserIdHash != _lastUserIdHash ||
                currentUsernameHash != _lastUsernameHash ||
                currentPcHash != _lastPcHash ||
                currentExternalIpHash != _lastExternalIpHash)
            {
                hashChanged = true;
                Log("Hash change detected, resending welcome message");
            }
            
            // Update last known hashes
            _lastUserIdHash = currentUserIdHash;
            _lastUsernameHash = currentUsernameHash;
            _lastPcHash = currentPcHash;
            _lastExternalIpHash = currentExternalIpHash;
            
            // Resend welcome message if hash changed
            if (hashChanged && _joinedChannel != null)
            {
                await SendClientDataAnnouncementAsync(_joinedChannel.Name);
            }
        }
        catch (Exception ex)
        {
            Log($"Error checking hash changes: {ex.Message}");
        }
    }
}
