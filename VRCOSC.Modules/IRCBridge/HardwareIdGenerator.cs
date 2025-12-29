// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Bluscream.Modules;

public static class HardwareIdGenerator
{
    public static string GenerateHardwareId(Action<string>? logAction = null)
    {
        try
        {
            var components = new StringBuilder();
            
            // Get CPU serial number
            var cpuId = GetCpuId();
            logAction?.Invoke($"CPU ID: {(string.IsNullOrEmpty(cpuId) ? "(empty)" : cpuId)}");
            if (!string.IsNullOrEmpty(cpuId))
            {
                components.Append(cpuId);
            }
            
            // Get motherboard serial number
            var motherboardId = GetMotherboardId();
            logAction?.Invoke($"Motherboard ID: {(string.IsNullOrEmpty(motherboardId) ? "(empty)" : motherboardId)}");
            if (!string.IsNullOrEmpty(motherboardId))
            {
                components.Append(motherboardId);
            }
            
            // Get GPU serial number
            var gpuId = GetGpuId();
            logAction?.Invoke($"GPU ID: {(string.IsNullOrEmpty(gpuId) ? "(empty)" : gpuId)}");
            if (!string.IsNullOrEmpty(gpuId))
            {
                components.Append(gpuId);
            }
            
            var componentsString = components.ToString();
            logAction?.Invoke($"Combined components string length: {componentsString.Length}");
            logAction?.Invoke($"Combined components (first 100 chars): {(componentsString.Length > 100 ? componentsString.Substring(0, 100) + "..." : componentsString)}");
            
            if (components.Length == 0)
            {
                logAction?.Invoke("No hardware components found, returning empty string");
                return string.Empty;
            }
            
            // Generate SHA256 hash of all components
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(componentsString));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            logAction?.Invoke($"Final SHA256 hash: {hashString}");
            logAction?.Invoke($"Hash length: {hashString.Length}");
            return hashString;
        }
        catch (Exception ex)
        {
            logAction?.Invoke($"Exception during generation: {ex.Message}");
            logAction?.Invoke($"Stack trace: {ex.StackTrace}");
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
