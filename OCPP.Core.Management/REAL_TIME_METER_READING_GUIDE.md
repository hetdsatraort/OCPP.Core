# Real-Time Meter Reading & SOC Calculation Guide

## Overview
This guide explains how the system now uses real-time meter values from charge points/connectors and calculates State of Charge (SOC) when ending charging sessions.

## Problem Statement

### Issue 1: Not Seeing Increasing Meter Values
**Root Cause:** The OCPP charge points send `MeterValues.req` messages periodically during charging, but we weren't using them when ending sessions.

**Solution:** The system now:
1. Captures real-time meter values from `ConnectorStatus.LastMeter`
2. Uses multiple data sources with priority fallback
3. Updates `ChargingGuns.ChargerMeterReading` after each session

### Issue 2: Need SOC-Based Calculations
**Requirement:** Calculate charging costs and metrics based on State of Charge and connector-level meter readings.

**Solution:** Enhanced session details API with:
1. SOC calculation from battery capacity
2. Real-time meter value integration
3. Comprehensive energy and cost breakdown

## How MeterValues Work

### OCPP Protocol Flow

```
Charge Point                 OCPP Server                Management API
     |                            |                            |
     |-- MeterValues.req -------->|                            |
     |   (Power: 12.5 kW)         |                            |
     |   (Energy: 15.234 kWh)     |                            |
     |   (SoC: 65%)               |                            |
     |                            |                            |
     |<-- MeterValues.conf -------|                            |
     |                            |                            |
     |                            |-- Update ConnectorStatus ->|
     |                            |   LastMeter = 15.234 kWh   |
     |                            |   LastMeterTime = Now      |
     |                            |                            |
     |-- (Every 60s) ------------->|                            |
     |                            |                            |
     |                            |                            |
     |                       [User stops charging]             |
     |                            |                            |
     |                            |<-- Get LastMeter ----------|
     |                            |--- Return 15.234 kWh ----->|
```

### MeterValues Message Content

From OCPP 1.6 specification:
```xml
<cs:meterValuesRequest>
  <cs:connectorId>1</cs:connectorId>
  <cs:transactionId>170</cs:transactionId>
  <cs:values>
    <cs:timestamp>2024-01-27T10:52:59.410Z</cs:timestamp>
    <cs:value cs:measurand="Current.Import" cs:unit="Amp">41.384</cs:value>
    <cs:value cs:measurand="Voltage" cs:unit="Volt">226.0</cs:value>
    <cs:value cs:measurand="Power.Active.Import" cs:unit="W">7018</cs:value>
    <cs:value cs:measurand="Energy.Active.Import.Register" cs:unit="Wh">15234</cs:value>
    <cs:value cs:measurand="SoC" cs:unit="Percent">65</cs:value>
    <cs:value cs:measurand="Temperature" cs:unit="Celsius">24</cs:value>
  </cs:values>
</cs:meterValuesRequest>
```

### Key Measurands

| Measurand | Description | Unit | Usage |
|-----------|-------------|------|-------|
| `Energy.Active.Import.Register` | Total energy delivered | Wh or kWh | Billing calculation |
| `Power.Active.Import` | Current charging power | W or kW | Speed monitoring |
| `Current.Import` | Current flow | A | Safety monitoring |
| `Voltage` | Supply voltage | V | Quality monitoring |
| `SoC` | State of Charge | % | Battery status |
| `Temperature` | Internal temp | °C | Safety monitoring |

## Data Sources Priority

When ending a charging session, the system uses this priority:

### Priority 1: OCPP Transaction (After Stop)
```csharp
var transaction = await _dbContext.Transactions
    .FirstOrDefaultAsync(t => t.TransactionId == session.TransactionId.Value);

startReading = transaction.MeterStart;  // From StartTransaction
endReading = transaction.MeterStop;      // From StopTransaction
```

**Most Authoritative** - Official OCPP transaction records

### Priority 2: Real-Time Connector Meter (Before Stop)
```csharp
var connectorStatus = await _dbContext.ConnectorStatuses
    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargePointId 
        && cs.ConnectorId == connectorId);

realtimeMeterReading = connectorStatus.LastMeter.Value;  // Latest MeterValue
```

**Real-Time Accuracy** - Last received meter value from charge point

### Priority 3: Session/Manual Readings (Fallback)
```csharp
if (double.TryParse(session.StartMeterReading, out double sessionStart))
{
    startReading = sessionStart;
}
if (double.TryParse(request.EndMeterReading, out double manualEnd))
{
    endReading = manualEnd;
}
```

**Fallback Only** - Stored or manually entered values

## Enhanced EndChargingSession Flow

### 1. Get Real-Time Connector Status
```csharp
// BEFORE calling StopTransaction
var connectorStatus = await _dbContext.ConnectorStatuses
    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargePointId 
        && cs.ConnectorId == connectorId && cs.Active == 1);

double realtimeMeterReading = connectorStatus.LastMeter ?? 0;
DateTime? realtimeMeterTime = connectorStatus.LastMeterTime;
```

### 2. Call OCPP StopTransaction
```csharp
var ocppResult = await CallOCPPStopTransaction(chargePointId, connectorId);
await Task.Delay(1000);  // Wait for transaction update
```

### 3. Get Transaction Meter Values
```csharp
var transaction = await _dbContext.Transactions
    .FirstOrDefaultAsync(t => t.TransactionId == session.TransactionId.Value);

startReading = transaction.MeterStart;
endReading = transaction.MeterStop ?? 0;
```

### 4. Fallback to Real-Time Meter
```csharp
if (endReading == 0 && realtimeMeterReading > 0)
{
    endReading = realtimeMeterReading;
    _logger.LogInformation($"Using real-time connector meter: {endReading:F3} kWh");
}
```

### 5. Calculate Energy and Cost
```csharp
double energyTransmitted = Math.Max(0, endReading - startReading);
double tariff = GetTariffFromChargingGun();
decimal totalCost = (decimal)(energyTransmitted * tariff);
```

### 6. Update ChargingGun Meter
```csharp
if (chargingGun != null)
{
    chargingGun.ChargerMeterReading = endReading.ToString("F3");
    chargingGun.UpdatedOn = DateTime.UtcNow;
}
```

## SOC Calculation

### When SOC Data is Available

If the vehicle's battery capacity is known:

```csharp
// Get battery capacity from UserVehicle → BatteryCapacityMaster
double batteryCapacity = 40.5; // kWh
double energyConsumed = 15.545; // kWh

// Calculate SOC change
double socChangePercentage = (energyConsumed / batteryCapacity) * 100;
// Result: 38.4%

// Estimate range added (4.5 km/kWh average)
double estimatedRange = energyConsumed * 4.5;
// Result: ~70 km
```

### Real-Time SOC from Charge Point

Some advanced charge points send SOC in MeterValues:

```csharp
// From MeterValues.req
sampleValue.Measurand == SampledValueMeasurand.SoC
double stateOfCharge = 65.0;  // %

// Stored in memory for active sessions
UpdateMemoryConnectorStatus(connectorId, meterKWH, meterTime, 
    currentChargeKW, stateOfCharge);
```

**Note:** Most charge points don't send SOC as they don't have direct access to vehicle BMS data.

## New API Endpoints

### 1. Get Real-Time Connector Meter Status

**GET** `/api/ChargingSession/connector-meter-status/{chargePointId}/{connectorId}`

**Response:**
```json
{
  "success": true,
  "message": "Connector meter status retrieved successfully",
  "data": {
    "chargePointId": "CP001",
    "connectorId": 1,
    "connectorName": "Connector 1",
    "status": "Charging",
    "statusTime": "2024-01-27T10:52:30Z",
    "meterValue": 15.234,
    "meterUnit": "kWh",
    "meterTime": "2024-01-27T10:52:29Z",
    "meterAge": "0 minutes ago",
    "hasActiveSession": true,
    "activeSession": {
      "sessionId": "abc-123",
      "startTime": "2024-01-27T09:45:00Z",
      "startMeter": "5.123",
      "energySinceStart": 10.111,
      "estimatedCost": 126.39,
      "duration": "1h 7m",
      "tariff": "12.50"
    },
    "recommendation": "Meter values are up-to-date"
  }
}
```

**Use Cases:**
- Monitor charging progress in real-time
- Verify charge point is sending meter values
- Troubleshoot stuck sessions
- Display live energy consumption to users

### 2. Enhanced Session Details (Already Updated)

**GET** `/api/ChargingSession/charging-session-details/{sessionId}`

Now includes:
- Real-time meter value source indication
- Connector meter usage tracking
- Data source transparency

## Troubleshooting

### Problem: Meter Values Not Increasing

**Symptoms:**
- `LastMeter` not updating
- `MeterAge` showing "Never updated"
- Meter values always 0

**Possible Causes:**

1. **Charge Point Not Sending MeterValues**
   ```bash
   # Check OCPP server logs
   grep "MeterValues" /var/log/ocpp/server.log
   
   # Should see periodic messages like:
   # [2024-01-27 10:52:29] MeterValues => Value: '15234.0' Wh
   ```

   **Solution:** Configure charge point to send MeterValues every 60 seconds
   ```json
   {
     "key": "MeterValueSampleInterval",
     "value": "60"  // seconds
   }
   ```

2. **Charge Point Not in Transaction**
   - MeterValues only sent during active transactions
   - Check if StartTransaction was successful

3. **Database Not Updating**
   - Check `UpdateConnectorStatus` is being called
   - Verify database connection

4. **Connector ID Mismatch**
   - Ensure connector IDs match between ChargingGuns and ConnectorStatus
   - Check ChargingSession.ChargingGunId is correct

### Problem: EndReading Same as StartReading

**Causes:**
- StopTransaction didn't update MeterStop
- No MeterValues received during charging
- Connector meter not captured

**Solution:**
```csharp
// Check DataSource in response
{
  "dataSource": {
    "transactionUsed": false,        // ❌ Transaction MeterStop not available
    "connectorMeterUsed": true,      // ✅ Using real-time connector meter
    "connectorMeterValue": "15.234 kWh",
    "connectorMeterTime": "2024-01-27 10:52:29"
  }
}
```

If `connectorMeterUsed` is false:
1. Check if charge point is sending MeterValues
2. Verify connector is active (`Active = 1`)
3. Check charge point connection

### Problem: SOC Calculation Not Available

**Causes:**
- No vehicle linked to user
- No battery capacity configured
- Battery capacity data missing

**Solution:**
1. **Ensure User Has Vehicle:**
   ```sql
   SELECT * FROM UserVehicles 
   WHERE UserId = 'user-id' AND DefaultConfig = 1 AND Active = 1;
   ```

2. **Check Battery Capacity Link:**
   ```sql
   SELECT uv.*, bc.BatteryCapcacity, bc.BatteryCapcacityUnit
   FROM UserVehicles uv
   LEFT JOIN BatteryCapacityMasters bc ON uv.BatteryCapacityId = bc.RecId
   WHERE uv.UserId = 'user-id';
   ```

3. **Add Battery Capacity:**
   ```http
   PUT /api/User/user-vehicle-update
   {
     "recId": "vehicle-id",
     "batteryCapacityId": "40kwh-capacity-id"
   }
   ```

## Configuration Checklist

### Charge Point Configuration

Configure using `ChangeConfiguration.req`:

```json
[
  {
    "key": "MeterValueSampleInterval",
    "value": "60"
  },
  {
    "key": "MeterValuesSampledData",
    "value": "Energy.Active.Import.Register,Power.Active.Import,SoC"
  },
  {
    "key": "StopTxnSampledData",
    "value": "Energy.Active.Import.Register"
  },
  {
    "key": "MeterValuesAlignedData",
    "value": "Energy.Active.Import.Register"
  }
]
```

### Database Integrity

Ensure proper relationships:
```sql
-- Verify connector status exists
SELECT COUNT(*) FROM ConnectorStatuses 
WHERE ChargePointId = 'CP001' AND Active = 1;

-- Verify charging gun links to connector
SELECT cg.*, cs.LastMeter, cs.LastMeterTime
FROM ChargingGuns cg
LEFT JOIN ChargingStations st ON cg.ChargingStationId = st.RecId
LEFT JOIN ConnectorStatuses cs ON cs.ChargePointId = st.ChargingPointId 
  AND cs.ConnectorId = CAST(cg.ConnectorId AS INT)
WHERE cg.Active = 1;
```

## Best Practices

### 1. Always Use Real-Time Meter First
```csharp
// Good ✅
var connector = await GetConnectorStatus(chargePointId, connectorId);
double endMeter = connector.LastMeter ?? transaction.MeterStop ?? manual;

// Bad ❌
double endMeter = transaction.MeterStop ?? 0;
```

### 2. Log Data Sources
```csharp
_logger.LogInformation(
    "Meter readings: Start={0:F3}, End={1:F3}, Source={2}",
    startReading, 
    endReading,
    source // "Transaction", "Connector", "Manual"
);
```

### 3. Validate Meter Readings
```csharp
if (endReading < startReading)
{
    _logger.LogError(
        "Invalid meter: End ({0}) < Start ({1})",
        endReading, startReading
    );
    endReading = startReading; // Prevent negative energy
}
```

### 4. Check Meter Age
```csharp
if (connectorStatus.LastMeterTime.HasValue)
{
    var age = DateTime.UtcNow - connectorStatus.LastMeterTime.Value;
    if (age.TotalMinutes > 5)
    {
        _logger.LogWarning(
            "Stale meter value: {0} minutes old",
            age.TotalMinutes
        );
    }
}
```

### 5. Provide User Feedback
```json
{
  "dataSource": {
    "description": "Using real-time meter from charge point",
    "reliability": "High",
    "lastUpdate": "23 seconds ago"
  }
}
```

## Testing

### 1. Test Real-Time Meter Updates

```bash
# Start charging
POST /api/ChargingSession/start-charging-session

# Wait 60 seconds for MeterValues

# Check connector status
GET /api/ChargingSession/connector-meter-status/CP001/1

# Should show increasing meter values
# Wait another 60 seconds and check again
```

### 2. Test Session End with Different Sources

**Test A: Normal Flow (All Data Available)**
```bash
# Start charging
# Wait 5 minutes (multiple MeterValues)
# Stop charging
POST /api/ChargingSession/end-charging-session

# Check dataSource in response
# Should use: Transaction MeterStop
```

**Test B: No Transaction Stop**
```bash
# Start charging
# Simulate connection loss during stop
# Stop charging
POST /api/ChargingSession/end-charging-session

# Should fallback to: Connector LastMeter
```

**Test C: No Meter Values**
```bash
# Charge point not sending MeterValues
# Stop charging with manual reading
POST /api/ChargingSession/end-charging-session
{
  "sessionId": "xxx",
  "endMeterReading": "25.5"
}

# Should use: Manual reading
```

### 3. Test SOC Calculation

```bash
# Ensure user has vehicle with battery capacity
# Start charging
# Charge for known duration
# Stop charging
# Verify SOC% matches expected

# Expected SOC = (Energy / BatteryCapacity) × 100
```

## Performance Considerations

### Database Queries
- `ConnectorStatus` lookup is indexed on `(ChargePointId, ConnectorId)`
- Use `FirstOrDefaultAsync` for single record queries
- Consider caching for high-frequency reads

### Real-Time Updates
- MeterValues update every 60 seconds (configurable)
- `LastMeter` and `LastMeterTime` updated atomically
- No transaction overhead for meter updates

### Logging
- Debug level: All meter value details
- Info level: Meter source and final values
- Warning level: Stale meters, missing data
- Error level: Invalid values, calculation failures

## Summary

### What's Fixed
✅ Real-time meter values now used when ending sessions
✅ Multiple data sources with priority fallback
✅ Transparent data source indication
✅ ChargingGun meter reading updated after each session
✅ SOC calculation when battery capacity available
✅ New API to check connector meter status

### What to Monitor
🔍 Charge points sending MeterValues every 60s
🔍 Connector LastMeter updating during charging
🔍 Transaction MeterStop populated on stop
🔍 SOC calculations when battery capacity known

### Next Steps
1. Configure charge points to send MeterValues
2. Test with real charge sessions
3. Monitor meter value age
4. Add user vehicle battery capacities
5. Implement dashboard showing real-time metrics
