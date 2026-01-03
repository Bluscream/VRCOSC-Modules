// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Modules.Attributes;
using Bluscream;

namespace Bluscream.Modules;

public class Hashing : IDisposable
{
    private readonly HttpClient _httpClient;
    
    // Cached values (not hashes)
    private IPAddress? _cachedExternalIp;
    private string? _cachedPcHash;
    
    // Events
    public event Action<IPAddress?, IPAddress?>? OnExternalIpChanged;
    public event Action<string?, string?>? OnPcHashChanged;
    
    public Hashing(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }
    
    // Public properties to access cached values
    public IPAddress? ExternalIp => _cachedExternalIp;
    public string? PcHash => _cachedPcHash;
    
    // Hash getters (hash on demand)
    public string? ExternalIpHash => _cachedExternalIp != null ? HashingUtils.GenerateSha256Hash(_cachedExternalIp.ToString()) : null;
    
    public async Task InitializeAsync()
    {
        // Initialize PC hash
        await UpdatePcHashAsync();
        
        // Initialize external IP (async, don't wait)
        _ = UpdateExternalIpAsync();
    }
    
    public async Task UpdateExternalIpAsync()
    {
        try
        {
            // Try multiple services for reliability
            var services = new[]
            {
                "https://api.ipify.org",
                "https://icanhazip.com",
                "https://ifconfig.me/ip",
                "http://ip-api.com/line?fields=query"
            };
            
            IPAddress? newIp = null;
            foreach (var service in services)
            {
                try
                {
                    var response = await _httpClient.GetStringAsync(service);
                    var ipString = response.Trim();
                    
                    if (!string.IsNullOrEmpty(ipString) && IPAddress.TryParse(ipString, out var ip))
                    {
                        newIp = ip;
                        break;
                    }
                }
                catch
                {
                    // Try next service
                    continue;
                }
            }
            
            // Check if IP changed
            if (newIp != null && !Equals(newIp, _cachedExternalIp))
            {
                var oldIp = _cachedExternalIp;
                _cachedExternalIp = newIp;
                OnExternalIpChanged?.Invoke(oldIp, newIp);
            }
            else if (newIp == null && _cachedExternalIp != null)
            {
                // IP fetch failed, clear cache
                var oldIp = _cachedExternalIp;
                _cachedExternalIp = null;
                OnExternalIpChanged?.Invoke(oldIp, null);
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
    public async Task UpdatePcHashAsync()
    {
        try
        {
            var newPcHash = GenerateHardwareId();
            
            if (newPcHash != _cachedPcHash)
            {
                var oldPcHash = _cachedPcHash;
                _cachedPcHash = newPcHash;
                OnPcHashChanged?.Invoke(oldPcHash, newPcHash);
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
    [ModuleUpdate(ModuleUpdateMode.Custom, false, 60000)] // Every 60 seconds
    private async void CheckForChanges()
    {
        await UpdatePcHashAsync();
        await UpdateExternalIpAsync();
    }
    
    #region Static Hash Generation Methods
    
    public static string GenerateHardwareId()
    {
        try
        {
            var components = new StringBuilder();
            
            // Get all CPU IDs (sorted for consistency)
            var cpuIds = GetAllCpuIds();
            if (cpuIds.Count > 0)
            {
                foreach (var cpuId in cpuIds)
                {
                    components.Append(cpuId);
                }
            }
            
            // Get motherboard serial number
            var motherboardId = GetMotherboardId();
            if (!string.IsNullOrEmpty(motherboardId))
            {
                components.Append(motherboardId);
            }
            
            // Get all GPU IDs (sorted for consistency)
            var gpuIds = GetAllGpuIds();
            if (gpuIds.Count > 0)
            {
                foreach (var gpuId in gpuIds)
                {
                    components.Append(gpuId);
                }
            }
            
            if (components.Length == 0)
            {
                return string.Empty;
            }
            
            // Generate SHA256 hash of all components
            var componentsString = components.ToString();
            return HashingUtils.GenerateSha256Hash(componentsString);
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private static List<string> GetAllCpuIds()
    {
        var cpuIds = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var processorId = obj["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(processorId))
                {
                    cpuIds.Add(processorId);
                }
            }
            // Sort for deterministic ordering
            cpuIds.Sort();
        }
        catch
        {
            // Ignore errors
        }
        return cpuIds;
    }
    
    private static string GetMotherboardId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                var serialNumber = obj["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serialNumber) && serialNumber != "To be filled by O.E.M." && serialNumber != "Default string")
                {
                    return serialNumber;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return string.Empty;
    }
    
    private static List<string> GetAllGpuIds()
    {
        var gpuIds = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var pnpDeviceId = obj["PNPDeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(pnpDeviceId))
                {
                    // Use the full PNPDeviceID as it's unique per GPU
                    gpuIds.Add(pnpDeviceId);
                }
            }
            // Sort for deterministic ordering
            gpuIds.Sort();
        }
        catch
        {
            // Ignore errors
        }
        return gpuIds;
    }
    
    #endregion
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
