// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bluscream.Modules.Providers;

/// <summary>
/// Registry settings provider - loads from CSV
/// </summary>
public class VRChatRegistrySettingsProvider : VRChatSettingsProviderBase<VRChatRegistrySetting>
{
    private const string RemoteUrl = "https://gist.github.com/Bluscream/393a8a88b37486f67e9d12b4c615183a/raw/registry.csv";
    private const string EmbeddedResource = "Bluscream.Modules.Definitions.Registry.csv";

    public VRChatRegistrySettingsProvider(VRChatSettingsModule module) : base(module) { }

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
                _module.Log($"Loaded {Settings.Count} registry settings from remote source");
                return true;
            }
        }

        // Fallback to embedded
        if (await LoadEmbeddedAsync())
        {
            _module.Log($"Loaded {Settings.Count} registry settings from embedded resource");
            return true;
        }

        _module.Log("Failed to load registry settings from any source");
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

    protected override async Task<bool> ParseAndLoadSettings(string csvContent)
    {
        try
        {
            Settings.Clear();

            using var reader = new StringReader(csvContent);
            
            // Skip header
            var header = await reader.ReadLineAsync();
            if (header == null)
                return false;

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var setting = ParseCsvLine(line);
                if (setting != null)
                {
                    Settings[setting.Key] = setting;
                }
            }

            IsLoaded = Settings.Count > 0;
            return IsLoaded;
        }
        catch (Exception ex)
        {
            if (_module.LogOperations)
            {
                _module.Log($"Failed to parse CSV: {ex.Message}");
            }
            return false;
        }
    }

    private VRChatRegistrySetting? ParseCsvLine(string line)
    {
        try
        {
            // Parse CSV with quoted fields
            var fields = ParseCsvFields(line);
            if (fields.Length < 3)
                return null;

            var keyName = fields[0];
            var valueTypeStr = fields[1];
            var description = fields[2];
            var defaultValue = fields.Length > 3 ? fields[3] : string.Empty;
            var pattern = fields.Length > 4 ? fields[4] : string.Empty;

            // Determine value type from registry type
            var valueType = VRChatValueType.String;
            if (valueTypeStr.Contains("QWORD") || valueTypeStr.Contains("float"))
                valueType = VRChatValueType.Float;
            else if (valueTypeStr.Contains("DWORD"))
            {
                // Could be int or bool - infer from description
                if (description.Contains("Enable/disable") || description.Contains("toggle"))
                    valueType = VRChatValueType.Bool;
                else
                    valueType = VRChatValueType.Int;
            }
            else if (valueTypeStr.Contains("BINARY"))
                valueType = VRChatValueType.String;

            // Determine category from key prefix or description
            var category = DetermineCategory(keyName, description);

            return new VRChatRegistrySetting
            {
                Key = keyName,
                DisplayName = FormatDisplayName(keyName),
                Description = description,
                ValueType = valueType,
                DefaultValue = ParseDefaultValue(defaultValue, valueType),
                Category = category,
                Pattern = pattern
            };
        }
        catch
        {
            return null;
        }
    }

    private string[] ParseCsvFields(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var currentField = string.Empty;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(currentField);
                currentField = string.Empty;
            }
            else
            {
                currentField += ch;
            }
        }

        fields.Add(currentField);
        return fields.ToArray();
    }

    private string DetermineCategory(string key, string description)
    {
        if (key.StartsWith("AUDIO_")) return "Audio";
        if (key.StartsWith("CustomTrustLevel_")) return "Safety";
        if (key.Contains("avatar") || key.Contains("Avatar")) return "Avatar";
        if (key.Contains("Region") || key.Contains("Network")) return "Network";
        if (description.Contains("Safety:")) return "Safety";
        
        return "General";
    }

    private string FormatDisplayName(string key)
    {
        return key.Replace("_", " ");
    }

    private object? ParseDefaultValue(string value, VRChatValueType type)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        try
        {
            switch (type)
            {
                case VRChatValueType.Bool:
                    return value.Contains("0x1") || value == "1" || value.ToLower() == "true";
                case VRChatValueType.Int:
                    if (value.StartsWith("0x"))
                        return Convert.ToInt32(value.Substring(2), 16);
                    return int.TryParse(value, out var intVal) ? intVal : 0;
                case VRChatValueType.Float:
                    // Parse hex double format
                    if (value.StartsWith("hex(4):"))
                    {
                        // Parse hex(4): format as bytes and convert to double
                        var hexBytesStr = value.Substring("hex(4):".Length)
                            .Replace(",", "")
                            .Replace(" ", "")
                            .Replace("\\", "")
                            .Replace("\r", "")
                            .Replace("\n", "");

                        // Remove any extraneous non-hex characters
                        var cleanHex = new string(hexBytesStr.Where(c => "0123456789abcdefABCDEF".Contains(c)).ToArray());

                        // hex(4): should mean 4 hex bytes, i.e. 8 hex chars
                        if (cleanHex.Length == 8)
                        {
                            try
                            {
                                var bytes = new byte[4];
                                for (int i = 0; i < 4; i++)
                                    bytes[i] = Convert.ToByte(cleanHex.Substring(i * 2, 2), 16);

                                // Registry stores little endian, so ensure correct order
                                var floatValue = BitConverter.ToSingle(bytes, 0);
                                return (double)floatValue;
                            }
                            catch
                            {
                                return 0.0;
                            }
                        }
                        else
                        {
                            return 0.0;
                        }
                    }
                    return double.TryParse(value, out var doubleVal) ? doubleVal : 0.0;
                default:
                    return value;
            }
        }
        catch
        {
            return null;
        }
    }

    public override bool ValidateValue(string key, object value, out string error)
    {
        error = string.Empty;

        if (!Settings.TryGetValue(key, out var setting))
        {
            error = $"Unknown registry setting: {key}";
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

    public override Dictionary<string, VRChatRegistrySetting> GetSettingsByCategory(string category)
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
/// Registry setting definition
/// </summary>
public class VRChatRegistrySetting
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public VRChatValueType ValueType { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public object? DefaultValue { get; set; }
    public string Category { get; set; } = "General";
    public string Pattern { get; set; } = string.Empty;
}
