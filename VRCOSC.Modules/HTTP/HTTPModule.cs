// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Http;
using System.Text.Json;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;

namespace Bluscream.Modules.HTTP;

[ModuleTitle("HTTP")]
[ModuleDescription("Send HTTP requests and receive responses for automation")]
[ModuleType(ModuleType.Integrations)]
public class HTTPModule : Module
{
    private readonly HttpClient _httpClient = new();

    protected override void OnPreLoad()
    {
        CreateToggle(HTTPSetting.LogRequests, "Log Requests", "Log all HTTP requests to console", false);
        CreateTextBox(HTTPSetting.Timeout, "Timeout (ms)", "HTTP request timeout", 30000);

        RegisterParameter<bool>(HTTPParameter.Success, "VRCOSC/HTTP/Success", ParameterMode.Write, "Success", "True for 1 second when request succeeds");
        RegisterParameter<bool>(HTTPParameter.Failed, "VRCOSC/HTTP/Failed", ParameterMode.Write, "Failed", "True for 1 second when request fails");
        RegisterParameter<int>(HTTPParameter.StatusCode, "VRCOSC/HTTP/StatusCode", ParameterMode.Write, "Status Code", "Last HTTP status code");

        CreateGroup("Settings", HTTPSetting.LogRequests, HTTPSetting.Timeout);
    }

    protected override Task<bool> OnModuleStart()
    {
        _httpClient.Timeout = TimeSpan.FromMilliseconds(GetSettingValue<int>(HTTPSetting.Timeout));
        return Task.FromResult(true);
    }

    public async Task<HttpResponse> SendRequest(string method, string url, string? body = null, Dictionary<string, string>? headers = null)
    {
        try
        {
            if (GetSettingValue<bool>(HTTPSetting.LogRequests))
            {
                Log($"HTTP {method} {url}");
            }

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body);
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            SendParameter(HTTPParameter.StatusCode, (int)response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                await SendSuccessParameter();
            }
            else
            {
                await SendFailedParameter();
            }

            return new HttpResponse
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Body = responseBody,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };
        }
        catch (Exception ex)
        {
            Log($"HTTP request failed: {ex.Message}");
            await SendFailedParameter();
            
            return new HttpResponse
            {
                Success = false,
                StatusCode = 0,
                Body = ex.Message,
                Headers = new Dictionary<string, string>()
            };
        }
    }

    private async Task SendSuccessParameter()
    {
        var wasAcknowledged = await SendParameterAndWait(HTTPParameter.Success, true);
        if (wasAcknowledged)
            SendParameter(HTTPParameter.Success, false);
    }

    private async Task SendFailedParameter()
    {
        var wasAcknowledged = await SendParameterAndWait(HTTPParameter.Failed, true);
        if (wasAcknowledged)
            SendParameter(HTTPParameter.Failed, false);
    }

    public enum HTTPSetting
    {
        LogRequests,
        Timeout
    }

    public enum HTTPParameter
    {
        Success,
        Failed,
        StatusCode
    }
}

public class HttpResponse
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Body { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
}