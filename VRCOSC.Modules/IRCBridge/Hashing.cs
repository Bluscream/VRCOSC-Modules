// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Management;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Bluscream.Modules;

public static class Hashing
{
    public static string GenerateHardwareId(Action<string>? logAction = null)
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
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(componentsString));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            
            // Only log the final hash
            logAction?.Invoke($"PC Hash: {hashString}");
            
            return hashString;
        }
        catch (Exception ex)
        {
            logAction?.Invoke($"Exception during PC hash generation: {ex.Message}");
            return string.Empty;
        }
    }
    
    private static string GetCpuId()
    {
        // Legacy method - use GetAllCpuIds() for consistency
        var cpuIds = GetAllCpuIds();
        return cpuIds.Count > 0 ? cpuIds[0] : string.Empty;
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
    
    private static string GetGpuId()
    {
        // Legacy method - use GetAllGpuIds() for consistency
        var gpuIds = GetAllGpuIds();
        return gpuIds.Count > 0 ? gpuIds[0] : string.Empty;
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
    
    /// <summary>
    /// Generate SHA256 hash of a string input
    /// </summary>
    public static string GenerateSha256Hash(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        
        try
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Get external IP address and return its SHA256 hash
    /// </summary>
    public static async Task<string> GetExternalIpHashAsync(HttpClient httpClient, Action<string>? logAction = null)
    {
        try
        {
            // Try multiple services for reliability
            var services = new[]
            {
                "https://api.ipify.org",
                "https://icanhazip.com",
                "https://ifconfig.me/ip"
            };
            
            foreach (var service in services)
            {
                try
                {
                    var response = await httpClient.GetStringAsync(service);
                    var ip = response.Trim();
                    
                    if (!string.IsNullOrEmpty(ip))
                    {
                        // Hash the IP with SHA256
                        var hash = GenerateSha256Hash(ip);
                        if (!string.IsNullOrEmpty(hash))
                        {
                            logAction?.Invoke($"External IP Hash: {hash}");
                            return hash;
                        }
                    }
                }
                catch
                {
                    // Try next service
                    continue;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        logAction?.Invoke("External IP Hash: (fetch failed)");
        return string.Empty;
    }
}
