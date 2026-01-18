# Charging Session APIs

This document describes the Charging Session management APIs.

## Base URL
```
/api/ChargingSession
```

## Authentication
Most endpoints require JWT Bearer token authentication (except `charging-gun-status`).

---

## API Endpoints

### 1. Start Charging Session
**Endpoint:** `POST /api/ChargingSession/start-charging-session`  
**Authorization:** Required  
**Description:** Starts a new charging session for a specific charging gun.

#### Request Body
```json
{
  "chargingGunId": "string",
  "chargingStationId": "string",
  "userId": "string",
  "startMeterReading": "string",
  "chargingTariff": "string"
}
```

#### Response
```json
{
  "success": true,
  "message": "Charging session started successfully",
  "data": {
    "recId": "guid",
    "chargingGunId": "string",
    "chargingStationId": "string",
    "chargingStationName": "string",
    "chargingHubName": "string",
    "startMeterReading": "string",
    "endMeterReading": "0",
    "energyTransmitted": "0",
    "startTime": "datetime",
    "endTime": null,
    "chargingSpeed": "0",
    "chargingTariff": "string",
    "chargingTotalFee": "0",
    "status": "Active",
    "duration": "00:00:00",
    "active": 1,
    "createdOn": "datetime",
    "updatedOn": "datetime"
  }
}
```

#### Error Responses
- **400 Bad Request:** Invalid request data or charging gun already in use
- **404 Not Found:** Charging station not found or inactive
- **500 Internal Server Error:** Server error

---

### 2. End Charging Session
**Endpoint:** `POST /api/ChargingSession/end-charging-session`  
**Authorization:** Required  
**Description:** Ends an active charging session and calculates final billing.

#### Request Body
```json
{
  "sessionId": "string",
  "endMeterReading": "string"
}
```

#### Response
```json
{
  "success": true,
  "message": "Charging session ended successfully",
  "data": {
    "recId": "guid",
    "chargingGunId": "string",
    "chargingStationId": "string",
    "chargingStationName": "string",
    "chargingHubName": "string",
    "startMeterReading": "string",
    "endMeterReading": "string",
    "energyTransmitted": "string",
    "startTime": "datetime",
    "endTime": "datetime",
    "chargingSpeed": "string",
    "chargingTariff": "string",
    "chargingTotalFee": "string",
    "status": "Completed",
    "duration": "HH:mm:ss",
    "active": 1,
    "createdOn": "datetime",
    "updatedOn": "datetime"
  }
}
```

#### Calculations
- **Energy Transmitted:** `endMeterReading - startMeterReading` (in kWh)
- **Charging Total Fee:** `energyTransmitted * chargingTariff`
- **Charging Speed:** `energyTransmitted / duration (in hours)` (in kW)

#### Error Responses
- **400 Bad Request:** Invalid request data or session already ended
- **404 Not Found:** Charging session not found
- **500 Internal Server Error:** Server error

---

### 3. Get Charging Gun Status
**Endpoint:** `GET /api/ChargingSession/charging-gun-status/{chargingGunId}`  
**Authorization:** Not Required  
**Description:** Gets the current status of a specific charging gun.

#### Path Parameters
- `chargingGunId` (string): The ID of the charging gun

#### Response
```json
{
  "success": true,
  "message": "Charging gun status retrieved successfully",
  "data": {
    "chargingGunId": "string",
    "chargingStationId": "string",
    "chargingStationName": "string",
    "status": "Available|In Use",
    "currentSessionId": "string or null",
    "lastStatusUpdate": "datetime",
    "isAvailable": true|false
  }
}
```

#### Status Values
- **Available:** Charging gun is free and ready to use
- **In Use:** Charging gun is currently being used in an active session

#### Error Responses
- **500 Internal Server Error:** Server error

---

### 4. Get Charging Session Details
**Endpoint:** `GET /api/ChargingSession/charging-session-details/{sessionId}`  
**Authorization:** Required  
**Description:** Gets detailed information about a specific charging session.

#### Path Parameters
- `sessionId` (string): The ID of the charging session

#### Response
```json
{
  "success": true,
  "message": "Charging session details retrieved successfully",
  "data": {
    "recId": "guid",
    "chargingGunId": "string",
    "chargingStationId": "string",
    "chargingStationName": "string",
    "chargingHubName": "string",
    "startMeterReading": "string",
    "endMeterReading": "string",
    "energyTransmitted": "string",
    "startTime": "datetime",
    "endTime": "datetime or null",
    "chargingSpeed": "string",
    "chargingTariff": "string",
    "chargingTotalFee": "string",
    "status": "Active|Completed",
    "duration": "HH:mm:ss",
    "active": 1,
    "createdOn": "datetime",
    "updatedOn": "datetime"
  }
}
```

#### Error Responses
- **404 Not Found:** Charging session not found
- **500 Internal Server Error:** Server error

---

### 5. Get Charging Sessions (List with Filters)
**Endpoint:** `GET /api/ChargingSession/charging-sessions`  
**Authorization:** Required  
**Description:** Gets a paginated list of charging sessions with optional filters.

#### Query Parameters
- `stationId` (string, optional): Filter by charging station ID
- `status` (string, optional): Filter by status ("active" or "completed")
- `pageSize` (int, optional, default: 50): Number of records per page
- `page` (int, optional, default: 1): Page number

#### Response
```json
{
  "success": true,
  "message": "Charging sessions retrieved successfully",
  "data": {
    "totalRecords": 100,
    "page": 1,
    "pageSize": 50,
    "totalPages": 2,
    "sessions": [
      {
        "recId": "guid",
        "chargingGunId": "string",
        "chargingStationId": "string",
        "chargingStationName": "string",
        "chargingHubName": "string",
        "startMeterReading": "string",
        "endMeterReading": "string",
        "energyTransmitted": "string",
        "startTime": "datetime",
        "endTime": "datetime or null",
        "chargingSpeed": "string",
        "chargingTariff": "string",
        "chargingTotalFee": "string",
        "status": "Active|Completed",
        "duration": "HH:mm:ss",
        "active": 1,
        "createdOn": "datetime",
        "updatedOn": "datetime"
      }
    ]
  }
}
```

#### Error Responses
- **500 Internal Server Error:** Server error

---

## Example Usage

### Starting a Charging Session
```bash
curl -X POST "https://your-api.com/api/ChargingSession/start-charging-session" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "chargingGunId": "GUN001",
    "chargingStationId": "STATION-GUID",
    "userId": "USER-GUID",
    "startMeterReading": "1000.50",
    "chargingTariff": "0.35"
  }'
```

### Ending a Charging Session
```bash
curl -X POST "https://your-api.com/api/ChargingSession/end-charging-session" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "SESSION-GUID",
    "endMeterReading": "1050.75"
  }'
```

### Checking Charging Gun Status
```bash
curl -X GET "https://your-api.com/api/ChargingSession/charging-gun-status/GUN001"
```

### Getting Session Details
```bash
curl -X GET "https://your-api.com/api/ChargingSession/charging-session-details/SESSION-GUID" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Listing Charging Sessions
```bash
# Get active sessions for a specific station
curl -X GET "https://your-api.com/api/ChargingSession/charging-sessions?stationId=STATION-GUID&status=active&page=1&pageSize=20" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

---

## Business Logic Notes

1. **Session Validation:**
   - Only one active session is allowed per charging gun at a time
   - Charging station must be active before starting a session

2. **Billing Calculation:**
   - Energy is calculated as the difference between end and start meter readings
   - Total fee is calculated as: `energyTransmitted * chargingTariff`
   - Charging speed is calculated as: `energyTransmitted / duration (in hours)`

3. **Status Management:**
   - **Active:** Session has started but not ended (EndTime is MinValue)
   - **Completed:** Session has been ended (EndTime is set)

4. **Soft Delete:**
   - Sessions use the `Active` field for soft delete functionality
   - Inactive sessions (Active = 0) are not returned in queries by default

---

## Data Models

### ChargingSession Entity
```csharp
public class ChargingSession
{
    public string RecId { get; set; }              // Unique session ID (GUID)
    public string ChargingGunId { get; set; }      // ID of the charging gun
    public string ChargingStationID { get; set; }  // ID of the charging station
    public string StartMeterReading { get; set; }  // Meter reading at start
    public string EndMeterReading { get; set; }    // Meter reading at end
    public string EnergyTransmitted { get; set; }  // kWh transmitted
    public DateTime StartTime { get; set; }        // Session start time
    public DateTime EndTime { get; set; }          // Session end time (MinValue if active)
    public string ChargingSpeed { get; set; }      // Charging speed in kW
    public string ChargingTariff { get; set; }     // Cost per kWh
    public string ChargingTotalFee { get; set; }   // Total cost
    public int Active { get; set; }                // 1 = active, 0 = deleted
    public DateTime CreatedOn { get; set; }        // Record creation time
    public DateTime UpdatedOn { get; set; }        // Record update time
}
```

---

## Error Handling

All endpoints return consistent error responses:

```json
{
  "success": false,
  "message": "Error description"
}
```

Common HTTP status codes:
- **200 OK:** Successful operation
- **400 Bad Request:** Invalid input or business rule violation
- **401 Unauthorized:** Missing or invalid JWT token
- **404 Not Found:** Resource not found
- **500 Internal Server Error:** Server error

---

## Future Enhancements

Potential features to consider:
1. Real-time session monitoring via WebSocket
2. Integration with payment gateway for automatic billing
3. Session history and analytics
4. User notifications for session events
5. Integration with OCPP protocol for real charger communication
6. Multi-user session management (group charging)
7. Dynamic tariff adjustments based on time of day
