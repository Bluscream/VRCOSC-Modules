// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Reflection;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.VRChat;

namespace Bluscream.Modules;

public class VRChat : IVRCClientEventHandler, IDisposable
{
    private readonly Player _player;
    
    // Cached values
    private string? _cachedVrcUserId;
    private string? _cachedVrcUsername;
    
    // Events
    public event Action<string?, string?>? OnUserIdChanged;
    public event Action<string?, string?>? OnUsernameChanged;
    
    public VRChat(Player player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
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
            var userProperty = typeof(Player).GetProperty("User", BindingFlags.Public | BindingFlags.Instance);
            if (userProperty?.GetValue(_player) is { } vrcUser)
            {
                var userIdProperty = vrcUser.GetType().GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
                var usernameProperty = vrcUser.GetType().GetProperty("Username", BindingFlags.Public | BindingFlags.Instance);
                
                var newUserId = userIdProperty?.GetValue(vrcUser) as string;
                var newUsername = usernameProperty?.GetValue(vrcUser) as string;
                
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
    
    public async void OnUserAuthenticated(VRChatClientEventUserAuthenticated eventArgs)
    {
        if (eventArgs.User == null)
        {
            return;
        }
        
        var newUserId = eventArgs.User.UserId;
        var newUsername = eventArgs.User.Username;
        
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
