# OCPP.Core API Documentation

## Table of Contents
- [Overview](#overview)
- [Authentication](#authentication)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [WebSocket Connection](#websocket-connection)
- [OCPP Message Flows](#ocpp-message-flows)
- [Error Responses](#error-responses)
- [Testing Examples](#testing-examples)

## Overview

OCPP.Core provides a comprehensive implementation of the Open Charge Point Protocol (OCPP) for electric vehicle charging infrastructure. This API supports both OCPP 1.6 and OCPP 2.0.1 protocols via WebSocket connections.

### Base URL
```
Production: wss://your-domain.com/ocpp
Development: ws://localhost:8080/ocpp
```

### Supported Protocols
- OCPP 1.6 JSON
- OCPP 2.0.1 JSON

---

## Authentication

### Overview
OCPP. Core supports multiple authentication methods to secure connections between Charge Points and the Central System. 

### 1. Basic Authentication
Uses HTTP Basic Authentication in the WebSocket handshake.

**Headers:**
```
Authorization: Basic <base64-encoded-credentials>
```

**Example:**
```
Username: CP001
Password: secretpassword
Authorization: Basic Q1AwMDE6c2VjcmV0cGFzc3dvcmQ=
```

### 2. API Key Authentication
Custom API key authentication via headers.

**Headers:**
```
X-API-Key: your-api-key-here
X-Charge-Point-Id: CP001
```

### 3. TLS Client Certificates
Mutual TLS authentication using client certificates.

**Configuration:**
```json
{
  "Security": {
    "RequireClientCertificate": true,
    "ValidateClientCertificate": true,
    "AllowedCertificateThumbprints": ["thumbprint1", "thumbprint2"]
  }
}
```

### 4. Token-Based Authentication
OAuth 2.0 or JWT tokens for advanced scenarios.

**Headers:**
```
Authorization: Bearer <jwt-token>
```

---

## Configuration

### Application Settings (appsettings.json)

```json
{
  "Logging": {
    "LogLevel":  {
      "Default": "Information",
      "Microsoft":  "Warning",
      "OCPP": "Debug"
    }
  },
  "OCPP": {
    "ServerEndpoint": "0.0.0.0",
    "ServerPort": 8080,
    "ProtocolVersion": "1.6",
    "HeartbeatInterval": 300,
    "ConnectionTimeout": 60,
    "MaxMessageSize": 65536,
    "EnableSSL": true,
    "CertificatePath": "/path/to/certificate.pfx",
    "CertificatePassword": "your-password"
  },
  "Database": {
    "ConnectionString":  "Server=localhost;Database=OCPPCore;User Id=sa;Password=YourPassword;",
    "Provider": "SqlServer"
  },
  "Security": {
    "RequireAuthentication": true,
    "AuthenticationMethod": "Basic",
    "AllowedChargePoints": ["CP001", "CP002", "CP003"]
  }
}
```

### Connection String Configuration

**SQL Server:**
```
Server=localhost;Database=OCPPCore;User Id=sa;Password=YourPassword;TrustServerCertificate=True;
```

**PostgreSQL:**
```
Host=localhost;Port=5432;Database=ocppcore;Username=postgres;Password=YourPassword;
```

**MySQL:**
```
Server=localhost;Port=3306;Database=ocppcore;Uid=root;Pwd=YourPassword;
```

---

## API Endpoints

### Management API

#### 1. Get Charge Point Status
```http
GET /api/chargepoints/{chargePointId}/status
```

**Response:**
```json
{
  "chargePointId": "CP001",
  "status": "Available",
  "lastHeartbeat": "2025-12-26T12:30:00Z",
  "connectors": [
    {
      "connectorId": 1,
      "status": "Available",
      "currentTransaction": null
    }
  ],
  "firmwareVersion": "1.2.3",
  "model": "ChargePoint-X1000"
}
```

#### 2. List All Charge Points
```http
GET /api/chargepoints
```

**Query Parameters:**
- `status` (optional): Filter by status (Available, Charging, Faulted, etc.)
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20)

**Response:**
```json
{
  "totalCount": 45,
  "page": 1,
  "pageSize": 20,
  "chargePoints": [
    {
      "chargePointId": "CP001",
      "status":  "Available",
      "lastSeen": "2025-12-26T12:30:00Z",
      "location": "Building A, Floor 1"
    }
  ]
}
```

#### 3. Start Remote Transaction
```http
POST /api/chargepoints/{chargePointId}/start-transaction
```

**Request Body:**
```json
{
  "connectorId": 1,
  "idTag": "USER123",
  "chargingProfile":  {
    "chargingProfileId": 1,
    "stackLevel": 0,
    "chargingProfilePurpose": "TxDefaultProfile",
    "chargingProfileKind": "Absolute",
    "chargingSchedule": {
      "chargingRateUnit": "W",
      "chargingSchedulePeriod": [
        {
          "startPeriod": 0,
          "limit": 32.0
        }
      ]
    }
  }
}
```

**Response:**
```json
{
  "status": "Accepted",
  "transactionId": 12345,
  "message": "Remote start request sent successfully"
}
```

#### 4. Stop Remote Transaction
```http
POST /api/chargepoints/{chargePointId}/stop-transaction
```

**Request Body:**
```json
{
  "transactionId": 12345
}
```

**Response:**
```json
{
  "status": "Accepted",
  "message": "Remote stop request sent successfully"
}
```

#### 5. Reset Charge Point
```http
POST /api/chargepoints/{chargePointId}/reset
```

**Request Body:**
```json
{
  "type": "Soft"
}
```

**Possible values:** `Soft`, `Hard`

**Response:**
```json
{
  "status": "Accepted",
  "message": "Reset command sent successfully"
}
```

#### 6. Update Firmware
```http
POST /api/chargepoints/{chargePointId}/update-firmware
```

**Request Body:**
```json
{
  "location": "https://firmware.example.com/firmware-v1.2.3.bin",
  "retrieveDate": "2025-12-27T00:00:00Z",
  "retries": 3,
  "retryInterval": 300
}
```

**Response:**
```json
{
  "status": "Accepted",
  "message": "Firmware update scheduled"
}
```

#### 7. Get Configuration
```http
GET /api/chargepoints/{chargePointId}/configuration
```

**Response:**
```json
{
  "configurationKey": [
    {
      "key":  "HeartbeatInterval",
      "readonly": false,
      "value":  "300"
    },
    {
      "key":  "MeterValueSampleInterval",
      "readonly": false,
      "value": "60"
    }
  ]
}
```

#### 8. Change Configuration
```http
POST /api/chargepoints/{chargePointId}/configuration
```

**Request Body:**
```json
{
  "key": "HeartbeatInterval",
  "value": "600"
}
```

**Response:**
```json
{
  "status": "Accepted",
  "message": "Configuration updated successfully"
}
```

#### 9. Get Transaction History
```http
GET /api/transactions
```

**Query Parameters:**
- `chargePointId` (optional): Filter by charge point
- `idTag` (optional): Filter by user ID tag
- `startDate` (optional): Start date (ISO 8601)
- `endDate` (optional): End date (ISO 8601)
- `page` (optional): Page number
- `pageSize` (optional): Items per page

**Response:**
```json
{
  "totalCount": 150,
  "transactions": [
    {
      "transactionId": 12345,
      "chargePointId": "CP001",
      "connectorId": 1,
      "idTag": "USER123",
      "startTime": "2025-12-26T10:00:00Z",
      "stopTime": "2025-12-26T12:00:00Z",
      "meterStart": 1000,
      "meterStop": 15000,
      "energyConsumed": 14. 0,
      "stopReason": "Local"
    }
  ]
}
```

---

## WebSocket Connection

### Connection Establishment

**OCPP 1.6:**
```javascript
const ws = new WebSocket('ws://localhost:8080/ocpp/CP001', ['ocpp1.6']);

ws.onopen = () => {
  console.log('Connected to OCPP Central System');
};
```

**OCPP 2.0.1:**
```javascript
const ws = new WebSocket('ws://localhost:8080/ocpp/CP001', ['ocpp2.0.1']);
```

### Message Format

All OCPP messages follow the JSON format: 

**Call (Request):**
```json
[2, "unique-message-id", "Action", {"field": "value"}]
```

**CallResult (Success Response):**
```json
[3, "unique-message-id", {"field": "value"}]
```

**CallError (Error Response):**
```json
[4, "unique-message-id", "ErrorCode", "Error description", {"details": "value"}]
```

---

## OCPP Message Flows

### 1. Boot Notification Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-001",
  "BootNotification",
  {
    "chargePointVendor": "VendorName",
    "chargePointModel": "Model-X1000",
    "chargePointSerialNumber": "SN123456",
    "firmwareVersion": "1.2.3",
    "iccid": "89012345678901234567",
    "imsi": "012345678901234"
  }
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-001",
  {
    "status": "Accepted",
    "currentTime": "2025-12-26T12:34:03Z",
    "interval": 300
  }
]
```

### 2. Authorize Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-002",
  "Authorize",
  {
    "idTag": "USER123"
  }
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-002",
  {
    "idTagInfo": {
      "status": "Accepted",
      "expiryDate": "2026-12-31T23:59:59Z",
      "parentIdTag": "PARENT001"
    }
  }
]
```

### 3. Start Transaction Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-003",
  "StartTransaction",
  {
    "connectorId": 1,
    "idTag": "USER123",
    "meterStart": 1000,
    "timestamp": "2025-12-26T12:00:00Z",
    "reservationId": 0
  }
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-003",
  {
    "transactionId": 12345,
    "idTagInfo": {
      "status":  "Accepted"
    }
  }
]
```

### 4. Heartbeat Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-004",
  "Heartbeat",
  {}
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-004",
  {
    "currentTime": "2025-12-26T12:34:03Z"
  }
]
```

### 5. Meter Values Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-005",
  "MeterValues",
  {
    "connectorId": 1,
    "transactionId": 12345,
    "meterValue": [
      {
        "timestamp":  "2025-12-26T12:15:00Z",
        "sampledValue": [
          {
            "value": "5000",
            "context": "Sample. Periodic",
            "format": "Raw",
            "measurand": "Energy.Active.Import. Register",
            "unit": "Wh"
          },
          {
            "value":  "16.5",
            "measurand": "Current.Import",
            "unit": "A"
          },
          {
            "value":  "230. 5",
            "measurand": "Voltage",
            "unit": "V"
          }
        ]
      }
    ]
  }
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-005",
  {}
]
```

### 6. Stop Transaction Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-006",
  "StopTransaction",
  {
    "transactionId": 12345,
    "idTag": "USER123",
    "timestamp": "2025-12-26T14:00:00Z",
    "meterStop": 15000,
    "reason": "Local",
    "transactionData": [
      {
        "timestamp": "2025-12-26T13:00:00Z",
        "sampledValue": [
          {
            "value": "10000",
            "measurand": "Energy.Active. Import.Register",
            "unit": "Wh"
          }
        ]
      }
    ]
  }
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-006",
  {
    "idTagInfo": {
      "status": "Accepted"
    }
  }
]
```

### 7. Status Notification Flow

**Charge Point → Central System:**
```json
[
  2,
  "msg-007",
  "StatusNotification",
  {
    "connectorId": 1,
    "errorCode": "NoError",
    "status": "Charging",
    "timestamp": "2025-12-26T12:30:00Z",
    "info": "Connected to vehicle",
    "vendorId": "VendorName",
    "vendorErrorCode": ""
  }
]
```

**Central System → Charge Point:**
```json
[
  3,
  "msg-007",
  {}
]
```

### 8. Remote Start Transaction Flow

**Central System → Charge Point:**
```json
[
  2,
  "msg-008",
  "RemoteStartTransaction",
  {
    "connectorId": 1,
    "idTag": "USER123",
    "chargingProfile": {
      "chargingProfileId": 1,
      "stackLevel": 0,
      "chargingProfilePurpose": "TxDefaultProfile",
      "chargingProfileKind": "Absolute",
      "chargingSchedule": {
        "chargingRateUnit": "W",
        "chargingSchedulePeriod": [
          {
            "startPeriod": 0,
            "limit": 32.0
          }
        ]
      }
    }
  }
]
```

**Charge Point → Central System:**
```json
[
  3,
  "msg-008",
  {
    "status": "Accepted"
  }
]
```

---

## Error Responses

### HTTP Error Codes

| Code | Description | Example Scenario |
|------|-------------|------------------|
| 400 | Bad Request | Invalid JSON format or missing required fields |
| 401 | Unauthorized | Invalid or missing authentication credentials |
| 403 | Forbidden | Insufficient permissions for the requested operation |
| 404 | Not Found | Charge point or resource not found |
| 409 | Conflict | Operation conflicts with current state |
| 500 | Internal Server Error | Unexpected server error |
| 503 | Service Unavailable | Server temporarily unavailable |

### OCPP Error Codes

| Error Code | Description |
|------------|-------------|
| NotImplemented | Requested action is not supported |
| NotSupported | Request is not supported |
| InternalError | Internal error occurred |
| ProtocolError | Protocol violation |
| SecurityError | Security-related error |
| FormationViolation | Message format violation |
| PropertyConstraintViolation | Property constraint violation |
| OccurrenceConstraintViolation | Message occurrence constraint violation |
| TypeConstraintViolation | Type constraint violation |
| GenericError | Generic error |

### Example Error Responses

**HTTP 400 Bad Request:**
```json
{
  "error": "BadRequest",
  "message": "Invalid request format",
  "details": {
    "field": "connectorId",
    "issue": "Connector ID must be a positive integer"
  }
}
```

**HTTP 401 Unauthorized:**
```json
{
  "error": "Unauthorized",
  "message": "Invalid authentication credentials"
}
```

**HTTP 404 Not Found:**
```json
{
  "error": "NotFound",
  "message": "Charge point 'CP999' not found"
}
```

**OCPP CallError:**
```json
[
  4,
  "msg-error-001",
  "NotSupported",
  "The requested action is not supported by this charge point",
  {
    "action": "GetCompositeSchedule"
  }
]
```

---

## Testing Examples

### 1. Using cURL

**Authenticate and Get Charge Point Status:**
```bash
curl -X GET \
  -H "Authorization: Basic Q1AwMDE6c2VjcmV0cGFzc3dvcmQ=" \
  http://localhost:8080/api/chargepoints/CP001/status
```

**Start Remote Transaction:**
```bash
curl -X POST \
  -H "Content-Type: application/json" \
  -H "Authorization: Basic Q1AwMDE6c2VjcmV0cGFzc3dvcmQ=" \
  -d '{
    "connectorId": 1,
    "idTag":  "USER123"
  }' \
  http://localhost:8080/api/chargepoints/CP001/start-transaction
```

### 2. Using Python

```python
import websocket
import json
import uuid

def send_ocpp_message(ws, action, payload):
    message_id = str(uuid.uuid4())
    message = [2, message_id, action, payload]
    ws.send(json.dumps(message))
    return message_id

def on_message(ws, message):
    print(f"Received:  {message}")
    data = json.loads(message)
    
    if data[0] == 3:  # CallResult
        print(f"Success: {data[2]}")
    elif data[0] == 4:  # CallError
        print(f"Error: {data[2]} - {data[3]}")

def on_open(ws):
    print("Connection established")
    
    # Send BootNotification
    boot_payload = {
        "chargePointVendor": "TestVendor",
        "chargePointModel": "TestModel",
        "firmwareVersion": "1.0.0"
    }
    send_ocpp_message(ws, "BootNotification", boot_payload)

# Connect to OCPP Central System
ws = websocket.WebSocketApp(
    "ws://localhost:8080/ocpp/CP001",
    subprotocols=["ocpp1.6"],
    on_open=on_open,
    on_message=on_message
)

ws.run_forever()
```

### 3. Using Node.js

```javascript
const WebSocket = require('ws');
const { v4: uuidv4 } = require('uuid');

const ws = new WebSocket('ws://localhost:8080/ocpp/CP001', ['ocpp1.6'], {
  headers: {
    'Authorization': 'Basic Q1AwMDE6c2VjcmV0cGFzc3dvcmQ='
  }
});

function sendOCPPMessage(action, payload) {
  const messageId = uuidv4();
  const message = [2, messageId, action, payload];
  ws.send(JSON.stringify(message));
  return messageId;
}

ws.on('open', () => {
  console.log('Connected to OCPP Central System');
  
  // Send BootNotification
  sendOCPPMessage('BootNotification', {
    chargePointVendor: 'TestVendor',
    chargePointModel: 'TestModel',
    firmwareVersion: '1.0.0'
  });
});

ws.on('message', (data) => {
  const message = JSON.parse(data);
  console.log('Received:', message);
  
  if (message[0] === 3) {
    console.log('CallResult:', message[2]);
  } else if (message[0] === 4) {
    console.error('CallError:', message[2], message[3]);
  }
});

ws.on('error', (error) => {
  console.error('WebSocket error:', error);
});

ws.on('close', () => {
  console.log('Connection closed');
});
```

### 4. Using Postman Collection

**Example Collection Structure:**

```json
{
  "info": {
    "name": "OCPP. Core API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "auth": {
    "type": "basic",
    "basic": [
      {
        "key":  "username",
        "value": "CP001"
      },
      {
        "key": "password",
        "value": "secretpassword"
      }
    ]
  },
  "item": [
    {
      "name": "Get Charge Point Status",
      "request": {
        "method": "GET",
        "url": "{{base_url}}/api/chargepoints/{{chargePointId}}/status"
      }
    },
    {
      "name": "Start Remote Transaction",
      "request":  {
        "method": "POST",
        "url": "{{base_url}}/api/chargepoints/{{chargePointId}}/start-transaction",
        "body": {
          "mode": "raw",
          "raw": "{\n  \"connectorId\": 1,\n  \"idTag\": \"USER123\"\n}"
        }
      }
    }
  ]
}
```

### 5. Integration Testing Example

```csharp
using Xunit;
using System.Net.Http;
using System.Threading.Tasks;

public class OCPPApiTests
{
    private readonly HttpClient _client;
    
    public OCPPApiTests()
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
        
        // Add Basic Authentication
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("CP001:secretpassword")
        );
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Basic", credentials);
    }
    
    [Fact]
    public async Task GetChargePointStatus_ReturnsSuccess()
    {
        var response = await _client. GetAsync("/api/chargepoints/CP001/status");
        
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("CP001", content);
    }
    
    [Fact]
    public async Task StartRemoteTransaction_ReturnsAccepted()
    {
        var payload = new StringContent(
            "{\"connectorId\": 1, \"idTag\": \"USER123\"}",
            Encoding.UTF8,
            "application/json"
        );
        
        var response = await _client.PostAsync(
            "/api/chargepoints/CP001/start-transaction",
            payload
        );
        
        Assert.True(response. IsSuccessStatusCode);
    }
}
```

---

## Best Practices

### 1. Connection Management
- Implement automatic reconnection with exponential backoff
- Handle WebSocket ping/pong frames for connection health monitoring
- Monitor heartbeat intervals to detect connection issues

### 2. Message Handling
- Generate unique message IDs using UUIDs
- Implement timeout handling for requests (typically 30-60 seconds)
- Queue messages when connection is temporarily unavailable
- Validate message format before sending

### 3. Error Handling
- Log all errors with sufficient context
- Implement retry logic for transient failures
- Gracefully handle unsupported operations
- Provide meaningful error messages

### 4. Security
- Always use TLS/SSL in production
- Rotate credentials regularly
- Implement rate limiting to prevent abuse
- Validate all input data
- Store sensitive data securely (encrypted at rest)

### 5. Performance
- Batch meter values when possible
- Implement efficient database queries with indexes
- Use connection pooling
- Monitor and optimize message processing times

---

## Support and Resources

### Documentation
- OCPP 1.6 Specification:  https://www.openchargealliance.org/protocols/ocpp-16/
- OCPP 2.0.1 Specification: https://www.openchargealliance.org/protocols/ocpp-201/

### Community
- GitHub Issues: https://github.com/dallmann-consulting/OCPP. Core/issues
- Discussion Forum: https://github.com/dallmann-consulting/OCPP.Core/discussions

### Contact
For enterprise support and consulting services, please contact Dallmann Consulting. 

---

**Document Version:** 1.0.0  
**Last Updated:** 2025-12-26  
**Maintained by:** OCPP. Core Development Team