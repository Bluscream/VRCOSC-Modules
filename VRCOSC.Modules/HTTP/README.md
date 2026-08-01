# HTTP Module

Send HTTP requests (GET, POST, PUT, DELETE) and receive responses for web automation and API integration.

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

---

## Setup & Requirements

- Network connection to target HTTP/HTTPS endpoints.

## Module Settings

| Setting Name | Type | Description | Default |
|---|---|---|---|
| **DefaultUrl** | `TextBox` | Default target URL for HTTP requests | `empty` |
| **TimeoutMs** | `Slider` | Request timeout in milliseconds | `5000` |
| **LogDebug** | `Toggle` | Log detailed HTTP request/response debug info | `false` |

## ChatBox Variables

| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Last URL** | `lasturl` | `string` | URL of the most recent HTTP request |
| **Status Code** | `statuscode` | `int` | HTTP status code of the last response (e.g. 200, 404) |
| **Last Response** | `lastresponse` | `string` | Body text of the most recent HTTP response |
| **Request Count** | `requestcount` | `int` | Total number of HTTP requests executed |

## ChatBox States

| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `HTTP Idle` | Module ready |
| **Requesting** | `requesting` | `Requesting {0}...` | HTTP request in progress |
| **Success** | `success` | `HTTP {0} OK` | Request succeeded (2xx status) |
| **Failed** | `failed` | `HTTP Error {0}` | Request failed or returned error status |

## ChatBox Events

| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Success** | `onsuccess` | `HTTP Success: {0}` | Triggered on successful HTTP response |
| **On Failed** | `onfailed` | `HTTP Failed: {0}` | Triggered on request failure or non-2xx status |

## Avatar OSC Parameters

| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| `VRCOSC/HTTP/Send` | `bool` | `Read` | Set to true to execute default HTTP request |
| `VRCOSC/HTTP/Success` | `bool` | `Write` | True if last request succeeded |
| `VRCOSC/HTTP/StatusCode` | `int` | `Write` | Last HTTP status code |
| `VRCOSC/HTTP/RequestCount` | `int` | `Write` | Total HTTP requests executed |

## Nodes Overview

| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **HTTP GET Request** | URL (string), Headers (Dict) | Response (string), Status Code (int), Success (bool) | Executes HTTP GET request |
| **HTTP POST Request** | URL (string), Body (string), Headers (Dict) | Response (string), Status Code (int), Success (bool) | Executes HTTP POST request |
| **HTTP Request** | Method (string), URL (string), Body (string), Headers (Dict) | Response (string), Status Code (int), Success (bool) | Executes custom HTTP request |

---

## License

Copyright (c) Bluscream. Licensed under the GPL-3.0 License.

## Module Settings

<!-- AUTOGEN:SETTINGS:START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **DefaultUrl** | `TextBox` | `Default target URL for HTTP requests` | `empty` |
| **TimeoutMs** | `Slider` | `Request timeout in milliseconds` | `5000` |
| **LogDebug** | `Toggle` | `Log detailed HTTP request/response debug info` | `false` |
<!-- AUTOGEN:SETTINGS:END -->

## ChatBox Variables

<!-- AUTOGEN:VARIABLES:START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Last URL** | `lasturl` | `string` | `URL of the most recent HTTP request` |
| **Status Code** | `statuscode` | `int` | `HTTP status code of the last response (e.g. 200, 404)` |
| **Last Response** | `lastresponse` | `string` | `Body text of the most recent HTTP response` |
| **Request Count** | `requestcount` | `int` | `Total number of HTTP requests executed` |
<!-- AUTOGEN:VARIABLES:END -->

## ChatBox States

<!-- AUTOGEN:STATES:START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Idle** | `idle` | `HTTP Idle` | `Module ready` |
| **Requesting** | `requesting` | `Requesting {0}...` | `HTTP request in progress` |
| **Success** | `success` | `HTTP {0} OK` | `Request succeeded (2xx status)` |
| **Failed** | `failed` | `HTTP Error {0}` | `Request failed or returned error status` |
<!-- AUTOGEN:STATES:END -->

## ChatBox Events

<!-- AUTOGEN:EVENTS:START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **On Success** | `onsuccess` | `HTTP Success: {0}` | `Triggered on successful HTTP response` |
| **On Failed** | `onfailed` | `HTTP Failed: {0}` | `Triggered on request failure or non-2xx status` |
<!-- AUTOGEN:EVENTS:END -->

## Avatar OSC Parameters

<!-- AUTOGEN:OSC_PARAMS:START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| **VRCOSC/HTTP/Send** | `bool` | `Read` | `Set to true to execute default HTTP request` |
| **VRCOSC/HTTP/Success** | `bool` | `Write` | `True if last request succeeded` |
| **VRCOSC/HTTP/StatusCode** | `int` | `Write` | `Last HTTP status code` |
| **VRCOSC/HTTP/RequestCount** | `int` | `Write` | `Total HTTP requests executed` |
<!-- AUTOGEN:OSC_PARAMS:END -->

## Nodes Overview

<!-- AUTOGEN:NODES:START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| **HTTP GET Request** | `URL (string), Headers (Dict)` | `Response (string), Status Code (int), Success (bool)` | `Executes HTTP GET request` |
| **HTTP POST Request** | `URL (string), Body (string), Headers (Dict)` | `Response (string), Status Code (int), Success (bool)` | `Executes HTTP POST request` |
| **HTTP Request** | `Method (string), URL (string), Body (string), Headers (Dict)` | `Response (string), Status Code (int), Success (bool)` | `Executes custom HTTP request` |
<!-- AUTOGEN:NODES:END -->
