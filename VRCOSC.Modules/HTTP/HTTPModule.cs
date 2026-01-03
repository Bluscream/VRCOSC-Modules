// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Http;
using System.Text.Json;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using Bluscream;

namespace Bluscream.Modules;

[ModuleTitle("HTTP")]
[ModuleDescription("Send HTTP requests and receive responses for automation")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
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
        RegisterParameter<int>(HTTPParameter.RequestsCount, "VRCOSC/HTTP/RequestsCount", ParameterMode.Write, "Requests Count", "Total number of successful requests");

        CreateGroup("Settings", "HTTP module settings", HTTPSetting.LogRequests, HTTPSetting.Timeout);
    }

    protected override void OnPostLoad()
    {
        var urlReference = CreateVariable<string>(HTTPVariable.LastUrl, "Last URL")!;
        var statusReference = CreateVariable<int>(HTTPVariable.LastStatusCode, "Last Status Code")!;
        var bodyReference = CreateVariable<string>(HTTPVariable.LastResponse, "Last Response")!;
        CreateVariable<int>(HTTPVariable.RequestsCount, "Requests Count");

        CreateState(HTTPState.Idle, "Idle", "HTTP Ready");
        CreateState(HTTPState.Requesting, "Requesting", "Requesting: {0}", new[] { urlReference });
        CreateState(HTTPState.Success, "Success", "HTTP {1}\n{0}", new[] { urlReference, statusReference });
        CreateState(HTTPState.Failed, "Failed", "HTTP {1}\n{0}", new[] { urlReference, statusReference });

        CreateEvent(HTTPEvent.OnSuccess, "On Success", "Success: {0} ({1})", new[] { urlReference, statusReference });
        CreateEvent(HTTPEvent.OnFailed, "On Failed", "Failed: {0} ({1})", new[] { urlReference, statusReference });
    }

    private int _requestsCount = 0;

    protected override Task<bool> OnModuleStart()
    {
        _httpClient.Timeout = TimeSpan.FromMilliseconds(GetSettingValue<int>(HTTPSetting.Timeout));
        ChangeState(HTTPState.Idle);
        SetVariableValue(HTTPVariable.RequestsCount, 0);
        _requestsCount = 0;
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

            // Update state and variables
            ChangeState(HTTPState.Requesting);
            SetVariableValue(HTTPVariable.LastUrl, url);

            var request = new HttpRequestMessage(new HttpMethod(method), url);

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            if (!body.IsNullOrEmpty())
            {
                request.Content = new StringContent(body);
            }

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            var statusCode = (int)response.StatusCode;
            SendParameter(HTTPParameter.StatusCode, statusCode);
            SetVariableValue(HTTPVariable.LastStatusCode, statusCode);
            SetVariableValue(HTTPVariable.LastResponse, responseBody.Truncate(200));

            if (response.IsSuccessStatusCode)
            {
                _requestsCount++;
                SetVariableValue(HTTPVariable.RequestsCount, _requestsCount);
                SendParameter(HTTPParameter.RequestsCount, _requestsCount);
                ChangeState(HTTPState.Success);
                TriggerEvent(HTTPEvent.OnSuccess);
                await SendSuccessParameter();
            }
            else
            {
                ChangeState(HTTPState.Failed);
                TriggerEvent(HTTPEvent.OnFailed);
                await SendFailedParameter();
            }

            // Return to idle after delay
            TaskUtils.DelayedAction(1000, () => ChangeState(HTTPState.Idle));

            return new HttpResponse
            {
                Success = response.IsSuccessStatusCode,
                StatusCode = statusCode,
                Body = responseBody,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };
        }
        catch (Exception ex)
        {
            Log($"HTTP request failed: {ex.Message}");
            ChangeState(HTTPState.Failed);
            SetVariableValue(HTTPVariable.LastResponse, ex.Message);
            TriggerEvent(HTTPEvent.OnFailed);
            await SendFailedParameter();
            
            // Return to idle after delay
            TaskUtils.DelayedAction(1000, () => ChangeState(HTTPState.Idle));
            
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
        StatusCode,
        RequestsCount
    }

    public enum HTTPState
    {
        Idle,
        Requesting,
        Success,
        Failed
    }

    public enum HTTPVariable
    {
        LastUrl,
        LastStatusCode,
        LastResponse,
        RequestsCount
    }

    public enum HTTPEvent
    {
        OnSuccess,
        OnFailed
    }
}

public class HttpResponse
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Body { get; init; } = string.Empty;
    public Dictionary<string, string> Headers { get; init; } = new();
}