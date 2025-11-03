// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;

namespace Bluscream;

/// <summary>
/// HTTP utility functions
/// </summary>
public static class HttpUtils
{
    public static string AddQueryParameter(string url, string key, string value)
    {
        var separator = url.Contains("?") ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
    
    public static bool IsValidUrl(string? str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        return Uri.TryCreate(str, UriKind.Absolute, out var uri) 
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
