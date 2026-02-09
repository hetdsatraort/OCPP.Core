# Charging Session Limits System

## Overview
The Charging Session Limits system allows users to set constraints on their charging sessions to automatically stop when certain thresholds are reached. This helps users control their spending, charging time, and energy consumption.

## Features

### Supported Limits
The system supports four types of limits that can be set when starting a charging session:

1. **Energy Limit** (`EnergyLimit`)
   - Unit: kWh (kilowatt-hours)
   - Controls: Maximum energy consumption
   - Example: Set 50 kWh to stop after consuming 50 kWh

2. **Cost Limit** (`CostLimit`)
   - Unit: Currency (based on tariff)
   - Controls: Maximum charging cost
   - Example: Set 25.00 to stop when cost reaches $25

3. **Time Limit** (`TimeLimit`)
   - Unit: Minutes
   - Controls: Maximum session duration
   - Example: Set 120 to stop after 2 hours

4. **Battery Increase Limit** (`BatteryIncreaseLimit`)
   - Unit: Percentage points (0-100)
   - Controls: Maximum battery charge increase
   - Example: Set 50 to stop after battery increases by 50%

### How It Works

1. **Session Start**: User optionally specifies limits when starting a charging session
2. **Periodic Monitoring**: System checks active sessions against their limits
3. **Automatic Stop**: When any limit is exceeded, the session is automatically stopped
4. **Tolerance**: System allows slight overages as mentioned by user requirements

## API Endpoints

### 1. Start Charging Session (Enhanced)
**Endpoint**: `POST /api/ChargingSession/start-charging-session`

**Request Payload**:
```json
{
  "chargingGunId": "1",
  "chargingStationId": "station-123",
  "userId": "user-456",
  "chargeTagId": "tag-789",
  "connectorId": 1,
  "startMeterReading": "100.5",
  "chargingTariff": "0.30",
  
  // Optional Limits
  "energyLimit": 50.0,           // Stop at 50 kWh
  "costLimit": 15.0,             // Stop at $15
  "timeLimit": 120,              // Stop at 120 minutes (2 hours)
  "batteryIncreaseLimit": 60.0   // Stop when battery increases by 60%
}
```

**Response**:
```json
{
  "success": true,
  "message": "Charging session started successfully",
  "data": {
    "session": {
      "recId": "session-guid",
      "energyLimit": 50.0,
      "costLimit": 15.0,
      "timeLimit": 120,
      "batteryIncreaseLimit": 60.0
      // ... other session fields
    }
  }
}
```

### 2. Check All Session Limits (Periodic Endpoint)
**Endpoint**: `GET /api/ChargingSession/check-session-limits`

**Purpose**: Designed to be called periodically (e.g., every 1-5 minutes) to monitor active sessions

**Response**:
```json
{
  "success": true,
  "message": "Checked 5 active sessions. Found 2 with limit violations. Auto-stopped 2 sessions.",
  "data": {
    "totalActiveSessions": 5,
    "violatedSessions": [
      {
        "sessionId": "session-guid-1",
        "chargingStationId": "station-123",
        "userId": "user-456",
        "hasViolations": true,
        "violatedLimits": [
          "Energy: 50.25 kWh >= 50.00 kWh limit",
          "Time: 125 min >= 120 min limit"
        ],
        "limitStatus": {
          "energyConsumed": 50.25,
          "energyLimit": 50.0,
          "energyPercentage": 100.5,
          "currentCost": 15.08,
          "costLimit": 15.0,
          "costPercentage": 100.5,
          "elapsedMinutes": 125,
          "timeLimit": 120,
          "timePercentage": 104.2,
          "batteryIncrease": 58.5,
          "batteryIncreaseLimit": 60.0,
          "batteryPercentage": 97.5
        }
      }
    ],
    "autoStoppedSessionIds": ["session-guid-1"],
    "checkedAt": "2026-02-09T10:30:00Z"
  }
}
```

### 3. Get Session Limit Status
**Endpoint**: `GET /api/ChargingSession/session-limit-status/{sessionId}`

**Purpose**: Check limit status of a specific session (useful for UI progress bars)

**Response**:
```json
{
  "success": true,
  "message": "Session is within all configured limits",
  "data": {
    "sessionId": "session-guid",
    "chargingStationId": "station-123",
    "userId": "user-456",
    "hasViolations": false,
    "violatedLimits": [],
    "limitStatus": {
      "energyConsumed": 35.5,
      "energyLimit": 50.0,
      "energyPercentage": 71.0,
      "currentCost": 10.65,
      "costLimit": 15.0,
      "costPercentage": 71.0,
      "elapsedMinutes": 85,
      "timeLimit": 120,
      "timePercentage": 70.8,
      "batteryIncrease": 42.3,
      "batteryIncreaseLimit": 60.0,
      "batteryPercentage": 70.5
    }
  }
}
```

## Database Schema Changes

### ChargingSession Table
Added the following columns:

```sql
ALTER TABLE ChargingSessions ADD EnergyLimit FLOAT NULL;
ALTER TABLE ChargingSessions ADD CostLimit FLOAT NULL;
ALTER TABLE ChargingSessions ADD TimeLimit INT NULL;
ALTER TABLE ChargingSessions ADD BatteryIncreaseLimit FLOAT NULL;
```

## Implementation Details

### Limit Checking Logic

The `CheckSessionLimitViolations` helper method:

1. **Retrieves Real-time Data**:
   - Current meter reading from connector status
   - Current State of Charge (SoC) from OCPP server cache
   - Session start values from database

2. **Calculates Current Metrics**:
   - Energy consumed = Current meter - Start meter
   - Cost = Energy consumed × Tariff
   - Time elapsed = Current time - Start time
   - Battery increase = Current SoC - Start SoC

3. **Compares Against Limits**:
   - Checks each configured limit
   - Calculates percentage of limit reached
   - Flags violations when actual >= limit

4. **Returns Detailed Status**:
   - Current values for all metrics
   - Configured limits
   - Percentage completion
   - List of violated limits

### Automatic Session Stop

When a violation is detected:

1. Calls OCPP `StopTransaction` command
2. Updates session record:
   - Sets `EndTime` to current time
   - Sets `Active` to 0
   - Captures final meter reading
   - Calculates final energy and cost
3. Logs the auto-stop action
4. Returns session ID in auto-stopped list

## Usage Scenarios

### Scenario 1: Budget-Conscious User
```json
{
  "costLimit": 10.0,
  "timeLimit": 180
}
```
User wants to spend maximum $10 and no more than 3 hours charging.

### Scenario 2: Quick Top-Up
```json
{
  "batteryIncreaseLimit": 30.0,
  "timeLimit": 45
}
```
User needs a quick 30% charge in under 45 minutes.

### Scenario 3: Full Charge with Budget
```json
{
  "batteryIncreaseLimit": 80.0,
  "costLimit": 25.0
}
```
User wants 80% charge but won't spend more than $25.

### Scenario 4: Energy-Based Charging
```json
{
  "energyLimit": 40.0
}
```
User knows their battery capacity and wants exactly 40 kWh.

## Integration Guide

### For Frontend Applications

1. **Starting a Session with Limits**:
```typescript
const startChargingWithLimits = async (sessionData) => {
  const response = await fetch('/api/ChargingSession/start-charging-session', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify({
      ...sessionData,
      energyLimit: 50.0,
      costLimit: 15.0,
      timeLimit: 120,
      batteryIncreaseLimit: 60.0
    })
  });
  return response.json();
};
```

2. **Displaying Progress**:
```typescript
const getSessionProgress = async (sessionId) => {
  const response = await fetch(`/api/ChargingSession/session-limit-status/${sessionId}`, {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  const data = await response.json();
  
  // Use percentages to show progress bars
  const { limitStatus } = data.data;
  updateProgressBar('energy', limitStatus.energyPercentage);
  updateProgressBar('cost', limitStatus.costPercentage);
  updateProgressBar('time', limitStatus.timePercentage);
  updateProgressBar('battery', limitStatus.batteryPercentage);
};
```

3. **Periodic Monitoring (Admin Panel)**:
```typescript
// Call every 2-5 minutes
const monitorSessionLimits = async () => {
  const response = await fetch('/api/ChargingSession/check-session-limits', {
    headers: {
      'Authorization': `Bearer ${token}`
    }
  });
  const data = await response.json();
  
  console.log(`Auto-stopped ${data.data.autoStoppedSessionIds.length} sessions`);
  
  // Notify users whose sessions were stopped
  data.data.autoStoppedSessionIds.forEach(sessionId => {
    notifyUser(sessionId, 'Session limit reached and stopped automatically');
  });
};

setInterval(monitorSessionLimits, 120000); // Every 2 minutes
```

### For Background Services

Create a scheduled task (Windows Task Scheduler, cron, or hosted service in ASP.NET):

```csharp
public class SessionLimitMonitorService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SessionLimitMonitorService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync(
                    "http://localhost:5000/api/ChargingSession/check-session-limits",
                    stoppingToken
                );
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation($"Limit check completed: {result}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session limit check");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}
```

Register in `Program.cs` or `Startup.cs`:
```csharp
services.AddHostedService<SessionLimitMonitorService>();
```

## Best Practices

1. **Limit Selection**:
   - Don't set all limits at once unless necessary
   - Choose limits that make sense for the use case
   - Energy and battery limits are most accurate

2. **Monitoring Frequency**:
   - Check every 1-5 minutes for active sessions
   - More frequent checks = faster limit enforcement
   - Less frequent checks = lower server load

3. **User Communication**:
   - Notify users when approaching limits (e.g., at 90%)
   - Send notification when session auto-stops
   - Display real-time progress in the app

4. **Tolerance Handling**:
   - System allows slight overages as designed
   - Typical overage: 1-5% depending on check frequency
   - OCPP stop command takes a few seconds to execute

5. **Error Handling**:
   - System logs but doesn't fail if auto-stop fails
   - Manual intervention may be needed in rare cases
   - Monitor the logs for failed auto-stop attempts

## Troubleshooting

### Sessions Not Auto-Stopping

**Possible Causes**:
- Periodic endpoint not being called
- OCPP communication issues
- Connector status not updating

**Solutions**:
- Verify background service is running
- Check OCPP server connectivity
- Review logs for errors

### Inaccurate Limit Calculations

**Possible Causes**:
- Stale connector status
- Missing SoC data
- Incorrect tariff values

**Solutions**:
- Verify connector status updates regularly
- Check OCPP server SoC caching
- Validate tariff configuration

### Limits Not Being Saved

**Possible Causes**:
- Database schema not updated
- Validation errors
- Null reference issues

**Solutions**:
- Run database migration
- Check request payload format
- Review server logs

## Future Enhancements

Potential improvements to consider:

1. **Warning Notifications**: Alert users at 75%, 90%, 95% of limit
2. **Limit Profiles**: Pre-configured limit sets (Budget, Quick Charge, Full Charge)
3. **Dynamic Limits**: Adjust limits based on remaining battery capacity
4. **Scheduling**: Set different limits for off-peak vs. peak hours
5. **Multi-Limit Strategies**: Choose which limit triggers first
6. **Limit History**: Track how often users hit limits
7. **Smart Recommendations**: Suggest optimal limits based on vehicle and usage patterns

## Technical Notes

- All limits are optional; sessions work normally without them
- Limits are evaluated using `>=` comparison (not strict `>`)
- Percentage calculations are rounded to 1 decimal place
- Energy and cost use 2 decimal places for accuracy
- Battery increase uses 1 decimal place (sufficient for percentage)
- Time is calculated in whole minutes
- System gracefully handles missing SoC data
- Real-time connector meter preferred over stored values
- Logs provide detailed audit trail of limit violations

## Support

For issues or questions:
1. Check application logs for detailed error messages
2. Verify database schema updates were applied
3. Test with single limit first, then combine
4. Monitor OCPP communication for connectivity issues
