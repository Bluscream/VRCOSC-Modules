# HTTP / MCP Server Module

Embedded REST API & Model Context Protocol (MCP) server allowing external web applications, local scripts, or AI Agents to query and control VRCOSC.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- Open port 8080 (or custom configured port).
- OpenAPI / Swagger UI available at `http://localhost:8080/docs`.
- MCP Endpoint available at `http://localhost:8080/mcp` for AI agent tools.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Port** | `TextBox` | TCP port for HTTP/MCP server | `8080` |
| **EnableRestApi** | `Toggle` | Enable REST API endpoints (/api/v1/...) | `true` |
| **EnableMcpServer** | `Toggle` | Enable Model Context Protocol (MCP) server (/mcp) | `true` |
| **EnableSwaggerUi** | `Toggle` | Enable Swagger UI documentation (/docs) | `true` |
| **AuthToken** | `TextBox` | Optional bearer authentication token (empty = no auth) | `empty` |
| **CorsAllowedOrigins** | `TextBox` | CORS allowed origins (comma-separated or '*' for all) | `*` |
| **LogRequests** | `Toggle` | Log HTTP request details to console | `false` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Port** | `port` | `int` | Active server TCP port |
| **Connected Clients** | `connectedclients` | `int` | Active client connections |
| **Total Requests** | `totalrequests` | `int` | Total HTTP/MCP requests processed |
| **Server Status** | `serverstatus` | `string` | Current server state (Running, Stopped, Error) |
| **Swagger URL** | `swaggerurl` | `string` | Local URL to Swagger UI documentation |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Stopped** | `stopped` | `HTTP Server Stopped` | Server offline |
| **Starting** | `starting` | `HTTP Server Starting...` | Server initializing |
| **Running** | `running` | `HTTP Server on port {0}` | Server active and listening |
| **Error** | `error` | `HTTP Server Error: {0}` | Server error state |
| **Stopping** | `stopping` | `HTTP Server Stopping...` | Server shutting down |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Server Started** | `onserverstarted` | `Server started on port {0}` | Triggered when server starts listening |
| **On Server Stopped** | `onserverstopped` | `Server stopped` | Triggered when server stops |
| **On Request Received** | `onrequestreceived` | `Request: {0} {1}` | Triggered on incoming HTTP request |
| **On MCP Tool Executed** | `onmcptoolexecuted` | `MCP Tool: {0}` | Triggered when an AI Agent invokes an MCP tool |
| **On Error** | `onerror` | `Server Error: {0}` | Triggered on server exception |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/HTTPServer/Running` | `bool` | `Write` | True if HTTP server is running |
| `VRCOSC/HTTPServer/Port` | `int` | `Write` | Active HTTP server port |
| `VRCOSC/HTTPServer/Requests` | `int` | `Write` | Total processed request count |
| `VRCOSC/HTTPServer/Error` | `bool` | `Write` | True if server is in error state |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get HTTP Server Status** | Flow trigger | Is Running (bool), Port (int), Requests (int) | Returns HTTP server state |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Port** | `TextBox` | `TCP port for HTTP/MCP server` | `8080` |
| **EnableRestApi** | `Toggle` | `Enable REST API endpoints (/api/v1/...)` | `true` |
| **EnableMcpServer** | `Toggle` | `Enable Model Context Protocol (MCP) server (/mcp)` | `true` |
| **EnableSwaggerUi** | `Toggle` | `Enable Swagger UI documentation (/docs)` | `true` |
| **AuthToken** | `TextBox` | `Optional bearer authentication token (empty = no auth)` | `empty` |
| **CorsAllowedOrigins** | `TextBox` | `CORS allowed origins (comma-separated or '*' for all)` | `*` |
| **LogRequests** | `Toggle` | `Log HTTP request details to console` | `false` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Port** | `port` | `int` | `Active server TCP port` |
| **Connected Clients** | `connectedclients` | `int` | `Active client connections` |
| **Total Requests** | `totalrequests` | `int` | `Total HTTP/MCP requests processed` |
| **Server Status** | `serverstatus` | `string` | `Current server state (Running, Stopped, Error)` |
| **Swagger URL** | `swaggerurl` | `string` | `Local URL to Swagger UI documentation` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Stopped** | `stopped` | `HTTP Server Stopped` | `Server offline` |
| **Starting** | `starting` | `HTTP Server Starting...` | `Server initializing` |
| **Running** | `running` | `HTTP Server on port {0}` | `Server active and listening` |
| **Error** | `error` | `HTTP Server Error: {0}` | `Server error state` |
| **Stopping** | `stopping` | `HTTP Server Stopping...` | `Server shutting down` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Server Started** | `onserverstarted` | `Server started on port {0}` | `Triggered when server starts listening` |
| **On Server Stopped** | `onserverstopped` | `Server stopped` | `Triggered when server stops` |
| **On Request Received** | `onrequestreceived` | `Request: {0} {1}` | `Triggered on incoming HTTP request` |
| **On MCP Tool Executed** | `onmcptoolexecuted` | `MCP Tool: {0}` | `Triggered when an AI Agent invokes an MCP tool` |
| **On Error** | `onerror` | `Server Error: {0}` | `Triggered on server exception` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/HTTPServer/Running** | `bool` | `Write` | `True if HTTP server is running` |
| **VRCOSC/HTTPServer/Port** | `int` | `Write` | `Active HTTP server port` |
| **VRCOSC/HTTPServer/Requests** | `int` | `Write` | `Total processed request count` |
| **VRCOSC/HTTPServer/Error** | `bool` | `Write` | `True if server is in error state` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Get HTTP Server Status** | `Flow trigger` | `Is Running (bool), Port (int), Requests (int)` | `Returns HTTP server state` |
<!-- AUTOGEN:NODES:END -->

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Port** | `TextBox` | `HTTP server port (1024-65535)` | `"8080"` |
| **Allow External Connections** | `Toggle` | `Allow connections from other devices on network` | `false` |
| **Require Authentication** | `Toggle` | `Require bearer token authentication` | `false` |
| **Authentication Token** | `TextBox` | `Bearer token for authentication (leave empty to generate)` | `empty` |
| **Enable CORS** | `Toggle` | `Enable Cross-Origin Resource Sharing` | `true` |
| **CORS Origins** | `TextBox` | `Allowed CORS origins (comma-separated, * for all)` | `"*"` |
| **Log Requests** | `Toggle` | `Log all HTTP requests to console` | `true` |
| **Auto Start** | `Toggle` | `Start server automatically when module loads` | `true` |
| **MCP Server** | `Toggle` | `Expose the MCP endpoints under /mcp so an AI agent can control VRCOSC. Restart the server to apply.` | `true` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Server Status** | `serverstatus` | `string` | `ChatBox variable Server Status` |
| **Server URL** | `serverurl` | `string` | `ChatBox variable Server URL` |
| **Last Request** | `lastrequest` | `string` | `ChatBox variable Last Request` |
| **Last Response** | `lastresponse` | `string` | `ChatBox variable Last Response` |
| **Request Count** | `requestcount` | `int` | `ChatBox variable Request Count` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Stopped** | `stopped` | `HTTP/MCP Server: Stopped` | `Stopped state` |
| **Starting** | `starting` | `HTTP/MCP Server: Starting...` | `Starting state` |
| **Running** | `running` | `HTTP/MCP Server: Running\n{0}` | `Running state` |
| **Stopping** | `stopping` | `HTTP/MCP Server: Stopping...` | `Stopping state` |
| **Error** | `error` | `HTTP/MCP Server: Error\n{0}` | `Error state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Server Started** | `onserverstarted` | `On Server Started` | `Triggered on On Server Started` |
| **On Server Stopped** | `onserverstopped` | `On Server Stopped` | `Triggered on On Server Stopped` |
| **On Request Received** | `onrequestreceived` | `On Request Received` | `Triggered on On Request Received` |
| **On Request Processed** | `onrequestprocessed` | `On Request Processed` | `Triggered on On Request Processed` |
| **On Error** | `onerror` | `On Error` | `Triggered on On Error` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/HTTPServer/Running** | `bool` | `Write` | `True when server is running` |
| **VRCOSC/HTTPServer/RequestReceived** | `bool` | `Write` | `True for 1 second when request is received` |
| **VRCOSC/HTTPServer/RequestCount** | `int` | `Write` | `Total number of requests processed` |
| **VRCOSC/HTTPServer/StatusCode** | `int` | `Write` | `Last response status code` |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **Start H T T P Server** | `Flow trigger` | `Output` | `Node node for Start H T T P Server` |
| **Stop H T T P Server** | `Flow trigger` | `Output` | `Node node for Stop H T T P Server` |
| **Get H T T P Server Status** | `Flow trigger` | `Output` | `Node node for Get H T T P Server Status` |
<!-- NODES_TABLE_END -->
