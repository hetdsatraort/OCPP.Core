# Quick Testing Guide - Real-Time Energy & Cost Fix

## What Was Fixed
Active charging sessions now display real-time energy consumption and estimated costs instead of showing zero values.

## API Endpoint
```
GET /api/ChargingSession/charging-session-details/{sessionId}
```

## Quick Test Steps

### 1. Start a Charging Session
```bash
POST /api/ChargingSession/start-charging-session
{
  "userId": "your-user-id",
  "chargingStationId": "station-id",
  "chargingGunId": "gun-id",
  "connectorId": 1,
  "chargeTagId": "tag-id"
}
```

### 2. Wait for Some Energy Consumption
- Let the session run for a few minutes
- The OCPP charger should send meter values
- Check that ConnectorStatus table is being updated

### 3. Check Session Details (BEFORE FIX - Would Show Zero)
```bash
GET /api/ChargingSession/charging-session-details/{sessionId}
```

**Expected Response (BEFORE FIX):**
```json
{
  "energyConsumption": {
    "totalEnergy": 0.000  // ❌ WRONG - Should show actual consumption
  },
  "costDetails": {
    "totalCost": 0.00  // ❌ WRONG - Should show estimated cost
  }
}
```

### 4. Check Session Details (AFTER FIX - Should Show Real Values)
```bash
GET /api/ChargingSession/charging-session-details/{sessionId}
```

**Expected Response (AFTER FIX):**
```json
{
  "meterReadings": {
    "startReading": 12.345,
    "currentReading": 27.891,  // ✅ Real-time value from connector
    "unit": "kWh",
    "dataSource": "Real-time Connector (Live)",  // ✅ Shows it's live data
    "isRealtime": true  // ✅ Indicates real-time updates
  },
  "energyConsumption": {
    "totalEnergy": 15.546,  // ✅ Calculated: 27.891 - 12.345
    "unit": "kWh"
  },
  "costDetails": {
    "energyCost": 194.33,  // ✅ Calculated: 15.546 × 12.50
    "taxes": 34.98,         // ✅ 18% GST
    "totalCost": 229.31,    // ✅ Energy + Tax
    "tariffApplied": 12.50
  },
  "status": "Charging",     // ✅ Shows active status
  "isActive": true
}
```

## What to Look For

### ✅ Success Indicators
1. **MeterReadings.CurrentReading** > MeterReadings.StartReading
2. **EnergyConsumption.TotalEnergy** > 0
3. **CostDetails.TotalCost** > 0
4. **MeterReadings.DataSource** = "Real-time Connector (Live)"
5. **MeterReadings.IsRealtime** = true
6. **Status** = "Charging"

### ❌ Failure Indicators
1. MeterReadings.CurrentReading == MeterReadings.StartReading (no change)
2. EnergyConsumption.TotalEnergy == 0
3. CostDetails.TotalCost == 0
4. MeterReadings.DataSource != "Real-time Connector (Live)"

## Troubleshooting

### If Still Showing Zero

#### Check 1: Verify ConnectorStatus Table
```sql
SELECT ChargePointId, ConnectorId, LastMeter, LastMeterTime, LastStatus
FROM ConnectorStatuses
WHERE ChargePointId = 'your-charge-point-id' 
  AND ConnectorId = your-connector-id
  AND Active = 1;
```
- **LastMeter** should have a value
- **LastMeterTime** should be recent (< 5 minutes ago)
- **LastStatus** should be "Charging" or similar

#### Check 2: Verify OCPP Communication
Look for these logs:
```
[INFO] Fetched real-time connector meter for active session {sessionId}: {value} kWh
[INFO] Using real-time connector meter for active session: {value} kWh
```

If you see:
```
[WARN] No real-time meter available for active session {sessionId}. Using MeterStart.
```
This means ConnectorStatus.LastMeter is NULL or not being updated.

#### Check 3: Verify Charging Station Configuration
```sql
SELECT cs.RecId, cs.ChargingPointId, cg.ConnectorId, cg.ChargerTariff
FROM ChargingStations cs
JOIN ChargingGuns cg ON cg.ChargingStationId = cs.RecId
WHERE cs.RecId = 'your-station-id';
```

#### Check 4: Verify OCPP Charger is Sending MeterValues
- Check your OCPP simulator or physical charger
- Ensure it's configured to send MeterValues periodically
- Typical interval: every 30-60 seconds

## Database Queries for Verification

### Get Active Session with Details
```sql
SELECT 
    cs.RecId AS SessionId,
    cs.StartTime,
    cs.EndTime,
    cs.StartMeterReading,
    cs.EndMeterReading,
    cs.TransactionId,
    t.MeterStart AS TransactionMeterStart,
    t.MeterStop AS TransactionMeterStop,
    conn.LastMeter AS ConnectorCurrentMeter,
    conn.LastMeterTime AS ConnectorLastUpdate
FROM ChargingSessions cs
LEFT JOIN Transactions t ON t.TransactionId = cs.TransactionId
LEFT JOIN ChargingStations station ON station.RecId = cs.ChargingStationID
LEFT JOIN ConnectorStatuses conn ON conn.ChargePointId = station.ChargingPointId 
    AND conn.ConnectorId = CAST(cs.ChargingGunId AS INT)
WHERE cs.EndTime = '0001-01-01 00:00:00'  -- Active sessions
  AND cs.Active = 1
ORDER BY cs.StartTime DESC;
```

### Expected Results:
- **TransactionMeterStart**: Should have a value (e.g., 12.345)
- **TransactionMeterStop**: NULL for active sessions
- **ConnectorCurrentMeter**: Should be > TransactionMeterStart
- **ConnectorLastUpdate**: Should be recent

## Sample Test Scenarios

### Scenario 1: Normal Active Session
```
1. Start session → Wait 5 minutes → Check details
2. Expected: Energy consumed ≈ 1-3 kWh (depends on charger)
3. Expected: Cost = Energy × Tariff
```

### Scenario 2: Long Running Session
```
1. Start session → Wait 30 minutes → Check details
2. Expected: Energy consumed ≈ 10-15 kWh (for 22kW charger)
3. Expected: Accurate cost calculation
```

### Scenario 3: Multiple Status Checks
```
1. Check at 5 minutes → Note energy/cost
2. Check at 10 minutes → Verify energy/cost increased
3. Check at 15 minutes → Verify continuous increase
```

### Scenario 4: End Session and Compare
```
1. Check active session details → Note estimated values
2. End the session
3. Check completed session details → Compare final values
4. Expected: Final values close to last estimated values
```

## API Response Fields to Monitor

### Key Fields for Real-Time Data:
```json
{
  "meterReadings": {
    "currentReading": number,    // Should increase over time
    "dataSource": string,        // Should be "Real-time Connector (Live)"
    "isRealtime": boolean        // Should be true
  },
  "energyConsumption": {
    "totalEnergy": number        // Should increase over time
  },
  "costDetails": {
    "totalCost": number,         // Should increase over time
    "tariffApplied": number      // Should match charging gun tariff
  },
  "chargingPerformance": {
    "averageChargingSpeed": number  // Should be > 0 (in kW)
  },
  "timing": {
    "isActive": boolean,         // Should be true
    "lastUpdate": datetime       // Should be current time
  }
}
```

## Performance Considerations

The fix adds one additional database query:
```csharp
// Added query to fetch ConnectorStatus
var connectorStatus = await _dbContext.ConnectorStatuses
    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargingStation.ChargingPointId 
        && cs.ConnectorId == connectorId && cs.Active == 1);
```

**Impact:**
- ✅ Minimal - Only for active sessions
- ✅ Indexed query (ChargePointId + ConnectorId)
- ✅ Single row lookup
- ⏱️ Expected time: < 10ms

## Success Criteria

✅ **Test Passed If:**
1. Active sessions show non-zero energy consumption
2. Active sessions show calculated estimated cost
3. Real-time meter values are used (check DataSource field)
4. Completed sessions still work as before
5. No performance degradation
6. Proper logging appears in logs

## Next Steps After Testing

If tests pass:
1. ✅ Deploy to production
2. ✅ Monitor logs for real-time meter usage
3. ✅ Collect user feedback
4. ✅ Consider adding WebSocket for live updates

If tests fail:
1. Check OCPP charger configuration
2. Verify ConnectorStatus table is being updated
3. Review application logs
4. Check database connectivity
5. Refer to CHARGING_SESSION_REALTIME_FIX.md for detailed troubleshooting
