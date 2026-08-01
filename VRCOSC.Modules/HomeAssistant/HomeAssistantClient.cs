// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HADotNet.Core;
using HADotNet.Core.Clients;
using HADotNet.Core.Models;

namespace Bluscream.Modules;

public class HomeAssistantClient
{
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private readonly Action<string> _logger;
    private readonly Action<string> _debugLogger;
    private int _messageIdCounter = 1;
    private StatesClient? _statesClient;
    private ServiceClient? _serviceClient;
    private TemplateClient? _templateClient;
    private HttpClient? _httpClient;

    public bool IsConnected { get; private set; }

    public event Action<bool>? OnConnectionStatusChanged;
    public event Action<string, string, JsonElement>? OnStateChanged;
    public event Action<int, string>? OnTemplateRendered;

    public HomeAssistantClient(Action<string> logger, Action<string>? debugLogger = null)
    {
        _logger = logger;
        _debugLogger = debugLogger ?? (_ => { });
    }

    public bool Initialize(string serverUrl, string accessToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(accessToken))
                return false;

            var uri = new Uri(serverUrl.TrimEnd('/'));
            ClientFactory.Initialize(uri, accessToken);

            _statesClient = ClientFactory.GetClient<StatesClient>();
            _serviceClient = ClientFactory.GetClient<ServiceClient>();
            _templateClient = ClientFactory.GetClient<TemplateClient>();

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            _httpClient.BaseAddress = uri;

            return true;
        }
        catch (Exception ex)
        {
            _logger($"Failed to initialize HomeAssistant client: {ex.Message}");
            return false;
        }
    }

    public async Task<IEnumerable<StateObject>?> GetStatesAsync()
    {
        try
        {
            if (_statesClient == null) return null;
            return await _statesClient.GetStates();
        }
        catch (Exception ex)
        {
            _logger($"Failed to fetch HomeAssistant states: {ex.Message}");
            return null;
        }
    }

    public async Task<StateObject?> GetStateAsync(string entityId)
    {
        try
        {
            if (_statesClient == null) return null;
            return await _statesClient.GetState(entityId);
        }
        catch (Exception ex)
        {
            _logger($"Failed to fetch state for {entityId}: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> RenderTemplateAsync(string template)
    {
        try
        {
            if (_templateClient == null) return null;
            return await _templateClient.RenderTemplate(template);
        }
        catch (Exception ex)
        {
            var snippet = template.Length > 60 ? template[..60] + "..." : template;
            _logger($"Failed to render template \"{snippet}\": {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CallServiceAsync(string domain, string service, string? entityId = null, object? serviceData = null)
    {
        try
        {
            if (_serviceClient == null) return false;
            
            object? payload = serviceData;
            if (!string.IsNullOrEmpty(entityId))
            {
                var dict = new Dictionary<string, object?> { ["entity_id"] = entityId };
                if (serviceData != null)
                {
                    var json = JsonSerializer.Serialize(serviceData);
                    var extraDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
                    if (extraDict != null)
                    {
                        foreach (var kvp in extraDict)
                            dict[kvp.Key] = kvp.Value;
                    }
                }
                payload = dict;
            }

            await _serviceClient.CallService(domain, service, payload);
            return true;
        }
        catch (Exception ex)
        {
            _logger($"Failed to call service {domain}.{service}: {ex.Message}");
            return false;
        }
    }

    public async Task StartWebSocketAsync(string serverUrl, string accessToken)
    {
        await StopWebSocket();

        _cts = new CancellationTokenSource();

        try
        {
            var wsUri = ConvertToWebSocketUri(serverUrl);
            _webSocket = new ClientWebSocket();
            _logger($"Connecting to Home Assistant WebSocket at {wsUri}...");

            await _webSocket.ConnectAsync(wsUri, _cts.Token);
            _ = ReceiveLoopAsync(accessToken, _cts.Token);
        }
        catch (Exception ex)
        {
            _logger($"Home Assistant WebSocket Connection Error: {ex.Message}");
            IsConnected = false;
            OnConnectionStatusChanged?.Invoke(false);
        }
    }

    public async Task StopWebSocket()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
            }
            catch { }
            finally
            {
                _webSocket.Dispose();
                _webSocket = null;
            }
        }

        if (IsConnected)
        {
            IsConnected = false;
            OnConnectionStatusChanged?.Invoke(false);
        }
    }

    private Uri ConvertToWebSocketUri(string httpUrl)
    {
        var uri = new Uri(httpUrl.TrimEnd('/'));
        var scheme = uri.Scheme == "https" ? "wss" : "ws";
        return new Uri($"{scheme}://{uri.Authority}/api/websocket");
    }

    private async Task ReceiveLoopAsync(string token, CancellationToken ct)
    {
        var buffer = new byte[8192];

        try
        {
            while (_webSocket != null && _webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ReceiveFullMessageAsync(buffer, ct);
                if (result == null) break;

                var jsonStr = Encoding.UTF8.GetString(result);
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("type", out var typeProp))
                {
                    var msgType = GetStringSafe(typeProp);

                    switch (msgType)
                    {
                        case "auth_required":
                            await SendAuthAsync(token, ct);
                            break;

                        case "auth_ok":
                            IsConnected = true;
                            _logger("Home Assistant WebSocket Auth Successful!");
                            OnConnectionStatusChanged?.Invoke(true);
                            await SubscribeStateChangesAsync(ct);
                            break;

                        case "auth_invalid":
                            _logger("Home Assistant WebSocket Auth Failed (Invalid Token).");
                            IsConnected = false;
                            OnConnectionStatusChanged?.Invoke(false);
                            return;

                        case "event":
                            HandleEventMessage(root);
                            break;

                        case "result":
                            HandleResultMessage(root);
                            break;
                    }
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger($"WebSocket receive loop error: {ex.Message}");
        }
        finally
        {
            IsConnected = false;
            OnConnectionStatusChanged?.Invoke(false);
        }
    }

    private async Task SendAuthAsync(string token, CancellationToken ct)
    {
        var authMsg = new
        {
            type = "auth",
            access_token = token
        };

        var json = JsonSerializer.Serialize(authMsg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private async Task SubscribeStateChangesAsync(CancellationToken ct)
    {
        int id = Interlocked.Increment(ref _messageIdCounter);
        var msg = new
        {
            id = id,
            type = "subscribe_events",
            event_type = "state_changed"
        };

        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        _debugLogger("Subscribed to state_changed events");
    }

    public async Task<int> SubscribeRenderTemplateAsync(string template)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open) return -1;

        int id = Interlocked.Increment(ref _messageIdCounter);
        var msg = new
        {
            id = id,
            type = "render_template",
            template = template
        };

        var json = JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        return id;
    }

    private void HandleEventMessage(JsonElement root)
    {
        if (root.TryGetProperty("event", out var eventObj))
        {
            // Check state_changed event
            if (eventObj.TryGetProperty("entity_id", out var entityIdProp) &&
                eventObj.TryGetProperty("new_state", out var newStateProp) &&
                newStateProp.ValueKind != JsonValueKind.Null)
            {
                var entityId = GetStringSafe(entityIdProp);
                var stateStr = newStateProp.TryGetProperty("state", out var s) ? GetStringSafe(s) : string.Empty;
                var attributes = newStateProp.TryGetProperty("attributes", out var attr) ? attr : default;

                OnStateChanged?.Invoke(entityId, stateStr, attributes);
            }
            // Check render_template event
            else if (root.TryGetProperty("id", out var idProp) &&
                     eventObj.TryGetProperty("result", out var resultProp))
            {
                int id = idProp.GetInt32();
                string rendered = GetStringSafe(resultProp);
                OnTemplateRendered?.Invoke(id, rendered);
            }
        }
    }

    private void HandleResultMessage(JsonElement root)
    {
        if (root.TryGetProperty("id", out var idProp) &&
            root.TryGetProperty("result", out var resultProp))
        {
            int id = idProp.GetInt32();
            if (resultProp.ValueKind == JsonValueKind.Object && resultProp.TryGetProperty("result", out var templateResult))
            {
                string rendered = GetStringSafe(templateResult);
                OnTemplateRendered?.Invoke(id, rendered);
            }
            else if (resultProp.ValueKind != JsonValueKind.Object)
            {
                string rendered = GetStringSafe(resultProp);
                OnTemplateRendered?.Invoke(id, rendered);
            }
        }
    }

    private static string GetStringSafe(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    private async Task<byte[]?> ReceiveFullMessageAsync(byte[] buffer, CancellationToken ct)
    {
        using var ms = new System.IO.MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await _webSocket!.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return ms.ToArray();
    }
}
