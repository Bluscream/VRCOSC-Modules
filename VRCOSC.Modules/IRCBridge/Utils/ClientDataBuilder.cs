// This is free and unencumbered software released into the public domain.
// For more information, please refer to <https://unlicense.org>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IrcDotNet;
using Bluscream;

namespace Bluscream.Modules.IRCBridge.Utils;

/// <summary>
/// Builds client data announcement CSV lines for IRC channel join messages
/// </summary>
public static class ClientDataBuilder
{
    /// <summary>
    /// Builds a CSV line containing client information for IRC announcement
    /// Format: <unix_timestamp_ms>;<executing_assembly_name>;<executing_assembly_version>;<irc_lib_name>;<irc_lib_version>;<module_name>;<module_version>;<external_ip_sha-256>;<pc_hash>;<userid_hash>;<username>
    /// </summary>
    public static string BuildClientDataCsv(
        string? externalIpHash,
        string? pcHash,
        string? userIdHash,
        string? username)
    {
        // Get executing assembly (VRCOSC app) name and version
        var executingAssemblyName = GetExecutingAssemblyName();
        var executingAssemblyVersion = GetExecutingAssemblyVersion();
        
        // Get IrcDotNet version and name
        var (ircLibName, ircLibVersion) = GetIrcDotNetInfo();
        
        // Get module version and name
        var moduleVersion = AssemblyUtils.GetVersion();
        var moduleName = AssemblyUtils.GetAssemblyName();
        
        // Get current Unix timestamp in milliseconds
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        // Build list of CSV values
        var csvValues = new List<string>
        {
            unixTimestamp.ToString(),
            executingAssemblyName,
            executingAssemblyVersion,
            ircLibName,
            ircLibVersion,
            moduleName,
            moduleVersion,
            externalIpHash ?? string.Empty,
            pcHash ?? string.Empty,
            userIdHash ?? string.Empty,
            username ?? string.Empty
        };
        
        // Sanitize each value by replacing semicolons with underscores to avoid breaking CSV parsers
        var sanitizedValues = csvValues.Select(v => v.Replace(";", "_")).ToArray();
        
        // Join with semicolons to create CSV line
        return string.Join(";", sanitizedValues);
    }
    
    private static string GetExecutingAssemblyName()
    {
        return AssemblyUtils.GetEntryAssemblyName();
    }
    
    private static string GetExecutingAssemblyVersion()
    {
        return AssemblyUtils.GetEntryAssemblyVersion();
    }
    
    private static (string name, string version) GetIrcDotNetInfo()
    {
        try
        {
            var ircLibAssembly = typeof(StandardIrcClient).Assembly;
            var ircLibAssemblyName = ircLibAssembly.GetName();
            var ircLibVersionObj = ircLibAssemblyName.Version;
            var ircLibVersion = ircLibVersionObj != null ? ircLibVersionObj.ToString(3) : string.Empty;
            var ircLibName = ircLibAssemblyName.Name ?? string.Empty;
            return (ircLibName, ircLibVersion);
        }
        catch
        {
            // Ignore errors
        }
        return (string.Empty, string.Empty);
    }
    
    /// <summary>
    /// Gets a formatted version string with app, library, and module versions
    /// Format: "AppName Version, LibraryName Version, ModuleName Version"
    /// </summary>
    public static string GetVersionString()
    {
        var parts = new List<string>();
        
        // Get executing assembly (VRCOSC app) name and version
        var executingAssemblyName = GetExecutingAssemblyName();
        var executingAssemblyVersion = GetExecutingAssemblyVersion();
        if (!string.IsNullOrEmpty(executingAssemblyName) && !string.IsNullOrEmpty(executingAssemblyVersion))
        {
            parts.Add($"{executingAssemblyName} {executingAssemblyVersion}");
        }
        
        // Get IrcDotNet version and name
        var (ircLibName, ircLibVersion) = GetIrcDotNetInfo();
        if (!string.IsNullOrEmpty(ircLibName) && !string.IsNullOrEmpty(ircLibVersion))
        {
            parts.Add($"{ircLibName} {ircLibVersion}");
        }
        
        // Get module version and name
        var moduleVersion = AssemblyUtils.GetVersion();
        var moduleName = AssemblyUtils.GetAssemblyName();
        if (!string.IsNullOrEmpty(moduleName) && !string.IsNullOrEmpty(moduleVersion))
        {
            parts.Add($"{moduleName} {moduleVersion}");
        }
        
        return string.Join(", ", parts);
    }
}
