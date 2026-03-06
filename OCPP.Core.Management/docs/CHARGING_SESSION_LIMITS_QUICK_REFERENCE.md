# Charging Session Limits - Quick Reference

## 🎯 Quick Start

### Start Session with Limits
```bash
POST /api/ChargingSession/start-charging-session
```
```json
{
  "chargingGunId": "1",
  "chargingStationId": "station-123",
  "connectorId": 1,
  "energyLimit": 50.0,      // kWh
  "costLimit": 15.0,        // Currency
  "timeLimit": 120,         // Minutes
  "batteryIncreaseLimit": 60.0  // Percentage
}
```

### Monitor All Sessions
```bash
GET /api/ChargingSession/check-session-limits
```

### Check Specific Session
```bash
GET /api/ChargingSession/session-limit-status/{sessionId}
```

## 📊 Limit Types

| Limit Type | Field Name | Unit | Example |
|------------|------------|------|---------|
| Energy | `energyLimit` | kWh | 50.0 |
| Cost | `costLimit` | Currency | 15.00 |
| Time | `timeLimit` | Minutes | 120 |
| Battery | `batteryIncreaseLimit` | Percentage | 60.0 |

## 🔄 Implementation Workflow

```
1. User starts session with limits
         ↓
2. Session stored in database
         ↓
3. Periodic check runs (every 1-5 min)
         ↓
4. System calculates current values
         ↓
5. Compares against limits
         ↓
6. If limit exceeded → Auto-stop session
```

## 📁 Files Modified

| File | Changes |
|------|---------|
| `ChargingSession.cs` | Added 4 limit fields |
| `ChargingSessionDto.cs` | Added limit DTOs and response models |
| `ChargingSessionController.cs` | Added 2 endpoints + helper method |

## 🔨 Database Changes Required

```sql
ALTER TABLE ChargingSessions ADD EnergyLimit FLOAT NULL;
ALTER TABLE ChargingSessions ADD CostLimit FLOAT NULL;
ALTER TABLE ChargingSessions ADD TimeLimit INT NULL;
ALTER TABLE ChargingSessions ADD BatteryIncreaseLimit FLOAT NULL;
```

## 💻 Frontend Integration

### Display Progress
```typescript
const limitStatus = session.limitStatus;

// Energy: 35.5 / 50.0 kWh (71%)
const energyProgress = limitStatus.energyPercentage;

// Cost: $10.65 / $15.00 (71%)
const costProgress = limitStatus.costPercentage;

// Time: 85 / 120 min (70.8%)
const timeProgress = limitStatus.timePercentage;

// Battery: +42.3% / +60% (70.5%)
const batteryProgress = limitStatus.batteryPercentage;
```

### Periodic Poll
```typescript
// Poll every 30 seconds for active session
setInterval(async () => {
  const status = await fetch(`/api/ChargingSession/session-limit-status/${sessionId}`);
  const data = await status.json();
  updateUI(data.data.limitStatus);
}, 30000);
```

## ⚙️ Backend Integration

### Create Background Service
```csharp
// Add to Program.cs
builder.Services.AddHostedService<SessionLimitMonitorService>();

// SessionLimitMonitorService.cs
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await CheckLimits();
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
    }
}
```

### Manual Check
```csharp
// Call from admin panel or scheduled task
var response = await httpClient.GetAsync(
    "http://localhost:5000/api/ChargingSession/check-session-limits"
);
```

## 🎨 UI Components Needed

### Session Start Form
- [ ] 4 optional input fields for limits
- [ ] Input validation (positive numbers)
- [ ] Suggested presets (Budget, Quick, Full)

### Active Session Dashboard
- [ ] 4 progress bars (one per limit type)
- [ ] Color coding (green < 75%, yellow 75-90%, orange 90-100%, red >100%)
- [ ] Current value vs. limit display
- [ ] Warning when approaching limit (90%)

### Admin Monitor
- [ ] List of active sessions
- [ ] Highlight sessions near limits
- [ ] Auto-stopped session notifications
- [ ] Manual intervention options

## 📱 Example Use Cases

### Budget User
```json
{ "costLimit": 10.0, "timeLimit": 180 }
```
"Charge for max $10, no more than 3 hours"

### Quick Charge
```json
{ "batteryIncreaseLimit": 30.0, "timeLimit": 45 }
```
"Quick 30% top-up in 45 minutes"

### Overnight Charging
```json
{ "batteryIncreaseLimit": 80.0, "costLimit": 20.0 }
```
"Charge to 80% but don't exceed $20"

### Energy-Based
```json
{ "energyLimit": 40.0 }
```
"Need exactly 40 kWh"

## ⚠️ Important Notes

- **All limits are optional** - Session works normally without them
- **Slight overages are OK** - System allows 1-5% tolerance
- **Real-time data preferred** - Uses connector status for accuracy
- **Auto-stop is automatic** - No user interaction needed
- **Logs everything** - Check logs for audit trail

## 🔍 Response Structure

### Limit Status Response
```json
{
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
  },
  "hasViolations": false,
  "violatedLimits": []
}
```

## 🛠️ Testing Checklist

- [ ] Start session without limits (should work normally)
- [ ] Start session with one limit
- [ ] Start session with all limits
- [ ] Verify limits saved in database
- [ ] Call check-session-limits endpoint
- [ ] Call session-limit-status endpoint
- [ ] Verify progress percentages
- [ ] Test auto-stop when limit exceeded
- [ ] Verify session marked as complete
- [ ] Check final meter reading captured
- [ ] Review logs for violations
- [ ] Test with missing SoC data
- [ ] Test with stale connector data

## 🚀 Deployment Steps

1. **Database**
   ```bash
   # Run migration or execute SQL script
   dotnet ef migrations add AddSessionLimits
   dotnet ef database update
   ```

2. **Backend**
   ```bash
   # Build and deploy
   dotnet build
   dotnet publish -c Release
   ```

3. **Background Service**
   ```bash
   # Configure and start monitoring service
   # Adjust check interval in configuration
   ```

4. **Testing**
   ```bash
   # Test endpoints
   curl -X GET http://localhost:5000/api/ChargingSession/check-session-limits
   ```

## 📞 Troubleshooting

| Issue | Solution |
|-------|----------|
| Limits not saved | Check database schema updated |
| No auto-stop | Verify periodic check is running |
| Wrong percentages | Check real-time data availability |
| OCPP stop fails | Review OCPP server connectivity |

## 📚 Related Documentation

- [CHARGING_SESSION_LIMITS_README.md](./CHARGING_SESSION_LIMITS_README.md) - Full documentation
- [CHARGING_SESSION_API_README.md](./CHARGING_SESSION_API_README.md) - Base API docs
- [CHARGING_SESSION_OCPP_INTEGRATION_SUMMARY.md](./CHARGING_SESSION_OCPP_INTEGRATION_SUMMARY.md) - OCPP integration

---

**Version**: 1.0  
**Last Updated**: February 9, 2026  
**Feature**: Charging Session Limits System
