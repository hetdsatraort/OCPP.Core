# Quick Implementation Guide - Unlock Connector Enhancement

## 🎯 Problem
Charging guns get stuck in "in use" state, preventing new sessions with error: "Charging gun is already in use"

## ✅ Solution
Enhance `UnlockConnector` API to automatically detect and clean up stuck sessions.

## 📝 Implementation Steps

### Step 1: Locate the Method
Open: `OCPP.Core.Management\Controllers\ChargingSessionController.cs`

Find the `UnlockConnector` method (around line 680-750)

### Step 2: Replace the Code

Find this section (after charge point verification):
```csharp
// Call OCPP server to unlock connector
var ocppResult = await CallOCPPUnlockConnector(
    chargePoint.ChargePointId,
    request.ConnectorId);
```

**Insert BEFORE the OCPP call:**
```csharp
// Check for stuck/active sessions on this connector
var stuckSessions = await _dbContext.ChargingSessions
    .Where(s => s.ChargingGunId == request.ConnectorId.ToString() &&
               s.ChargingStationID == request.ChargingStationId &&
               s.Active == 1 && 
               s.EndTime == DateTime.MinValue)
    .ToListAsync();

var cleanedSessions = new System.Collections.Generic.List<string>();
var warnings = new System.Collections.Generic.List<string>();

// Clean up stuck sessions before unlocking
if (stuckSessions.Any())
{
    _logger.LogWarning($"Found {stuckSessions.Count} stuck session(s) for connector {request.ConnectorId} at station {request.ChargingStationId}");

    foreach (var stuckSession in stuckSessions)
    {
        try
        {
            // Mark session as force-ended
            stuckSession.EndTime = DateTime.UtcNow;
            stuckSession.UpdatedOn = DateTime.UtcNow;

            // Try to get final meter reading from transaction if available
            if (stuckSession.TransactionId.HasValue)
            {
                var transaction = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == stuckSession.TransactionId.Value);

                if (transaction != null)
                {
                    double meterStop = transaction.MeterStop ?? transaction.MeterStart;
                    stuckSession.EndMeterReading = meterStop.ToString("F2");
                    
                    double energyConsumed = Math.Max(0, meterStop - transaction.MeterStart);
                    stuckSession.EnergyTransmitted = energyConsumed.ToString("F2");

                    if (double.TryParse(stuckSession.ChargingTariff, out double tariff))
                    {
                        decimal fee = (decimal)(energyConsumed * tariff);
                        stuckSession.ChargingTotalFee = fee.ToString("F2");
                    }
                }
            }

            cleanedSessions.Add(stuckSession.RecId);
            _logger.LogInformation($"Force-ended stuck session: {stuckSession.RecId}");
            
            warnings.Add($"Session {stuckSession.RecId} was automatically ended");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up stuck session {stuckSession.RecId}");
            warnings.Add($"Failed to clean session {stuckSession.RecId}: {ex.Message}");
        }
    }

    await _dbContext.SaveChangesAsync();
}
```

### Step 3: Update Error Response

Change the OCPP error response to include cleanup info:
```csharp
if (!ocppResult.Success)
{
    return BadRequest(new ChargingSessionResponseDto  // Changed from Ok to BadRequest
    {
        Success = false,
        Message = ocppResult.Message,
        Data = new
        {
            CleanedSessions = cleanedSessions,
            Warnings = warnings
        }
    });
}
```

### Step 4: Update Success Response

Replace the success return statement:
```csharp
_logger.LogInformation($"Connector unlocked: Station {request.ChargingStationId}, Connector {request.ConnectorId}. Cleaned {cleanedSessions.Count} session(s)");

var responseMessage = ocppResult.Message;
if (cleanedSessions.Any())
{
    responseMessage += $". {cleanedSessions.Count} stuck session(s) automatically cleared.";
}

return Ok(new ChargingSessionResponseDto
{
    Success = true,
    Message = responseMessage,
    Data = new
    {
        ChargingStationId = request.ChargingStationId,
        ConnectorId = request.ConnectorId,
        ChargePointId = chargePoint.ChargePointId,
        Status = "Unlocked",
        CleanedSessions = cleanedSessions,
        SessionsCleared = cleanedSessions.Count,
        Warnings = warnings
    }
});
```

### Step 5: Update Error Handler

Change the catch block status code:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error unlocking connector");
    return StatusCode(500, new ChargingSessionResponseDto  // Changed from Ok to StatusCode
    {
        Success = false,
        Message = "An error occurred while unlocking the connector"
    });
}
```

## 🧪 Quick Test

### Create Stuck Session:
```bash
# 1. Start a session
curl -X POST "http://localhost:5001/api/ChargingSession/start-charging-session" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "chargingStationId": "STATION_ID",
    "connectorId": 1,
    "chargeTagId": "TAG001",
    "chargingTariff": "0.25"
  }'

# 2. Don't end the session (simulate stuck state)

# 3. Try starting another session - should fail
# "Charging gun is already in use"

# 4. Unlock connector
curl -X POST "http://localhost:5001/api/ChargingSession/unlock-connector" \
  -H "Authorization: Bearer TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "chargingStationId": "STATION_ID",
    "connectorId": 1
  }'

# Expected response:
# {
#   "success": true,
#   "message": "Connector unlocked successfully. 1 stuck session(s) automatically cleared.",
#   "data": {
#     "sessionsCleared": 1,
#     "cleanedSessions": ["session-id"]
#   }
# }

# 5. Try starting session again - should work now!
```

## ✨ What This Fixes

**Before:**
- Gun stuck → Users can't charge → Support ticket → Manual DB fix → Hours of downtime

**After:**
- Gun stuck → Admin clicks unlock → Auto-cleanup → Gun available → Minutes of downtime

## 📊 Response Changes

### Before
```json
{
  "success": true,
  "message": "Connector unlocked successfully",
  "data": {
    "chargingStationId": "...",
    "connectorId": 1,
    "status": "Unlocked"
  }
}
```

### After (with stuck sessions)
```json
{
  "success": true,
  "message": "Connector unlocked successfully. 2 stuck session(s) automatically cleared.",
  "data": {
    "chargingStationId": "...",
    "connectorId": 1,
    "status": "Unlocked",
    "cleanedSessions": ["session-1", "session-2"],
    "sessionsCleared": 2,
    "warnings": [
      "Session session-1 was automatically ended",
      "Session session-2 was automatically ended"
    ]
  }
}
```

## 🔍 What Gets Cleaned

For each stuck session:
- ✅ Sets `EndTime` to current time
- ✅ Updates `UpdatedOn` timestamp
- ✅ Gets actual meter readings from OCPP transaction
- ✅ Calculates energy consumed
- ✅ Calculates final costs
- ✅ Saves all changes to database
- ✅ Logs the cleanup action

## 🚨 Important Notes

1. **Sessions cleaned BEFORE unlock attempt** - Even if OCPP unlock fails, database is cleaned
2. **Meter readings preserved** - Uses actual OCPP data for accurate billing
3. **Multiple sessions handled** - Cleans all stuck sessions for that connector
4. **Non-blocking** - Cleanup errors logged but don't prevent unlock
5. **Transparent** - Response shows exactly what was cleaned

## 📝 Logging

Watch for these log messages:
```
[WARN] Found 2 stuck session(s) for connector 1 at station ABC
[INFO] Force-ended stuck session: session-guid-123
[INFO] Connector unlocked: Station ABC, Connector 1. Cleaned 2 session(s)
[ERROR] Error cleaning up stuck session session-guid-456: ...
```

## ✅ Checklist

- [ ] Code added to UnlockConnector method
- [ ] Response updated to include cleanup info
- [ ] Error handling updated
- [ ] Tested with stuck session
- [ ] Tested with multiple stuck sessions
- [ ] Tested normal unlock (no stuck sessions)
- [ ] Verified logging works
- [ ] Verified meter readings preserved
- [ ] Verified costs calculated correctly
- [ ] Documentation updated

## 🎉 Benefits

**For Users:**
- Faster recovery from stuck states
- Better uptime
- No waiting for support

**For Admins:**
- One-click fix
- No manual database work
- Clear audit trail

**For System:**
- Automatic data cleanup
- Maintained integrity
- Comprehensive logging

---

**Estimated Implementation Time**: 15-20 minutes  
**Testing Time**: 10 minutes  
**Total**: ~30 minutes

See `UNLOCK_CONNECTOR_ENHANCEMENT.md` for complete documentation.
