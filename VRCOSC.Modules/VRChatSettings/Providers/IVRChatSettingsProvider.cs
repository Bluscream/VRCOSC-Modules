// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Bluscream.Modules.Providers;

/// <summary>
/// Base interface for VRChat settings providers
/// </summary>
public interface IVRChatSettingsProvider<T> where T : class
{
    /// <summary>
    /// All loaded settings
    /// </summary>
    Dictionary<string, T> Settings { get; }

    /// <summary>
    /// Whether settings have been loaded successfully
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Load settings (tries remote first, then embedded)
    /// </summary>
    Task<bool> LoadAsync(bool allowRemote = true);

    /// <summary>
    /// Load from embedded resource
    /// </summary>
    Task<bool> LoadEmbeddedAsync();

    /// <summary>
    /// Load from remote URL
    /// </summary>
    Task<bool> LoadRemoteAsync(string url);

    /// <summary>
    /// Check if a setting key exists
    /// </summary>
    bool IsKnownSetting(string key);

    /// <summary>
    /// Get a specific setting
    /// </summary>
    T? GetSetting(string key);

    /// <summary>
    /// Validate a value against setting constraints
    /// </summary>
    bool ValidateValue(string key, object value, out string error);

    /// <summary>
    /// Get setting by category
    /// </summary>
    Dictionary<string, T> GetSettingsByCategory(string category);

    /// <summary>
    /// Get all categories
    /// </summary>
    List<string> GetCategories();
}

/// <summary>
/// Base abstract provider with common functionality
/// </summary>
public abstract class VRChatSettingsProviderBase<T> : IVRChatSettingsProvider<T> where T : class
{
    protected readonly VRChatSettingsModule _module;
    protected readonly HttpClient _httpClient;

    public Dictionary<string, T> Settings { get; protected set; } = new();
    public bool IsLoaded { get; protected set; }

    protected VRChatSettingsProviderBase(VRChatSettingsModule module)
    {
        _module = module;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public abstract Task<bool> LoadAsync(bool allowRemote = true);
    public abstract Task<bool> LoadEmbeddedAsync();
    public abstract Task<bool> LoadRemoteAsync(string url);
    protected abstract Task<bool> ParseAndLoadSettings(string content);
    protected abstract string GetEmbeddedResourceName();
    public abstract bool ValidateValue(string key, object value, out string error);

    public bool IsKnownSetting(string key) => Settings.ContainsKey(key);

    public T? GetSetting(string key) => Settings.TryGetValue(key, out var setting) ? setting : null;

    public abstract Dictionary<string, T> GetSettingsByCategory(string category);
    public abstract List<string> GetCategories();

    protected async Task<string?> DownloadContentAsync(string url)
    {
        try
        {
            if (_module.LogOperations)
            {
                _module.Log($"Attempting to download from: {url}");
            }

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            
            if (_module.LogOperations)
            {
                _module.Log($"Successfully downloaded {content.Length} bytes");
            }

            return content;
        }
        catch (Exception ex)
        {
            if (_module.LogOperations)
            {
                _module.Log($"Failed to download from {url}: {ex.Message}");
            }
            return null;
        }
    }

    protected async Task<string?> LoadEmbeddedResourceAsync()
    {
        try
        {
            var assembly = typeof(VRChatSettingsModule).Assembly;
            var resourceName = GetEmbeddedResourceName();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                if (_module.LogOperations)
                {
                    _module.Log($"Embedded resource not found: {resourceName}");
                }
                return null;
            }

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            if (_module.LogOperations)
            {
                _module.Log($"Loaded embedded resource: {resourceName} ({content.Length} bytes)");
            }

            return content;
        }
        catch (Exception ex)
        {
            if (_module.LogOperations)
            {
                _module.Log($"Failed to load embedded resource: {ex.Message}");
            }
            return null;
        }
    }
}
