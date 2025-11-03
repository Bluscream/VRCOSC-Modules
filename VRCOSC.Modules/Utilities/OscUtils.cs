// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

namespace Bluscream;

/// <summary>
/// OSC utility functions
/// </summary>
public static class OscUtils
{
    public static string NormalizeOscAddress(string address)
        => address.StartsWith("/") ? address : "/" + address;
    
    public static bool IsValidOscAddress(string address)
        => !string.IsNullOrEmpty(address) && address.StartsWith("/");
    
    public static string GetOscParameterName(string address)
    {
        var parts = address.Split('/');
        return parts.Length > 0 ? parts[^1] : address;
    }
}
