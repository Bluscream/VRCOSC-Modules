# HTTP Server Module

Control OSC parameters via HTTP requests. This module runs an HTTP server that allows external applications and devices to interact with VRChat OSC through a RESTful API.

## Features

- **HTTP Server**: Lightweight HTTP server with configurable port
- **OSC Integration**: Control OSC parameters through HTTP endpoints
- **Authentication**: Optional bearer token authentication
- **CORS Support**: Enable cross-origin requests for web applications
- **Concurrent Requests**: Handle multiple simultaneous requests
- **Auto-start**: Optionally start server when module loads

## Configuration

### Server Settings
- **Port**: HTTP server port (default: 8080)
- **Allow External Connections**: Allow connections from other devices on your network
- **Max Concurrent Requests**: Maximum number of simultaneous request handlers
- **Auto Start**: Start server automatically when module loads

### Security Settings
- **Require Authentication**: Enable bearer token authentication
- **Authentication Token**: Bearer token for authentication (leave empty to auto-generate)
- **Enable CORS**: Enable Cross-Origin Resource Sharing
- **CORS Origins**: Allowed CORS origins (comma-separated, * for all)

### Debug Settings
- **Log Requests**: Log all HTTP requests to console

## Nodes

### Start HTTP Server
Starts the HTTP server.

**Outputs:**
- `Success` (bool): True if server started successfully
- `Server URL` (string): The server URL
- `Error` (string): Error message if failed

### Stop HTTP Server
Stops the HTTP server.

**Outputs:**
- `Success` (bool): True if server stopped successfully

### Get HTTP Server Status
Gets the current server status.

**Outputs:**
- `Is Running` (bool): True if server is running
- `Server URL` (string): The server URL

## API Endpoints

### GET /
Returns server status and basic information.

**Response:**
```json
{
  "message": "HTTP Server is running",
  "path": "/",
  "method": "GET",
  "timestamp": "2025-11-03T12:00:00.000Z",
  "requestCount": 42
}
```

### Future Endpoints (To Be Implemented)

- `GET /osc/parameters` - List all OSC parameters
- `GET /osc/parameters/{name}` - Get specific parameter value
- `POST /osc/parameters/{name}` - Set parameter value
- `GET /avatars/current` - Get current avatar info
- `POST /chatbox/send` - Send chatbox message

## Example Usage

### Basic Request (No Authentication)
```bash
curl http://localhost:8080/
```

### Authenticated Request
```bash
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:8080/
```

### From JavaScript/Web
```javascript
fetch('http://localhost:8080/', {
  headers: {
    'Authorization': 'Bearer YOUR_TOKEN'
  }
})
.then(response => response.json())
.then(data => console.log(data));
```

## OSC Parameters

- `VRCOSC/HTTPServer/Running`: True when server is running
- `VRCOSC/HTTPServer/RequestReceived`: True for 1 second when request is received
- `VRCOSC/HTTPServer/RequestCount`: Total number of requests processed
- `VRCOSC/HTTPServer/StatusCode`: Last response status code

## ChatBox Integration

The module provides ChatBox states and variables:
- **States**: Stopped, Starting, Running, Stopping, Error
- **Variables**: Server Status, Last Request, Last Response, Request Count, Server URL
- **Events**: On Server Started, On Server Stopped, On Request Received, On Request Processed, On Error

## Security Notes

- By default, the server only accepts connections from localhost
- Enable "Allow External Connections" to accept connections from other devices
- Always use authentication when allowing external connections
- Keep your authentication token secure and don't commit it to version control

## Roadmap

- [ ] OSC parameter GET/SET endpoints
- [ ] Avatar information endpoints
- [ ] ChatBox message sending
- [ ] WebSocket support for real-time updates
- [ ] Custom route registration
- [ ] Request rate limiting
- [ ] HTTPS support

## License

GPL-3.0 - See LICENSE file for details
