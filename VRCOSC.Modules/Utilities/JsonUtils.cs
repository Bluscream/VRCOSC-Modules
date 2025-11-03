// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;

namespace Bluscream;

/// <summary>
/// JSON utility functions
/// </summary>
public static class JsonUtils
{
    public static string ToJson(object obj, bool indented = false)
    {
        var options = new JsonSerializerOptions { WriteIndented = indented };
        return JsonSerializer.Serialize(obj, options);
    }
    
    public static T? FromJson<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
    
    public static bool TryGetJsonProperty<T>(JsonElement element, string propertyName, out T? value)
    {
        value = default;
        if (element.TryGetProperty(propertyName, out var prop))
        {
            try
            {
                value = JsonSerializer.Deserialize<T>(prop.GetRawText());
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }
}
