// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Bluscream.Modules;

public static class Hashing
{
    public static string GenerateHardwareId(Action<string>? logAction = null)
    {
        try
        {
            var components = new StringBuilder();
            
            // Get CPU serial number
            var cpuId = GetCpuId();
            if (!string.IsNullOrEmpty(cpuId))
            {
                components.Append(cpuId);
            }
            
            // Get motherboard serial number
            var motherboardId = GetMotherboardId();
            if (!string.IsNullOrEmpty(motherboardId))
            {
                components.Append(motherboardId);
            }
            
            // Get GPU serial number
            var gpuId = GetGpuId();
            if (!string.IsNullOrEmpty(gpuId))
            {
                components.Append(gpuId);
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
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var processorId = obj["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(processorId))
                {
                    return processorId;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return string.Empty;
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
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var pnpDeviceId = obj["PNPDeviceID"]?.ToString();
                if (!string.IsNullOrEmpty(pnpDeviceId))
                {
                    // Extract device ID from PNPDeviceID (format: PCI\\VEN_XXXX&DEV_XXXX&...)
                    // Use the full PNPDeviceID as it's unique per GPU
                    return pnpDeviceId;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return string.Empty;
    }
}
