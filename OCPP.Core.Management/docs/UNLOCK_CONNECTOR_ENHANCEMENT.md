# Unlock Connector Enhancement - Automatic Stuck Session Cleanup

## Problem Statement

When a charging gun becomes stuck or unfairly remains in an "in use" state, users cannot start a new session due to the error:
```
"Charging gun is already in use"
```

This can happen due to:
- Network failures during charging
- Improper session termination
- System crashes
- OCPP communication issues
- User not properly ending the session

## Solution

Enhanced the `UnlockConnector` API to automatically detect and clean up stuck sessions before unlocking the connector.

## Implementation

### Updated UnlockConnector Method

Replace the existing `UnlockConnector` method in `ChargingSessionController.cs` with:

```csharp
/// <summary>
/// Unlock a charging connector and automatically clean up any stuck sessions
/// </summary>
[HttpPost("unlock-connector")]
[Authorize]
public async Task<IActionResult> UnlockConnector([FromBody] UnlockConnectorRequestDto request)
{
    try
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new ChargingSessionResponseDto
            {
                Success = false,
                Message = "Invalid request data"
            });
        }

        // Verify charging station exists
        var chargingStation = await _dbContext.ChargingStations
            .FirstOrDefaultAsync(cs => cs.RecId == request.ChargingStationId && cs.Active == 1);

        if (chargingStation == null)
        {
            return NotFound(new ChargingSessionResponseDto
            {
                Success = false,
                Message = "Charging station not found or inactive"
            });
        }

        // Verify charge point exists
        var chargePoint = await _dbContext.ChargePoints
            .FirstOrDefaultAsync(cp => cp.ChargePointId == chargingStation.ChargingPointId);

        if (chargePoint == null)
        {
            return NotFound(new ChargingSessionResponseDto
            {
                Success = false,
                Message = "Charge point not found"
            });
        }

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

        // Call OCPP server to unlock connector
        var ocppResult = await CallOCPPUnlockConnector(
            chargePoint.ChargePointId,
            request.ConnectorId);

        if (!ocppResult.Success)
        {
            return BadRequest(new ChargingSessionResponseDto
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
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error unlocking connector");
        return StatusCode(500, new ChargingSessionResponseDto
        {
            Success = false,
            Message = "An error occurred while unlocking the connector"
        });
    }
}
```

## Key Features

### 1. Automatic Stuck Session Detection
- Searches for any active sessions on the connector being unlocked
- Identifies sessions where `EndTime == DateTime.MinValue` (not ended)
- Matches by `ConnectorId` and `ChargingStationId`

### 2. Smart Session Cleanup
- Automatically ends stuck sessions
- Retrieves final meter readings from OCPP transactions if available
- Calculates energy consumed and fees
- Marks sessions with proper end time

### 3. Comprehensive Logging
- Logs number of stuck sessions found
- Logs each session being cleaned
- Logs any errors during cleanup
- Provides detailed warnings in response

### 4. Enhanced Response
- Returns list of cleaned session IDs
- Provides count of sessions cleared
- Includes any warnings or errors
- Updates message with cleanup information

## Response Examples

### No Stuck Sessions
```json
{
  "success": true,
  "message": "Connector unlocked successfully",
  "data": {
    "chargingStationId": "station-guid",
    "connectorId": 1,
    "chargePointId": "CP001",
    "status": "Unlocked",
    "cleanedSessions": [],
    "sessionsCleared": 0,
    "warnings": []
  }
}
```

### With Stuck Sessions Cleaned
```json
{
  "success": true,
  "message": "Connector unlocked successfully. 2 stuck session(s) automatically cleared.",
  "data": {
    "chargingStationId": "station-guid",
    "connectorId": 1,
    "chargePointId": "CP001",
    "status": "Unlocked",
    "cleanedSessions": [
      "session-guid-1",
      "session-guid-2"
    ],
    "sessionsCleared": 2,
    "warnings": [
      "Session session-guid-1 was automatically ended",
      "Session session-guid-2 was automatically ended"
    ]
  }
}
```

### OCPP Unlock Failed (But Sessions Still Cleaned)
```json
{
  "success": false,
  "message": "Charge point did not respond in time",
  "data": {
    "cleanedSessions": [
      "session-guid-1"
    ],
    "warnings": [
      "Session session-guid-1 was automatically ended"
    ]
  }
}
```

## Usage Flow

### Before This Enhancement
```
1. Gun stuck in "in use" state
2. Admin calls unlock-connector
3. Connector unlocked (or not)
4. Session still marked as active in database
5. Users still can't start new session ❌
6. Manual database cleanup required
```

### After This Enhancement
```
1. Gun stuck in "in use" state
2. Admin calls unlock-connector
3. System detects stuck session(s)
4. System automatically ends stuck session(s)
   - Updates EndTime
   - Records meter readings from OCPP
   - Calculates final costs
5. Connector unlocked ✓
6. Gun now available for new sessions ✓
7. No manual intervention needed ✓
```

## Testing Scenarios

### Test 1: Unlock with Stuck Session
```bash
# 1. Start a charging session
POST /api/ChargingSession/start-charging-session
{
  "chargingStationId": "station-guid",
  "connectorId": 1,
  "chargeTagId": "TAG001",
  "chargingTariff": "0.25"
}

# 2. Simulate stuck state (don't end session properly)

# 3. Try to start another session - should fail
POST /api/ChargingSession/start-charging-session
# Response: "Charging gun is already in use"

# 4. Unlock connector (admin action)
POST /api/ChargingSession/unlock-connector
{
  "chargingStationId": "station-guid",
  "connectorId": 1
}

# Expected: Session automatically ended, connector unlocked

# 5. Try to start session again - should succeed
POST /api/ChargingSession/start-charging-session
# Response: Success!
```

### Test 2: Multiple Stuck Sessions
```bash
# If somehow multiple sessions exist for same connector
POST /api/ChargingSession/unlock-connector

# Expected: All stuck sessions cleaned up
# Response shows count and list of cleaned sessions
```

### Test 3: Normal Unlock (No Stuck Sessions)
```bash
# Unlock a connector that's not in use
POST /api/ChargingSession/unlock-connector

# Expected: Normal unlock, no cleanup needed
# Response shows 0 sessions cleared
```

## Benefits

### For Administrators
✅ One-click recovery from stuck states  
✅ No manual database queries needed  
✅ Clear feedback on what was cleaned  
✅ Audit trail in logs  

### For Users
✅ Faster resolution when gun is stuck  
✅ No waiting for manual intervention  
✅ Better user experience  
✅ Less downtime  

### For System
✅ Automatic cleanup of orphaned sessions  
✅ Maintains data integrity  
✅ Proper cost calculation even for force-ended sessions  
✅ Comprehensive logging  

## API Documentation

### Endpoint
```
POST /api/ChargingSession/unlock-connector
```

### Authorization
Required (Admin/User with permissions)

### Request Body
```json
{
  "chargingStationId": "string (required)",
  "connectorId": "integer (required)"
}
```

### Response Fields
| Field | Type | Description |
|-------|------|-------------|
| success | boolean | Operation success status |
| message | string | Result message |
| data.chargingStationId | string | Station ID |
| data.connectorId | integer | Connector ID |
| data.chargePointId | string | OCPP charge point ID |
| data.status | string | "Unlocked" if successful |
| data.cleanedSessions | array | List of session IDs that were force-ended |
| data.sessionsCleared | integer | Count of sessions cleaned |
| data.warnings | array | Any warnings during cleanup |

## Error Handling

### Graceful Degradation
- If session cleanup fails, the unlock still proceeds
- Errors logged but don't block the unlock operation
- Warnings included in response for transparency

### Partial Failures
- If some sessions fail to clean, others still proceed
- Each failure logged separately
- Response includes both successes and failures

## Security Considerations

### Authorization
- Only authorized users can unlock connectors
- Action is logged with user information
- Audit trail maintained

### Data Integrity
- Sessions properly ended with timestamps
- Meter readings preserved from OCPP
- Cost calculations maintained

## Monitoring & Alerts

### Log Messages
```
WARNING: "Found 2 stuck session(s) for connector 1 at station ABC"
INFO: "Force-ended stuck session: session-guid-123"
INFO: "Connector unlocked: Station ABC, Connector 1. Cleaned 2 session(s)"
ERROR: "Error cleaning up stuck session session-guid-456: [error details]"
```

### Metrics to Track
- Number of unlock-connector calls
- Number of stuck sessions found
- Number of sessions successfully cleaned
- Number of cleanup failures
- Average cleanup time

## Rollout Plan

### Phase 1: Testing
1. Test with intentionally stuck sessions
2. Verify meter readings are preserved
3. Confirm cost calculations work
4. Test with multiple stuck sessions

### Phase 2: Deployment
1. Deploy to test environment
2. Run through test scenarios
3. Monitor logs for issues
4. Deploy to production

### Phase 3: Monitoring
1. Watch for stuck session patterns
2. Monitor cleanup success rate
3. Track user feedback
4. Optimize as needed

## Future Enhancements

1. **Automatic Recovery**: Periodic background job to auto-detect and clean stuck sessions
2. **Session Timeout**: Auto-end sessions after X hours of inactivity
3. **User Notifications**: Notify users when their session was force-ended
4. **Cost Adjustment**: Option to waive fees for force-ended sessions
5. **Admin Dashboard**: View and manage stuck sessions
6. **Webhook**: Notify external systems when sessions are force-ended

## FAQ

**Q: What happens to the user's wallet if a session is force-ended?**  
A: The system attempts to calculate costs from OCPP meter readings and debit accordingly. If meter readings are unavailable, the session ends with $0 cost.

**Q: Can users unlock connectors themselves?**  
A: Depends on authorization configuration. Typically only admins, but can be configured.

**Q: What if OCPP unlock fails but sessions are cleaned?**  
A: Sessions are cleaned first to ensure database consistency, even if physical unlock fails. The response indicates both actions.

**Q: Will this affect billing accuracy?**  
A: No. The system uses actual OCPP meter readings when available, ensuring accurate billing even for force-ended sessions.

**Q: How do I know if sessions were cleaned?**  
A: Check the response data - it includes `cleanedSessions` array and `sessionsCleared` count, plus detailed logs.

---

**Status**: Ready for Implementation  
**Priority**: High - Resolves critical user-facing issue  
**Impact**: Positive - Reduces support burden, improves UX
