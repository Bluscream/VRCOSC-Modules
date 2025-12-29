// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Bluscream.Modules;

public static class HardwareIdGenerator
{
    public static string GenerateHardwareId()
    {
        try
        {
            var components = new StringBuilder();
            
            // Get CPU serial number
            var cpuId = GetCpuId();
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] CPU ID: {(string.IsNullOrEmpty(cpuId) ? "(empty)" : cpuId)}");
            if (!string.IsNullOrEmpty(cpuId))
            {
                components.Append(cpuId);
            }
            
            // Get motherboard serial number
            var motherboardId = GetMotherboardId();
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Motherboard ID: {(string.IsNullOrEmpty(motherboardId) ? "(empty)" : motherboardId)}");
            if (!string.IsNullOrEmpty(motherboardId))
            {
                components.Append(motherboardId);
            }
            
            // Get GPU serial number
            var gpuId = GetGpuId();
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] GPU ID: {(string.IsNullOrEmpty(gpuId) ? "(empty)" : gpuId)}");
            if (!string.IsNullOrEmpty(gpuId))
            {
                components.Append(gpuId);
            }
            
            var componentsString = components.ToString();
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Combined components string length: {componentsString.Length}");
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Combined components (first 100 chars): {(componentsString.Length > 100 ? componentsString.Substring(0, 100) + "..." : componentsString)}");
            
            if (components.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[HardwareIdGenerator] No hardware components found, returning empty string");
                return string.Empty;
            }
            
            // Generate SHA256 hash of all components
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(componentsString));
            var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Final SHA256 hash: {hashString}");
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Hash length: {hashString.Length}");
            return hashString;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Exception during generation: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[HardwareIdGenerator] Stack trace: {ex.StackTrace}");
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
