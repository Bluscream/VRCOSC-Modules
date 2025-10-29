// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bluscream.Modules.Providers;

/// <summary>
/// Config file settings provider - loads from JSON schema
/// </summary>
public class VRChatConfigSettingsProvider : VRChatSettingsProviderBase<VRChatConfigSetting>
{
    private const string RemoteUrl = "https://gist.github.com/Bluscream/393a8a88b37486f67e9d12b4c615183a/raw/config.schema.json";
    private const string EmbeddedResource = "Bluscream.Modules.Definitions.config.schema.json";

    public VRChatConfigSettingsProvider(VRChatSettingsModule module) : base(module) { }

    protected override string GetEmbeddedResourceName() => EmbeddedResource;

    public override async Task<bool> LoadAsync(bool allowRemote = true)
    {
        if (IsLoaded)
            return true;

        // Try remote first if allowed
        if (allowRemote && _module.AllowRemoteDefinitions)
        {
            if (await LoadRemoteAsync(RemoteUrl))
            {
                _module.Log($"Loaded {Settings.Count} config settings from remote source");
                return true;
            }
        }

        // Fallback to embedded
        if (await LoadEmbeddedAsync())
        {
            _module.Log($"Loaded {Settings.Count} config settings from embedded resource");
            return true;
        }

        _module.Log("Failed to load config settings from any source");
        return false;
    }

    public override async Task<bool> LoadRemoteAsync(string url)
    {
        var content = await DownloadContentAsync(url);
        if (content == null)
            return false;

        return await ParseAndLoadSettings(content);
    }

    public override async Task<bool> LoadEmbeddedAsync()
    {
        var content = await LoadEmbeddedResourceAsync();
        if (content == null)
            return false;

        return await ParseAndLoadSettings(content);
    }

    protected override Task<bool> ParseAndLoadSettings(string jsonContent)
    {
        try
        {
            Settings.Clear();

            var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Parse JSON Schema
            if (root.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    var setting = ParseSchemaProperty(property.Name, property.Value);
                    if (setting != null)
                    {
                        Settings[setting.Key] = setting;
                    }
                }
            }

            IsLoaded = Settings.Count > 0;
            return Task.FromResult(IsLoaded);
        }
        catch (Exception ex)
        {
            if (_module.LogOperations)
            {
                _module.Log($"Failed to parse JSON schema: {ex.Message}");
            }
            return Task.FromResult(false);
        }
    }

    private VRChatConfigSetting? ParseSchemaProperty(string key, JsonElement element)
    {
        try
        {
            var description = element.TryGetProperty("description", out var desc) ? desc.GetString() : string.Empty;
            var typeStr = element.TryGetProperty("type", out var type) ? type.GetString() : "string";

            // Determine value type
            var valueType = typeStr?.ToLower() switch
            {
                "integer" => VRChatValueType.Int,
                "number" => VRChatValueType.Float,
                "boolean" => VRChatValueType.Bool,
                _ => VRChatValueType.String
            };

            object? minValue = null;
            object? maxValue = null;
            object? defaultValue = null;

            if (element.TryGetProperty("minimum", out var min))
            {
                minValue = valueType == VRChatValueType.Int ? min.GetInt32() : (object)min.GetDouble();
            }

            if (element.TryGetProperty("maximum", out var max))
            {
                maxValue = valueType == VRChatValueType.Int ? max.GetInt32() : (object)max.GetDouble();
            }

            if (element.TryGetProperty("default", out var def))
            {
                defaultValue = ParseDefaultValue(def, valueType);
            }

            // Determine category
            var category = DetermineCategory(key, description ?? string.Empty);

            // Check if requires restart (usually file/path changes do)
            var requiresRestart = key.Contains("directory") || key.Contains("path") || 
                                description?.Contains("restart") == true;

            return new VRChatConfigSetting
            {
                Key = key,
                DisplayName = FormatDisplayName(key),
                Description = description ?? string.Empty,
                ValueType = valueType,
                MinValue = minValue,
                MaxValue = maxValue,
                DefaultValue = defaultValue,
                RequiresRestart = requiresRestart,
                Category = category
            };
        }
        catch
        {
            return null;
        }
    }

    private object? ParseDefaultValue(JsonElement element, VRChatValueType type)
    {
        try
        {
            return type switch
            {
                VRChatValueType.Bool => element.GetBoolean(),
                VRChatValueType.Int => element.GetInt32(),
                VRChatValueType.Float => element.GetDouble(),
                _ => element.GetString()
            };
        }
        catch
        {
            return null;
        }
    }

    private string DetermineCategory(string key, string description)
    {
        if (key.StartsWith("cache")) return "Cache";
        if (key.Contains("screenshot") || key.Contains("camera") || key.Contains("picture")) return "Visual";
        if (key.Contains("dynamic_bone") || key.Contains("particle")) return "Performance";
        if (key.Contains("vrcx")) return "VRCX";
        if (key.Contains("discord") || key.Contains("RichPresence")) return "Privacy";
        
        return "General";
    }

    private string FormatDisplayName(string key)
    {
        // Convert snake_case or camelCase to Title Case
        var words = key.Replace("_", " ").Split(' ');
        return string.Join(" ", words.Select(w => 
            w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1) : w));
    }

    public override bool ValidateValue(string key, object value, out string error)
    {
        error = string.Empty;

        if (!Settings.TryGetValue(key, out var setting))
        {
            error = $"Unknown config setting: {key}";
            return false;
        }

        try
        {
            switch (setting.ValueType)
            {
                case VRChatValueType.Float:
                    var floatValue = Convert.ToSingle(value);
                    if (setting.MinValue != null && floatValue < (double)setting.MinValue)
                    {
                        error = $"Value {floatValue} is below minimum {setting.MinValue}";
                        return false;
                    }
                    if (setting.MaxValue != null && floatValue > (double)setting.MaxValue)
                    {
                        error = $"Value {floatValue} is above maximum {setting.MaxValue}";
                        return false;
                    }
                    break;

                case VRChatValueType.Int:
                    var intValue = Convert.ToInt32(value);
                    if (setting.MinValue != null && intValue < (int)setting.MinValue)
                    {
                        error = $"Value {intValue} is below minimum {setting.MinValue}";
                        return false;
                    }
                    if (setting.MaxValue != null && intValue > (int)setting.MaxValue)
                    {
                        error = $"Value {intValue} is above maximum {setting.MaxValue}";
                        return false;
                    }
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Validation error: {ex.Message}";
            return false;
        }
    }

    public override Dictionary<string, VRChatConfigSetting> GetSettingsByCategory(string category)
    {
        return Settings.Where(kvp => kvp.Value.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public override List<string> GetCategories()
    {
        return Settings.Values
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }
}

/// <summary>
/// Config file setting definition
/// </summary>
public class VRChatConfigSetting
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public VRChatValueType ValueType { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public object? DefaultValue { get; set; }
    public bool RequiresRestart { get; set; }
    public string Category { get; set; } = "General";
}

/// <summary>
/// Value types
/// </summary>
public enum VRChatValueType
{
    String,
    Int,
    Float,
    Bool
}
