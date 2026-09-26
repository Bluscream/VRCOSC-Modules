// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// Free Google Translate web endpoint (the one the browser extension uses; no key). The answer
// is a nested array whose [0][i][0] elements are the translated segments.

using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Bluscream.Modules.TranslationPatches;

internal sealed class Translator
{
    private const string EndpointUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={0}&dt=t&q={1}";
    private const int MaxCacheEntries = 500;
    private static readonly TimeSpan FailureLogInterval = TimeSpan.FromSeconds(30);

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly ConcurrentDictionary<(string Text, string Language), string> _cache = new();
    private readonly Action<string> _log;
    private DateTime _lastFailureLog = DateTime.MinValue;

    public Translator(Action<string> log)
    {
        _log = log;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 VRCOSC-BluscreamModules/1.0 (https://github.com/Bluscream/VRCOSC-Modules)");
        return client;
    }

    /// <summary>Translated text, or empty when the request failed. Never throws.</summary>
    public async Task<string> TranslateAsync(string text, string language)
    {
        text = text.Trim();
        language = language.Trim().ToLowerInvariant();
        if (text.Length == 0 || language.Length == 0) return string.Empty;

        var key = (text, language);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        try
        {
            var url = string.Format(EndpointUrl, Uri.EscapeDataString(language), Uri.EscapeDataString(text));
            using var response = await Http.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogFailure($"Translation request returned {(int)response.StatusCode}.");
                return string.Empty;
            }

            var translated = Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            if (translated.Length == 0)
            {
                LogFailure("Translation response contained no segments.");
                return string.Empty;
            }

            if (_cache.Count >= MaxCacheEntries) _cache.Clear();
            _cache[key] = translated;
            return translated;
        }
        catch (HttpRequestException ex) { LogFailure($"Translation request failed: {ex.Message}"); }
        catch (TaskCanceledException) { LogFailure("Translation request timed out."); }
        catch (JsonException ex) { LogFailure($"Translation response was not valid JSON: {ex.Message}"); }

        return string.Empty;
    }

    private static string Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return string.Empty;

        var segments = root[0];
        if (segments.ValueKind != JsonValueKind.Array) return string.Empty;

        var sb = new StringBuilder();
        foreach (var segment in segments.EnumerateArray())
        {
            if (segment.ValueKind == JsonValueKind.Array && segment.GetArrayLength() > 0 && segment[0].ValueKind == JsonValueKind.String)
                sb.Append(segment[0].GetString());
        }

        return sb.ToString().Trim();
    }

    private void LogFailure(string message)
    {
        if (DateTime.UtcNow - _lastFailureLog < FailureLogInterval) return;
        _lastFailureLog = DateTime.UtcNow;
        _log(message);
    }
}
