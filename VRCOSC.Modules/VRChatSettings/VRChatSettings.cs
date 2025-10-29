// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Bluscream.Modules.Providers;

namespace Bluscream.Modules;

/// <summary>
/// Main VRChat settings accessor with registry and config file support
/// Uses provider pattern to load definitions at runtime
/// </summary>
public class VRChatSettings
{
    private readonly VRChatSettingsModule _module;
    private readonly VRChatRegistrySettingsProvider _registryProvider;
    private readonly VRChatConfigSettingsProvider _configProvider;

    private const string RegistryPath = @"SOFTWARE\VRChat\VRChat";
    
    private readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "VRChat", "VRChat", "config.json"
    );

    public VRChatSettings(VRChatSettingsModule module)
    {
        _module = module;
        _registryProvider = new VRChatRegistrySettingsProvider(module);
        _configProvider = new VRChatConfigSettingsProvider(module);
    }

    /// <summary>
    /// Initialize and load all definitions
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        var allowRemote = _module.AllowRemoteDefinitions;

        var registryTask = _registryProvider.LoadAsync(allowRemote);
        var configTask = _configProvider.LoadAsync(allowRemote);

        var results = await Task.WhenAll(registryTask, configTask);

        return results.All(r => r);
    }

    public bool IsLoaded => _registryProvider.IsLoaded && _configProvider.IsLoaded;

    public int GetTotalSettingsCount()
    {
        return _registryProvider.Settings.Count + _configProvider.Settings.Count;
    }

    #region Helper Methods

    private string AddHashToKeyName(string key)
    {
        uint hash = 5381;
        foreach (var c in key)
            hash = (hash * 33) ^ c;
        return key + "_h" + hash;
    }

    #endregion

    #region Registry Operations

    public bool GetRegistrySetting<T>(string key, out T? value, out string error)
    {
        value = default;
        error = string.Empty;

        try
        {
            var setting = _registryProvider.GetSetting(key);
            if (setting == null && !_module.AllowUnknownSettings)
            {
                error = $"Unknown setting '{key}'";
                return false;
            }

            var keyName = AddHashToKeyName(key);
            using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath);
            
            if (regKey == null)
            {
                error = "VRChat registry key not found";
                return false;
            }

            var data = regKey.GetValue(keyName);
            if (data == null)
            {
                error = $"Setting '{key}' not found";
                return false;
            }

            var type = regKey.GetValueKind(keyName);
            value = ConvertRegistryValue<T>(data, type, setting?.ValueType ?? VRChatValueType.String);

            if (_module.LogOperations)
            {
                _module.Log($"Registry GET: {key} = {value}");
            }

            _module.UpdateVariables(key, value?.ToString() ?? string.Empty, false);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error reading '{key}': {ex.Message}";
            return false;
        }
    }

    public bool SetRegistrySetting<T>(string key, T value, out string error)
    {
        error = string.Empty;

        try
        {
            var setting = _registryProvider.GetSetting(key);
            
            if (setting == null && !_module.AllowUnknownSettings)
            {
                error = $"Unknown setting '{key}'";
                return false;
            }

            if (setting != null && !_module.AllowOutsideLimits)
            {
                if (!_registryProvider.ValidateValue(key, value!, out var validationError))
                {
                    error = validationError;
                    return false;
                }
            }

            if (_module.AutoBackup)
            {
                BackupRegistry();
            }

            var keyName = AddHashToKeyName(key);
            using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            
            if (regKey == null)
            {
                error = "VRChat registry key not found";
                return false;
            }

            var valueType = setting?.ValueType ?? VRChatValueType.String;
            SetRegistryValue(regKey, keyName, value!, valueType);

            if (_module.LogOperations)
            {
                _module.Log($"Registry SET: {key} = {value}");
            }

            _module.UpdateVariables(key, value?.ToString() ?? string.Empty, true);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error writing '{key}': {ex.Message}";
            return false;
        }
    }

    private T ConvertRegistryValue<T>(object data, RegistryValueKind type, VRChatValueType targetType)
    {
        object result = data;

        switch (type)
        {
            case RegistryValueKind.Binary:
                var str = Encoding.ASCII.GetString((byte[])data).TrimEnd('\0');
                result = str;
                break;

            case RegistryValueKind.DWord:
                var intVal = Convert.ToInt32(data);
                result = targetType == VRChatValueType.Bool ? intVal != 0 : intVal;
                break;

            default:
                if (type == (RegistryValueKind)0x0004) // REG_QWORD
                {
                    if (data is long longValue)
                    {
                        var bytes = BitConverter.GetBytes(longValue);
                        result = BitConverter.ToDouble(bytes, 0);
                    }
                }
                break;
        }

        return (T)Convert.ChangeType(result, typeof(T));
    }

    private void SetRegistryValue<T>(RegistryKey regKey, string keyName, T value, VRChatValueType valueType)
    {
        switch (valueType)
        {
            case VRChatValueType.String:
                regKey.SetValue(keyName, Encoding.ASCII.GetBytes(value!.ToString()! + "\0"), RegistryValueKind.Binary);
                break;

            case VRChatValueType.Bool:
                regKey.SetValue(keyName, Convert.ToBoolean(value) ? 1 : 0, RegistryValueKind.DWord);
                break;

            case VRChatValueType.Int:
                regKey.SetValue(keyName, Convert.ToInt32(value), RegistryValueKind.DWord);
                break;

            case VRChatValueType.Float:
                var doubleVal = Convert.ToDouble(value);
                var bytes = BitConverter.GetBytes(doubleVal);
                var longVal = BitConverter.ToInt64(bytes, 0);
                regKey.SetValue(keyName, longVal, (RegistryValueKind)4); // REG_QWORD
                break;
        }
    }

    public Dictionary<string, object> ListAllRegistrySettings(out string error)
    {
        var settings = new Dictionary<string, object>();
        error = string.Empty;

        try
        {
            using var regKey = Registry.CurrentUser.OpenSubKey(RegistryPath);
            
            if (regKey == null)
            {
                error = "VRChat registry key not found";
                return settings;
            }

            foreach (var valueName in regKey.GetValueNames())
            {
                var index = valueName.LastIndexOf("_h", StringComparison.Ordinal);
                if (index <= 0) continue;

                var keyName = valueName.Substring(0, index);
                
                if (!_module.AllowUnknownSettings && !_registryProvider.IsKnownSetting(keyName))
                    continue;

                if (GetRegistrySetting<object>(keyName, out var value, out _) && value != null)
                {
                    settings[keyName] = value;
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            error = $"Error listing registry: {ex.Message}";
            return settings;
        }
    }

    private void BackupRegistry()
    {
        try
        {
            var backupDir = string.IsNullOrEmpty(_module.BackupDirectory) 
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCOSC", "Backups", "VRChatSettings")
                : _module.BackupDirectory;

            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(backupDir, $"vrchat_registry_backup_{timestamp}.reg");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $"export \"HKCU\\{RegistryPath}\" \"{backupFile}\" /y",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            process?.WaitForExit(5000);

            if (_module.LogOperations)
            {
                _module.Log($"Registry backed up to: {backupFile}");
            }
        }
        catch (Exception ex)
        {
            _module.Log($"Warning: Failed to backup registry: {ex.Message}");
        }
    }

    #endregion

    #region Config File Operations

    public bool GetConfigSetting<T>(string key, out T? value, out string error)
    {
        value = default;
        error = string.Empty;

        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                error = $"Config file not found";
                return false;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            var keys = key.Split('.');
            System.Text.Json.JsonElement current = doc.RootElement;

            foreach (var k in keys)
            {
                if (!current.TryGetProperty(k, out var next))
                {
                    error = $"Key '{key}' not found";
                    return false;
                }
                current = next;
            }

            value = System.Text.Json.JsonSerializer.Deserialize<T>(current.GetRawText());

            if (_module.LogOperations)
            {
                _module.Log($"Config GET: {key} = {value}");
            }

            _module.UpdateVariables(key, value?.ToString() ?? string.Empty, false);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error reading config '{key}': {ex.Message}";
            return false;
        }
    }

    public bool SetConfigSetting<T>(string key, T value, out string error)
    {
        error = string.Empty;

        try
        {
            var setting = _configProvider.GetSetting(key);
            
            if (setting == null && !_module.AllowUnknownSettings)
            {
                error = $"Unknown setting '{key}'";
                return false;
            }

            if (setting != null && !_module.AllowOutsideLimits)
            {
                if (!_configProvider.ValidateValue(key, value!, out var validationError))
                {
                    error = validationError;
                    return false;
                }
            }

            if (_module.AutoBackup)
            {
                BackupConfigFile();
            }

            if (!File.Exists(ConfigFilePath))
            {
                error = "Config file not found";
                return false;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var root = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (root == null)
            {
                error = "Failed to parse config file";
                return false;
            }

            // Navigate and set value (simplified - full implementation would handle nested objects)
            root[key] = value!;

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var updatedJson = System.Text.Json.JsonSerializer.Serialize(root, options);
            File.WriteAllText(ConfigFilePath, updatedJson);

            if (_module.LogOperations)
            {
                _module.Log($"Config SET: {key} = {value}");
            }

            _module.UpdateVariables(key, value?.ToString() ?? string.Empty, true);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Error writing config '{key}': {ex.Message}";
            return false;
        }
    }

    public Dictionary<string, object> ListAllConfigSettings(out string error)
    {
        var settings = new Dictionary<string, object>();
        error = string.Empty;

        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                error = "Config file not found";
                return settings;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var doc = System.Text.Json.JsonDocument.Parse(json);

            ExtractConfigKeys(doc.RootElement, string.Empty, settings);

            if (!_module.AllowUnknownSettings)
            {
                var filtered = new Dictionary<string, object>();
                foreach (var kvp in settings)
                {
                    if (_configProvider.IsKnownSetting(kvp.Key))
                    {
                        filtered[kvp.Key] = kvp.Value;
                    }
                }
                return filtered;
            }

            return settings;
        }
        catch (Exception ex)
        {
            error = $"Error listing config: {ex.Message}";
            return settings;
        }
    }

    private void ExtractConfigKeys(System.Text.Json.JsonElement element, string prefix, Dictionary<string, object> keys)
    {
        if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                
                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object || 
                    prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    ExtractConfigKeys(prop.Value, key, keys);
                }
                else
                {
                    keys[key] = prop.Value.ToString();
                }
            }
        }
    }

    private void BackupConfigFile()
    {
        try
        {
            var backupDir = string.IsNullOrEmpty(_module.BackupDirectory) 
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VRCOSC", "Backups", "VRChatSettings")
                : _module.BackupDirectory;

            Directory.CreateDirectory(backupDir);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(backupDir, $"vrchat_config_backup_{timestamp}.json");

            File.Copy(ConfigFilePath, backupFile, true);

            if (_module.LogOperations)
            {
                _module.Log($"Config backed up to: {backupFile}");
            }
        }
        catch (Exception ex)
        {
            _module.Log($"Warning: Failed to backup config file: {ex.Message}");
        }
    }

    #endregion
}
