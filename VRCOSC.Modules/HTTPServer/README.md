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
