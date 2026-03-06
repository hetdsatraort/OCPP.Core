# Real-Time Meter & SOC - Quick Reference

## 🎯 What Changed

### Before ❌
- Only used Transaction.MeterStop (often not updated)
- No real-time meter value usage
- Fixed F2 precision (lost accuracy)
- No data source transparency

### After ✅
- Uses ConnectorStatus.LastMeter (real-time)
- Multi-source priority fallback
- F3 precision (Wh accuracy)
- Clear data source indication
- ChargingGun meter updated
- SOC calculation ready

## 📊 Data Priority

```
1️⃣ Transaction.MeterStop (after StopTransaction)
     ↓ not available
2️⃣ ConnectorStatus.LastMeter (real-time from MeterValues)
     ↓ not available
3️⃣ Manual/Session readings (fallback)
```

## 🔌 How MeterValues Work

```
Every 60 seconds during charging:

ChargePoint → MeterValues.req → OCPP Server
                                      ↓
                              Update ConnectorStatus
                              - LastMeter = 15.234 kWh
                              - LastMeterTime = Now
```

## 🚀 New API Endpoint

### Check Real-Time Meter Status

```http
GET /api/ChargingSession/connector-meter-status/{chargePointId}/{connectorId}
```

**Response:**
```json
{
  "meterValue": 15.234,
  "meterTime": "2024-01-27T10:52:29Z",
  "meterAge": "2 minutes ago",
  "activeSession": {
    "energySinceStart": 10.111,
    "estimatedCost": 126.39,
    "duration": "1h 7m"
  }
}
```

## 💰 Cost Calculation Flow

```
1. Get ChargingGun.ChargerTariff (₹12.50/kWh)
2. Get EndMeter from priority sources
3. Energy = EndMeter - StartMeter (15.234 - 5.123 = 10.111 kWh)
4. Cost = Energy × Tariff (10.111 × 12.50 = ₹126.39)
5. Tax = Cost × 0.18 (₹22.75)
6. Total = Cost + Tax (₹149.14)
```

## 🔋 SOC Calculation

When battery capacity available:

```
Energy Consumed: 15.545 kWh
Battery Capacity: 40 kWh

SOC Change = (15.545 / 40) × 100 = 38.9%
Range Added = 15.545 × 4.5 = ~70 km
```

## 🔧 Troubleshooting

### No Meter Values Increasing?

**Check:**
1. Charge point sending MeterValues every 60s
2. Connector Active = 1
3. OCPP server logs show MeterValues processing
4. Database ConnectorStatus.LastMeter updating

**Fix:**
```json
Configure charge point:
{
  "MeterValueSampleInterval": "60",
  "MeterValuesSampledData": "Energy.Active.Import.Register,Power.Active.Import"
}
```

### EndMeter = StartMeter?

**Check DataSource in response:**
```json
{
  "dataSource": {
    "transactionUsed": false,
    "connectorMeterUsed": false,  // ❌ Problem here
    "connectorMeterValue": "0 kWh"  // No meter values received
  }
}
```

**Solutions:**
1. Verify charge point is online
2. Check MeterValues in OCPP logs
3. Use connector-meter-status endpoint to monitor
4. Provide manual reading as fallback

### No SOC Calculation?

**Check:**
1. User has vehicle linked (UserVehicle)
2. Vehicle has BatteryCapacityId set
3. BatteryCapacityMaster has valid capacity

**Fix:**
```http
PUT /api/User/user-vehicle-update
{
  "recId": "vehicle-id",
  "batteryCapacityId": "capacity-id"
}
```

## 📝 Response Fields

### EndChargingSession Response

```json
{
  "success": true,
  "data": {
    "energyConsumed": 10.111,
    "cost": 126.39,
    "meterStart": 5.123,
    "meterStop": 15.234,
    
    "dataSource": {
      "transactionUsed": false,
      "connectorMeterUsed": true,  // ✅ Used real-time meter
      "connectorMeterValue": "15.234 kWh",
      "connectorMeterTime": "2024-01-27 10:52:29"
    }
  }
}
```

### ChargingSessionDetails Response

```json
{
  "energyConsumption": {
    "totalEnergy": 15.545,
    "unit": "kWh"
  },
  
  "stateOfCharge": {
    "socChangePercentage": 38.9,
    "estimatedRangeAdded": 70,
    "description": "Battery charged by 38.9% (~70 km range added)"
  },
  
  "chargingPerformance": {
    "averageChargingSpeed": 12.44,
    "chargingEfficiency": 56.5,
    "unit": "kW"
  }
}
```

## 🎯 Key Improvements

| Aspect | Before | After |
|--------|--------|-------|
| **Data Source** | Transaction only | Multi-source priority |
| **Real-time** | No | Yes (LastMeter) |
| **Precision** | 0.01 kWh (F2) | 0.001 kWh (F3) |
| **Transparency** | None | Data source indicated |
| **SOC** | Manual only | Calculated if capacity known |
| **Troubleshooting** | Difficult | Clear data source info |

## 📱 User-Facing Benefits

1. **More Accurate Billing** - F3 precision (Wh level)
2. **Real-Time Visibility** - See live consumption
3. **Transparent Costs** - Know where data comes from
4. **SOC Insights** - See battery % charged
5. **Range Estimation** - Know km added
6. **Trust** - Clear data sources build confidence

## 🔐 Configuration Checklist

- [ ] Charge points sending MeterValues every 60s
- [ ] ConnectorStatus.Active = 1 for all connectors
- [ ] ChargingGuns linked to correct ConnectorId
- [ ] Users have vehicles with battery capacities
- [ ] Tariffs configured in ChargingGuns
- [ ] OCPP server processing MeterValues correctly

## 📞 Support Info

**Check Logs:**
```bash
# OCPP Server
grep "MeterValues" /logs/ocpp-server.log

# Management API
grep "Using.*meter" /logs/management-api.log
```

**Database Queries:**
```sql
-- Check connector meter status
SELECT ChargePointId, ConnectorId, LastMeter, LastMeterTime 
FROM ConnectorStatuses WHERE Active = 1;

-- Check charging gun meters
SELECT RecId, ConnectorId, ChargerMeterReading, UpdatedOn 
FROM ChargingGuns WHERE Active = 1;
```

## 🚦 Status Indicators

### Meter Value Health
- ✅ **Good**: Updated < 2 minutes ago
- ⚠️ **Warning**: Updated 2-5 minutes ago
- ❌ **Stale**: Updated > 5 minutes ago
- ⚫ **Never**: No updates received

### Data Source Reliability
- 🥇 **Transaction MeterStop**: Most authoritative
- 🥈 **Connector LastMeter**: Real-time accurate
- 🥉 **Manual Reading**: Fallback only

---

**Last Updated:** 2024-01-27
**Version:** 1.0
