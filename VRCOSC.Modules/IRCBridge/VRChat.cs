// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Reflection;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.VRChat;
using VRCOSC.App.SDK.VRChat.Logs;
using VRCOSC.App.SDK.VRChat.Logs.Handlers;

namespace Bluscream.Modules;

public class VRChat : IVRCClientEventHandler, IDisposable
{
    private readonly VRChatClient _client;
    
    // Cached values
    private string? _cachedVrcUserId;
    private string? _cachedVrcUsername;
    
    // Events
    public event Action<string?, string?>? OnUserIdChanged;
    public event Action<string?, string?>? OnUsernameChanged;
    
    public VRChat(VRChatClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }
    
    // Public properties to access cached values
    public string? UserId => _cachedVrcUserId;
    public string? Username => _cachedVrcUsername;
    
    public async Task InitializeAsync()
    {
        await UpdateUserInfoAsync();
    }
    
    public async Task UpdateUserInfoAsync()
    {
        try
        {
            var vrcUser = _client.User;
            if (vrcUser != null)
            {
                var newUserId = vrcUser.Id;
                var newUsername = vrcUser.Username;
                
                // Check for changes and trigger events
                if (newUserId != _cachedVrcUserId)
                {
                    var oldUserId = _cachedVrcUserId;
                    _cachedVrcUserId = newUserId;
                    OnUserIdChanged?.Invoke(oldUserId, newUserId);
                }
                
                if (newUsername != _cachedVrcUsername)
                {
                    var oldUsername = _cachedVrcUsername;
                    _cachedVrcUsername = newUsername;
                    OnUsernameChanged?.Invoke(oldUsername, newUsername);
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
    #region IVRCClientEventHandler
    
    public async void HandleClientEvent(IVRChatClientEvent @event)
    {
        if (@event is not UserAuthenticatedClientEvent userAuthenticatedEvent || userAuthenticatedEvent.User == null)
        {
            return;
        }
        
        var newUserId = userAuthenticatedEvent.User.Id;
        var newUsername = userAuthenticatedEvent.User.Username;
        
        // Check if username or user ID changed
        bool userIdChanged = newUserId != _cachedVrcUserId;
        bool usernameChanged = newUsername != _cachedVrcUsername;
        
        if (userIdChanged)
        {
            var oldUserId = _cachedVrcUserId;
            _cachedVrcUserId = newUserId;
            OnUserIdChanged?.Invoke(oldUserId, newUserId);
        }
        
        if (usernameChanged)
        {
            var oldUsername = _cachedVrcUsername;
            _cachedVrcUsername = newUsername;
            OnUsernameChanged?.Invoke(oldUsername, newUsername);
        }
        
        // Also update from reflection as fallback
        await UpdateUserInfoAsync();
    }
    
    #endregion
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
