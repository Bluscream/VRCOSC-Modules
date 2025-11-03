// Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VRCOSC.App.SDK.Modules;
using VRCOSC.App.SDK.Parameters;
using Bluscream;
using VRCOSCModule = VRCOSC.App.SDK.Modules.Module;

namespace Bluscream.Modules;

[ModuleTitle("HTTP Server")]
[ModuleDescription("HTTP server to control OSC via HTTP requests")]
[ModuleType(ModuleType.Integrations)]
[ModuleInfo("https://github.com/Bluscream/VRCOSC-Modules")]
public class HTTPServerModule : VRCOSCModule
{
    private HttpListener? _httpListener;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning = false;
    private int _requestCount = 0;
    private DateTime _serverStartTime = DateTime.UtcNow;
    private string _serverUrl = string.Empty;
    private string _authToken = string.Empty;
    private Dictionary<string, JsonElement>? _openApiSpec;

    // Enums for settings/parameters/etc
    private enum HTTPServerSetting
    {
        Port,
        AllowExternalConnections,
        RequireAuthentication,
        AuthenticationToken,
        EnableCORS,
        CORSOrigins,
        LogRequests,
        AutoStart
    }

    private enum HTTPServerParameter
    {
        ServerRunning,
        RequestReceived,
        RequestCount,
        LastStatusCode
    }

    private enum HTTPServerVariable
    {
        ServerStatus,
        LastRequest,
        LastResponse,
        RequestCount,
        ServerUrl
    }

    private enum HTTPServerState
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }

    private enum HTTPServerEvent
    {
        OnServerStarted,
        OnServerStopped,
        OnRequestReceived,
        OnRequestProcessed,
        OnError
    }

    protected override void OnPreLoad()
    {
        // Server configuration
        CreateTextBox(HTTPServerSetting.Port, "Port", "HTTP server port (1024-65535)", "8080");
        CreateToggle(HTTPServerSetting.AllowExternalConnections, "Allow External Connections", "Allow connections from other devices on network", false);
        CreateToggle(HTTPServerSetting.RequireAuthentication, "Require Authentication", "Require bearer token authentication", false);
        CreateTextBox(HTTPServerSetting.AuthenticationToken, "Authentication Token", "Bearer token for authentication (leave empty to generate)", string.Empty);
        
        // CORS settings
        CreateToggle(HTTPServerSetting.EnableCORS, "Enable CORS", "Enable Cross-Origin Resource Sharing", true);
        CreateTextBox(HTTPServerSetting.CORSOrigins, "CORS Origins", "Allowed CORS origins (comma-separated, * for all)", "*");
        
        // Behavior settings
        CreateToggle(HTTPServerSetting.LogRequests, "Log Requests", "Log all HTTP requests to console", true);
        CreateToggle(HTTPServerSetting.AutoStart, "Auto Start", "Start server automatically when module loads", true);

        // OSC Parameters
        RegisterParameter<bool>(HTTPServerParameter.ServerRunning, "VRCOSC/HTTPServer/Running", ParameterMode.Write, "Running", "True when server is running");
        RegisterParameter<bool>(HTTPServerParameter.RequestReceived, "VRCOSC/HTTPServer/RequestReceived", ParameterMode.Write, "Request", "True for 1 second when request is received");
        RegisterParameter<int>(HTTPServerParameter.RequestCount, "VRCOSC/HTTPServer/RequestCount", ParameterMode.Write, "Count", "Total number of requests processed");
        RegisterParameter<int>(HTTPServerParameter.LastStatusCode, "VRCOSC/HTTPServer/StatusCode", ParameterMode.Write, "Status", "Last response status code");

        // Groups
        CreateGroup("Server", "HTTP server settings", HTTPServerSetting.Port, HTTPServerSetting.AllowExternalConnections, HTTPServerSetting.AutoStart);
        CreateGroup("Security", "Authentication and CORS", HTTPServerSetting.RequireAuthentication, HTTPServerSetting.AuthenticationToken, HTTPServerSetting.EnableCORS, HTTPServerSetting.CORSOrigins);
        CreateGroup("Debug", "Debug settings", HTTPServerSetting.LogRequests);

        // Load OpenAPI spec from embedded resource
        LoadOpenApiSpec();
    }

    protected override void OnPostLoad()
    {
        // Variables
        var statusRef = CreateVariable<string>(HTTPServerVariable.ServerStatus, "Server Status");
        var urlRef = CreateVariable<string>(HTTPServerVariable.ServerUrl, "Server URL");
        CreateVariable<string>(HTTPServerVariable.LastRequest, "Last Request");
        CreateVariable<string>(HTTPServerVariable.LastResponse, "Last Response");
        CreateVariable<int>(HTTPServerVariable.RequestCount, "Request Count");

        // States
        CreateState(HTTPServerState.Stopped, "Stopped", "HTTP Server: Stopped");
        CreateState(HTTPServerState.Starting, "Starting", "HTTP Server: Starting...");
        CreateState(HTTPServerState.Running, "Running", "HTTP Server: Running\n{0}", urlRef != null ? new[] { urlRef } : null);
        CreateState(HTTPServerState.Stopping, "Stopping", "HTTP Server: Stopping...");
        CreateState(HTTPServerState.Error, "Error", "HTTP Server: Error\n{0}", statusRef != null ? new[] { statusRef } : null);

        // Events
        CreateEvent(HTTPServerEvent.OnServerStarted, "On Server Started");
        CreateEvent(HTTPServerEvent.OnServerStopped, "On Server Stopped");
        CreateEvent(HTTPServerEvent.OnRequestReceived, "On Request Received");
        CreateEvent(HTTPServerEvent.OnRequestProcessed, "On Request Processed");
        CreateEvent(HTTPServerEvent.OnError, "On Error");
    }

    protected override async Task<bool> OnModuleStart()
    {
        SetVariableValue(HTTPServerVariable.RequestCount, 0);
        _requestCount = 0;
        
        if (GetSettingValue<bool>(HTTPServerSetting.AutoStart))
        {
            return await StartServer();
        }
        
        ChangeState(HTTPServerState.Stopped);
        return true;
    }

    protected override Task OnModuleStop()
    {
        StopServer();
        return Task.CompletedTask;
    }

    private void LoadOpenApiSpec()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Bluscream.Modules.HTTPServer.openapi.json";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                Log("Warning: OpenAPI spec not found in embedded resources");
                return;
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            
            var doc = JsonDocument.Parse(json);
            _openApiSpec = new Dictionary<string, JsonElement>();
            
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                _openApiSpec[prop.Name] = prop.Value.Clone();
            }

            Log("Loaded OpenAPI spec from embedded resources");
        }
        catch (Exception ex)
        {
            Log($"Failed to load OpenAPI spec: {ex.Message}");
        }
    }

    public async Task<bool> StartServer()
    {
        if (_isRunning)
        {
            Log("Server is already running");
            return false;
        }

        try
        {
            ChangeState(HTTPServerState.Starting);
            
            var portStr = GetSettingValue<string>(HTTPServerSetting.Port);
            if (!int.TryParse(portStr, out var port) || port < 1024 || port > 65535)
            {
                Log($"Invalid port number: {portStr}. Must be between 1024-65535.");
                SetVariableValue(HTTPServerVariable.ServerStatus, "Error: Invalid port");
                ChangeState(HTTPServerState.Error);
                return false;
            }
            
            var allowExternal = GetSettingValue<bool>(HTTPServerSetting.AllowExternalConnections);
            _authToken = GetSettingValue<string>(HTTPServerSetting.AuthenticationToken);
            
            _httpListener = new HttpListener();
            
            // Add prefixes
            // Note: Using + instead of localhost/127.0.0.1 because it works with the netsh URL reservation
            // http://+:PORT/ covers localhost, 127.0.0.1, and machine name
            if (allowExternal)
            {
                _httpListener.Prefixes.Add($"http://+:{port}/");
                _serverUrl = $"http://localhost:{port}"; // Display localhost for user friendliness
            }
            else
            {
                _httpListener.Prefixes.Add($"http://+:{port}/");
                _serverUrl = $"http://localhost:{port}"; // Display localhost for user friendliness
            }

            _httpListener.Start();
            _isRunning = true;
            _serverStartTime = DateTime.UtcNow;

            SetVariableValue(HTTPServerVariable.ServerUrl, _serverUrl);
            SetVariableValue(HTTPServerVariable.ServerStatus, "Running");
            
            this.SendParameterSafe(HTTPServerParameter.ServerRunning, true);
            ChangeState(HTTPServerState.Running);
            TriggerEvent(HTTPServerEvent.OnServerStarted);
            
            Log($"HTTP Server started on {_serverUrl}");
            Log($"API documentation at {_serverUrl}/docs");

            // Start listening for requests
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => ListenForRequests(_cancellationTokenSource.Token));

            return true;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5) // Access Denied
        {
            Log($"Access denied: HttpListener requires administrator privileges or URL reservation.");
            Log($"Run this as admin or run: netsh http add urlacl url=http://+:{GetSettingValue<string>(HTTPServerSetting.Port)}/ user=Everyone");
            SetVariableValue(HTTPServerVariable.ServerStatus, "Error: Access Denied");
            ChangeState(HTTPServerState.Error);
            TriggerEvent(HTTPServerEvent.OnError);
            _isRunning = false;
            return false;
        }
        catch (Exception ex)
        {
            Log($"Failed to start HTTP server: {ex.Message}");
            SetVariableValue(HTTPServerVariable.ServerStatus, $"Error: {ex.Message}");
            ChangeState(HTTPServerState.Error);
            TriggerEvent(HTTPServerEvent.OnError);
            _isRunning = false;
            return false;
        }
    }

    private async Task ListenForRequests(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), cancellationToken);
            }
            catch (HttpListenerException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                Log($"Error in request listener: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;
            
            // Log request
            if (GetSettingValue<bool>(HTTPServerSetting.LogRequests))
            {
                Log($"{request.HttpMethod} {request.Url?.PathAndQuery}");
            }

            // CORS headers
            if (GetSettingValue<bool>(HTTPServerSetting.EnableCORS))
            {
                var origins = GetSettingValue<string>(HTTPServerSetting.CORSOrigins);
                response.AddHeader("Access-Control-Allow-Origin", origins);
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
                
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }
            }

            // Check authentication
            if (!CheckAuthentication(request, response))
            {
                return;
            }

            // Route the request
            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            await RouteRequest(context, method, path);
        }
        catch (Exception ex)
        {
            Log($"Error handling request: {ex.Message}");
            try
            {
                SendJsonResponse(context.Response, 500, new { error = "Internal server error", message = ex.Message });
            }
            catch
            {
                // Ignore errors when sending error response
            }
        }
    }

    private bool CheckAuthentication(HttpListenerRequest request, HttpListenerResponse response)
    {
        var requiresAuth = GetSettingValue<bool>(HTTPServerSetting.RequireAuthentication) && !string.IsNullOrEmpty(_authToken);
        var path = request.Url?.AbsolutePath ?? "/";
        
        // Skip auth for docs
        if (path.StartsWith("/docs") || path.StartsWith("/swagger") || path == "/openapi.json")
        {
            return true;
        }

        if (requiresAuth)
        {
            var authHeader = request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ") || authHeader.Substring(7) != _authToken)
            {
                SendJsonResponse(response, 401, new { error = "Unauthorized", message = "Valid bearer token required" });
                return false;
            }
        }

        return true;
    }

    private async Task RouteRequest(HttpListenerContext context, string method, string path)
    {
        var response = context.Response;

        // Track request
        _requestCount++;
        SetVariableValue(HTTPServerVariable.RequestCount, _requestCount);
        SetVariableValue(HTTPServerVariable.LastRequest, $"{method} {path}");
        SendParameter(HTTPServerParameter.RequestCount, _requestCount);
        SendParameter(HTTPServerParameter.RequestReceived, true);
        _ = Task.Delay(1000).ContinueWith(_ => SendParameter(HTTPServerParameter.RequestReceived, false));
        TriggerEvent(HTTPServerEvent.OnRequestReceived);

        // Route based on path and method
        switch (path.ToLowerInvariant())
        {
            case "/":
                if (method == "GET")
                    await HandleRoot(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            case "/openapi.json":
                if (method == "GET")
                    await HandleOpenApiSpec(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            case "/docs":
            case "/swagger":
                if (method == "GET")
                    await HandleSwaggerUI(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            case "/server/status":
            case "/status":
                if (method == "GET")
                    await HandleServerStatus(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            case "/osc/parameters":
                if (method == "GET")
                    await HandleGetAllParameters(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            case "/chatbox/send":
            case "/chatbox":
                if (method == "POST")
                    await HandleSendChatBox(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            case "/avatars/current":
            case "/avatar":
                if (method == "GET")
                    await HandleGetCurrentAvatar(context);
                else
                    SendJsonResponse(response, 405, new { error = "Method not allowed" });
                break;

            default:
                // Check if it's a parameter-specific endpoint
                if (path.StartsWith("/osc/parameters/"))
                {
                    var paramName = path.Substring("/osc/parameters/".Length);
                    if (method == "GET")
                        await HandleGetParameter(context, paramName);
                    else if (method == "POST" || method == "PUT")
                        await HandleSetParameter(context, paramName);
                    else
                        SendJsonResponse(response, 405, new { error = "Method not allowed" });
                }
                else
                {
                    SendJsonResponse(response, 404, new { error = "Endpoint not found", path });
                }
                break;
        }

        TriggerEvent(HTTPServerEvent.OnRequestProcessed);
    }

    #region Request Handlers

    private async Task HandleRoot(HttpListenerContext context)
    {
        var responseObj = new
        {
            message = "VRCOSC HTTP Server is running",
            version = "2025.1103.1",
            documentation = $"{_serverUrl}/docs",
            endpoints = GetEndpointsFromOpenApi(),
            timestamp = DateTime.UtcNow.ToString("o"),
            requestCount = _requestCount
        };

        SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }

    private async Task HandleOpenApiSpec(HttpListenerContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Bluscream.Modules.HTTPServer.openapi.json";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            SendJsonResponse(context.Response, 404, new { error = "OpenAPI spec not found" });
            return;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        // Update server URL dynamically
        content = content.Replace("http://localhost:8080", _serverUrl);
        
        context.Response.ContentType = "application/json";
        var buffer = Encoding.UTF8.GetBytes(content);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    private async Task HandleSwaggerUI(HttpListenerContext context)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "Bluscream.Modules.HTTPServer.swagger-ui.html";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            SendJsonResponse(context.Response, 404, new { error = "Swagger UI not found" });
            return;
        }

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        
        context.Response.ContentType = "text/html";
        var buffer = Encoding.UTF8.GetBytes(content);
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        context.Response.Close();
    }

    private async Task HandleServerStatus(HttpListenerContext context)
    {
        var responseObj = new
        {
            success = true,
            status = "running",
            version = "2025.1103.1",
            uptime = GetUptime(),
            requestCount = _requestCount,
            serverUrl = _serverUrl,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }

    private async Task HandleGetAllParameters(HttpListenerContext context)
    {
        var responseObj = new
        {
            success = true,
            message = "OSC parameter listing not yet implemented",
            parameters = new object[]
            {
                new { name = "VRCOSC/HTTPServer/Running", type = "bool", value = true },
                new { name = "VRCOSC/HTTPServer/RequestCount", type = "int", value = _requestCount }
            }
        };

        SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }

    private async Task HandleGetParameter(HttpListenerContext context, string name)
    {
        var responseObj = new
        {
            success = true,
            parameter = name,
            value = (object?)null,
            type = "unknown",
            message = "OSC parameter reading not yet implemented"
        };

        SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }

    private async Task HandleSetParameter(HttpListenerContext context, string name)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body);

            if (!data.RootElement.TryGetProperty("value", out var valueElement))
            {
                SendJsonResponse(context.Response, 400, new { error = "Missing 'value' field in request body" });
                return;
            }

            var responseObj = new
            {
                success = true,
                parameter = name,
                value = valueElement.ToString(),
                message = "OSC parameter writing not yet implemented"
            };

            SendJsonResponse(context.Response, 200, responseObj);
        }
        catch (Exception ex)
        {
            SendJsonResponse(context.Response, 400, new { error = "Invalid request body", message = ex.Message });
        }
    }

    private async Task HandleGetCurrentAvatar(HttpListenerContext context)
    {
        var responseObj = new
        {
            success = true,
            message = "Avatar info not yet implemented",
            avatar = new
            {
                id = "avtr_00000000-0000-0000-0000-000000000000",
                name = "Unknown",
                loaded = false
            }
        };

        SendJsonResponse(context.Response, 200, responseObj);
        await Task.CompletedTask;
    }

    private async Task HandleSendChatBox(HttpListenerContext context)
    {
        try
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync();
            var data = JsonDocument.Parse(body);

            var message = data.RootElement.TryGetProperty("message", out var msgElement) ? msgElement.GetString() : "";
            var send = data.RootElement.TryGetProperty("send", out var sendElement) && sendElement.GetBoolean();
            var minimalBackground = data.RootElement.TryGetProperty("minimalBackground", out var bgElement) && bgElement.GetBoolean();

            // Use utilities to send chatbox message
            var success = ReflectionUtils.SendChatBox(message ?? "", minimalBackground);

            var responseObj = new
            {
                success = success,
                message = message,
                sent = send,
                implemented = true
            };

            SendJsonResponse(context.Response, 200, responseObj);
        }
        catch (Exception ex)
        {
            SendJsonResponse(context.Response, 400, new { error = "Invalid request body", message = ex.Message });
        }
    }

    #endregion

    #region Helper Methods

    private List<string> GetEndpointsFromOpenApi()
    {
        var endpoints = new List<string>();

        try
        {
            if (_openApiSpec != null && _openApiSpec.TryGetValue("paths", out var paths))
            {
                foreach (var pathEntry in paths.EnumerateObject())
                {
                    var path = pathEntry.Name;
                    foreach (var methodEntry in pathEntry.Value.EnumerateObject())
                    {
                        var method = methodEntry.Name.ToUpperInvariant();
                        if (method == "GET" || method == "POST" || method == "PUT" || method == "DELETE")
                        {
                            var summary = methodEntry.Value.TryGetProperty("summary", out var summaryElement)
                                ? summaryElement.GetString()
                                : "";
                            endpoints.Add($"{method} {path} - {summary}");
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback to hardcoded list
            endpoints = new List<string>
            {
                "GET /osc/parameters - List all OSC parameters",
                "GET /osc/parameters/{name} - Get specific parameter value",
                "POST /osc/parameters/{name} - Set parameter value",
                "GET /avatars/current - Get current avatar info",
                "POST /chatbox/send - Send chatbox message",
                "GET /server/status - Server status",
                "GET /docs - API documentation"
            };
        }

        return endpoints;
    }

    private void SendJsonResponse(HttpListenerResponse response, int statusCode, object data)
    {
        try
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var buffer = Encoding.UTF8.GetBytes(json);
            
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();

            SendParameter(HTTPServerParameter.LastStatusCode, statusCode);
        }
        catch (Exception ex)
        {
            Log($"Error sending response: {ex.Message}");
        }
    }

    #endregion

    public void StopServer()
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            ChangeState(HTTPServerState.Stopping);
            
            _cancellationTokenSource?.Cancel();
            _httpListener?.Stop();
            _httpListener?.Close();
            
            _isRunning = false;
            
            SetVariableValue(HTTPServerVariable.ServerStatus, "Stopped");
            this.SendParameterSafe(HTTPServerParameter.ServerRunning, false);
            ChangeState(HTTPServerState.Stopped);
            TriggerEvent(HTTPServerEvent.OnServerStopped);
            
            Log("HTTP Server stopped");
        }
        catch (Exception ex)
        {
            Log($"Error stopping HTTP server: {ex.Message}");
        }
    }

    public bool IsRunning => _isRunning;
    public string GetServerUrl() => _serverUrl;
    public int GetRequestCount() => _requestCount;
    
    public string GetUptime()
    {
        if (!_isRunning) return "Not running";
        var uptime = DateTime.UtcNow - _serverStartTime;
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
    }
}
