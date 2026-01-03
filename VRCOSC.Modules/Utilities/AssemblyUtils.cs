using System;
using System.Reflection;

namespace Bluscream;

/// <summary>
/// Utility methods for assembly information
/// </summary>
public static class AssemblyUtils
{
    private static string? _cachedVersion;
    private static string? _cachedAssemblyName;

    /// <summary>
    /// Get the current assembly version (cached)
    /// Returns version in SemVer format (YYYY.MDD.Build)
    /// </summary>
    public static string GetVersion()
    {
        if (_cachedVersion != null) return _cachedVersion;

        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            _cachedVersion = version?.ToString(3) ?? "unknown";
            return _cachedVersion;
        }
        catch
        {
            _cachedVersion = "unknown";
            return _cachedVersion;
        }
    }

    /// <summary>
    /// Get the current assembly name (cached)
    /// </summary>
    public static string GetAssemblyName()
    {
        if (_cachedAssemblyName != null) return _cachedAssemblyName;

        try
        {
            _cachedAssemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "unknown";
            return _cachedAssemblyName;
        }
        catch
        {
            _cachedAssemblyName = "unknown";
            return _cachedAssemblyName;
        }
    }

    /// <summary>
    /// Clear cached assembly information (useful after hot-reload or updates)
    /// </summary>
    public static void ClearCache()
    {
        _cachedVersion = null;
        _cachedAssemblyName = null;
    }
    
    /// <summary>
    /// Get the entry assembly name (the application, not the module)
    /// </summary>
    public static string GetEntryAssemblyName()
    {
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var assemblyNameObj = entryAssembly.GetName();
            return assemblyNameObj.Name ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Get the entry assembly version (the application, not the module)
    /// </summary>
    public static string GetEntryAssemblyVersion()
    {
        try
        {
            var entryAssembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var versionObj = entryAssembly.GetName().Version;
            return versionObj?.ToString(3) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
