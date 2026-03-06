# Charging Session APIs (with OCPP Integration)

This document describes the Charging Session management APIs with full OCPP protocol integration.

## Base URL
```
/api/ChargingSession
```

## Authentication
Most endpoints require JWT Bearer token authentication (except `charging-gun-status`).

## OCPP Integration
These APIs communicate with the actual OCPP charge points through the OCPP server. Each start/stop/unlock operation:
1. Validates the request and database records
2. Calls the OCPP server API to control the physical charge point
3. Handles the OCPP protocol response
4. Updates the database accordingly

**Configuration Required:**
- `ServerApiUrl`: The URL of your OCPP server (configured in appsettings.json)
- `ApiKey`: API key for authenticating with the OCPP server

---

## API Endpoints

### 1. Start Charging Session
**Endpoint:** `POST /api/ChargingSession/start-charging-session`  
**Authorization:** Required  
**Description:** Starts a new charging session and sends OCPP RemoteStartTransaction command to the charge point.

#### Request Body
```json
{
  "chargingGunId": "string",
  "chargingStationId": "string",
  "userId": "string",
  "chargeTagId": "string",
  "connectorId": 1,
  "startMeterReading": "string",
  "chargingTariff": "string"
}
```

**Field Descriptions:**
- `chargingGunId`: Your internal charging gun/connector identifier
- `chargingStationId`: Database ID of the charging station
- `userId`: User initiating the session
- `chargeTagId`: RFID tag or authorization token for OCPP protocol
- `connectorId`: Physical connector number on the charge point (typically 1 or 2)
- `startMeterReading`: Initial meter reading (optional for display)
- `chargingTariff`: Tariff rate per kWh

#### OCPP Flow:
1. Validates charging station exists and is active
2. Checks charge point is registered in database
3. Verifies no existing active session on the gun
4. Sends OCPP `RemoteStartTransaction` command to charge point
5. Waits for OCPP response (Accepted/Rejected/Timeout)
6. If accepted, creates database record

#### Response
```json
{
  "success": true,
  "message": "Charging session started successfully. OCPP Status: Transaction accepted by charge point",
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

#### OCPP Status Responses:
- **Accepted**: Charge point accepted the transaction
- **Rejected**: Charge point rejected the transaction (connector not available, authorization failed, etc.)
- **Timeout**: Charge point did not respond within 15 seconds
- **Offline**: Charge point is not connected to OCPP server

#### Error Responses
- **400 Bad Request:** Invalid request data, charging gun already in use, or OCPP rejected
- **404 Not Found:** Charging station or charge point not found
- **500 Internal Server Error:** Server error

---

### 2. End Charging Session
**Endpoint:** `POST /api/ChargingSession/end-charging-session`  
**Authorization:** Required  
**Description:** Ends an active charging session and sends OCPP RemoteStopTransaction command.

#### Request Body
```json
{
  "sessionId": "string",
  "endMeterReading": "string"
}
```

#### OCPP Flow:
1. Validates session exists and is active
2. Retrieves charge point information
3. Sends OCPP `RemoteStopTransaction` command to charge point
4. Waits for OCPP response (Accepted/Rejected/Timeout)
5. Updates database with end time and calculations (even if OCPP fails)

#### Response
```json
{
  "success": true,
  "message": "Charging session ended successfully. OCPP Status: Stop transaction accepted by charge point",
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

#### OCPP Behavior:
- If OCPP stop fails but session needs to end (e.g., charger offline), the database is still updated
- OCPP failure is logged as a warning, not an error

#### Error Responses
- **400 Bad Request:** Invalid request data or session already ended
- **404 Not Found:** Charging session, station, or charge point not found
- **500 Internal Server Error:** Server error

---

### 3. Unlock Connector
**Endpoint:** `POST /api/ChargingSession/unlock-connector`  
**Authorization:** Required  
**Description:** Sends OCPP UnlockConnector command to physically unlock a stuck connector.

#### Request Body
```json
{
  "chargingStationId": "string",
  "connectorId": 1
}
```

**Use Cases:**
- Connector cable is stuck in vehicle
- Emergency unlock needed
- Maintenance or troubleshooting

#### OCPP Flow:
1. Validates charging station exists
2. Retrieves charge point information
3. Sends OCPP `UnlockConnector` command
4. Waits for charge point response

#### Response
```json
{
  "success": true,
  "message": "Connector unlocked successfully",
  "data": {
    "chargingStationId": "string",
    "connectorId": 1,
    "chargePointId": "string",
    "status": "Unlocked"
  }
}
```

#### OCPP Status Responses:
- **Unlocked**: Connector unlocked successfully
- **UnlockFailed**: Physical unlock mechanism failed
- **OngoingAuthorizedTransaction**: Cannot unlock during authorized transaction
- **UnknownConnector**: Connector ID not recognized
- **NotSupported**: Charge point doesn't support unlock command
- **Timeout**: Charge point did not respond
- **Offline**: Charge point not connected

#### Error Responses
- **400 Bad Request:** OCPP unlock failed
- **404 Not Found:** Charging station or charge point not found
- **500 Internal Server Error:** Server error

---

### 4. Get Charging Gun Status
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

### Starting a Charging Session (with OCPP)
```bash
curl -X POST "https://your-api.com/api/ChargingSession/start-charging-session" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "chargingGunId": "GUN001",
    "chargingStationId": "STATION-GUID",
    "userId": "USER-GUID",
    "chargeTagId": "RFID-TAG-001",
    "connectorId": 1,
    "startMeterReading": "1000.50",
    "chargingTariff": "0.35"
  }'
```

**Expected Flow:**
1. API validates station and charge point
2. OCPP RemoteStartTransaction sent to charge point
3. Charge point responds "Accepted"
4. Database session record created
5. User can start charging

### Ending a Charging Session (with OCPP)
```bash
curl -X POST "https://your-api.com/api/ChargingSession/end-charging-session" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "SESSION-GUID",
    "endMeterReading": "1050.75"
  }'
```

**Expected Flow:**
1. API retrieves session and charge point
2. OCPP RemoteStopTransaction sent to charge point
3. Charge point stops transaction
4. Database updated with final readings and calculations
5. Billing information calculated

### Unlocking a Stuck Connector
```bash
curl -X POST "https://your-api.com/api/ChargingSession/unlock-connector" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "chargingStationId": "STATION-GUID",
    "connectorId": 1
  }'
```

**Expected Flow:**
1. API validates station and charge point
2. OCPP UnlockConnector command sent
3. Charge point attempts physical unlock
4. Response indicates success or failure reason

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

## OCPP Integration Details

### Architecture
```
Mobile App/Web → ChargingSessionController → OCPP Server → Charge Point
     ↓                      ↓                      ↓              ↓
  REST API          Database Update         WebSocket       Physical Action
```

### OCPP Commands Used

1. **RemoteStartTransaction**
   - Triggered by: `start-charging-session`
   - Parameters: ChargePointId, ConnectorId, IdTag (ChargeTagId)
   - Purpose: Authorize and start a charging transaction
   - Timeout: 15 seconds
   - Possible Responses: Accepted, Rejected

2. **RemoteStopTransaction**
   - Triggered by: `end-charging-session`
   - Parameters: ChargePointId, ConnectorId
   - Purpose: Stop an ongoing charging transaction
   - Timeout: 15 seconds
   - Possible Responses: Accepted, Rejected

3. **UnlockConnector**
   - Triggered by: `unlock-connector`
   - Parameters: ChargePointId, ConnectorId
   - Purpose: Physically unlock the connector cable
   - Timeout: 15 seconds
   - Possible Responses: Unlocked, UnlockFailed, OngoingAuthorizedTransaction, UnknownConnector, NotSupported

### Configuration

**appsettings.json:**
```json
{
  "ServerApiUrl": "https://your-ocpp-server.com/api",
  "ApiKey": "your-api-key-here"
}
```

### Error Handling Strategy

1. **Start Session:**
   - If OCPP fails → No database record created
   - User notified of failure reason
   - Can retry after fixing issue

2. **End Session:**
   - If OCPP fails → Database still updated
   - Warning logged but not treated as critical error
   - Ensures billing records are complete
   - Physical charging may need manual intervention

3. **Unlock Connector:**
   - If OCPP fails → Error returned to user
   - May need physical assistance
   - Critical for user experience

### Charge Point Requirements

For these APIs to work, charge points must:
1. Support OCPP 1.6 or later
2. Be registered in the database (ChargePoints table)
3. Be actively connected to the OCPP server
4. Support RemoteStartTransaction and RemoteStopTransaction
5. (Optional) Support UnlockConnector for unlock functionality

### Mapping Between Systems

| Database Entity | OCPP Concept | API Parameter |
|----------------|--------------|---------------|
| ChargingStation | - | chargingStationId |
| ChargePoint.ChargePointId | Charge Point Identifier | Resolved from station |
| ChargingGunId | - | chargingGunId (internal) |
| ConnectorId | Connector ID | connectorId (1, 2, etc.) |
| ChargeTagId | IdTag/RFID | chargeTagId |
| ChargingSession | Transaction | sessionId |

### Common Issues and Solutions

**Issue:** "Charge point is offline"
- **Cause:** Charge point not connected to OCPP server
- **Solution:** Check physical connection, network, and OCPP server logs

**Issue:** "Transaction rejected by charge point"
- **Cause:** Connector unavailable, authorization failed, or fault condition
- **Solution:** Check connector status, verify charge tag is authorized

**Issue:** "Charge point did not respond in time"
- **Cause:** Network latency or charge point processing delay
- **Solution:** Retry after a few seconds, check network quality

**Issue:** "Cannot unlock - ongoing authorized transaction"
- **Cause:** Active charging session prevents unlock
- **Solution:** End the charging session first, then unlock

### Testing Without Physical Charge Points

For development/testing without real hardware:
1. Use OCPP simulator tools (e.g., OCPP Station Simulator)
2. Configure ServerApiUrl to point to test environment
3. Mock charge point responses in test setup
4. Validate API logic independently of OCPP server

---

## Business Logic Notes

1. **Session Validation:**
   - Only one active session is allowed per charging gun at a time
   - Charging station must be active before starting a session
   - Charge point must be online and registered

2. **OCPP Transaction Flow:**
   - Start: API → OCPP Server → Charge Point → Authorization → Transaction Started
   - Stop: API → OCPP Server → Charge Point → Stop Transaction → Meter Value Reported
   - Unlock: API → OCPP Server → Charge Point → Physical Unlock Mechanism

3. **Billing Calculation:**
   - Energy is calculated as the difference between end and start meter readings
   - Total fee is calculated as: `energyTransmitted * chargingTariff`
   - Charging speed is calculated as: `energyTransmitted / duration (in hours)`
   - Calculations performed even if OCPP stop fails (for accurate billing)

4. **Status Management:**
   - **Active:** Session has started but not ended (EndTime is MinValue)
   - **Completed:** Session has been ended (EndTime is set)
   - Physical charge point status may differ if offline

5. **Soft Delete:**
   - Sessions use the `Active` field for soft delete functionality
   - Inactive sessions (Active = 0) are not returned in queries by default

---

## Security Considerations

1. **API Authentication:**
   - All write operations require JWT bearer token
   - Charging gun status check is public for user convenience

2. **OCPP Server Authentication:**
   - X-API-Key header used for OCPP server authentication
   - Configure strong API keys in production

3. **Authorization:**
   - Verify user has permission to start session at station
   - Validate ChargeTagId is associated with requesting user
   - Check user's wallet balance before starting session

4. **Data Validation:**
   - Validate all input parameters
   - Prevent SQL injection through parameterized queries
   - Sanitize connector IDs and charge point identifiers

---

## Future Enhancements

Potential features to consider:
1. **Real-time Updates:**
   - WebSocket notifications for session status changes
   - Push notifications when charging completes
   - Live meter value updates during charging

2. **Advanced OCPP Features:**
   - Smart charging profiles (load management)
   - Reservation system (ReserveNow command)
   - Diagnostics and firmware updates
   - Cost estimation before charging starts

3. **Payment Integration:**
   - Pre-authorization holds on payment methods
   - Automatic payment processing on session end
   - Multiple payment method support
   - Refunds for failed sessions

4. **Analytics:**
   - Session history and analytics
   - Charging patterns and predictions
   - Station utilization metrics
   - Carbon savings calculations

5. **User Experience:**
   - QR code scanning for station identification
   - In-app navigation to charging stations
   - Session sharing (for fleet management)
   - Charging schedules and reminders

6. **Operations:**
   - Automated fault detection and reporting
   - Remote diagnostics for charge points
   - Predictive maintenance alerts
   - Multi-tenant support for charge point operators

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
