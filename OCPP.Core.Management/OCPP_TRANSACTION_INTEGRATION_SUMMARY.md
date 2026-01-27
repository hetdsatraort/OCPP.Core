# OCPP Transaction Integration with Charging Sessions - Implementation Summary

## ✅ Implementation Complete

The charging session APIs have been updated to properly integrate with the OCPP Transactions table, ensuring accurate meter readings, rates, and cost calculations directly from the OCPP protocol.

## 🔄 Key Changes

### 1. Database Schema Update

**ChargingSession Entity** - Added `TransactionId` field:
```csharp
public class ChargingSession
{
    public string RecId { get; set; }
    public string UserId { get; set; }
    public int? TransactionId { get; set; }  // ✨ NEW - Links to OCPP Transaction
    public string ChargingGunId { get; set; }
    public string ChargingStationID { get; set; }
    // ... other fields
}
```

### 2. Enhanced Start Charging Session

**What Changed:**
- Now captures the actual OCPP `TransactionId` after starting
- Uses actual meter readings from OCPP `Transactions` table
- Records real `MeterStart` value instead of manual input
- Links charging session to OCPP transaction for accurate tracking

**Response includes:**
```json
{
  "success": true,
  "data": {
    "session": { ... },
    "transactionId": 12345,
    "meterStart": 1250.50,
    "tariff": "0.25"
  }
}
```

### 3. Enhanced End Charging Session

**What Changed:**
- Retrieves actual meter readings from OCPP `Transactions` table
- Uses `transaction.MeterStart` and `transaction.MeterStop` for accuracy
- Calculates energy consumed from actual OCPP data
- Falls back to manual readings only if transaction data unavailable
- Provides detailed breakdown of cost calculation

**Response includes:**
```json
{
  "success": true,
  "message": "Charging session ended successfully. $12.50 debited.",
  "data": {
    "session": { ... },
    "transactionId": 12345,
    "energyConsumed": 50.00,
    "cost": 12.50,
    "meterStart": 1250.50,
    "meterStop": 1300.50,
    "duration": 60.5,
    "chargingSpeed": "49.50",
    "walletTransaction": { ... }
  }
}
```

### 4. Enhanced Session Details API

**Enhanced API:** `GET /api/ChargingSession/charging-session-details/{sessionId}`

Now provides comprehensive real-time data for both active and completed sessions:
```json
{
  "success": true,
  "data": {
    "session": { ... },
    "transactionId": 12345,
    "status": "Charging",
    "isActive": true,
    "meterStart": 1250.50,
    "meterCurrent": 1275.25,
    "energyConsumed": 24.75,
    "estimatedCost": 6.19,
    "chargingSpeed": 49.50,
    "tariff": "0.25",
    "duration": {
      "totalMinutes": 30,
      "hours": 0,
      "minutes": 30,
      "totalHours": 0.5
    },
    "startTime": "2024-01-22T12:00:00Z",
    "endTime": null,
    "lastUpdate": "2024-01-22T12:30:00Z"
  }
}
```

## 📊 Data Flow

### Start Session Flow
```
1. User initiates charging
   ↓
2. Call OCPP StartTransaction API
   ↓
3. OCPP server creates Transaction record
   - Records MeterStart from charge point
   - Assigns TransactionId
   ↓
4. Retrieve Transaction from database
   - Get actual TransactionId
   - Get actual MeterStart value
   - Get actual StartTime
   ↓
5. Create ChargingSession record
   - Link to Transaction via TransactionId
   - Store actual meter readings
   - Record tariff rate
   ↓
6. Return session details with transaction info
```

### End Session Flow
```
1. User/System ends charging
   ↓
2. Call OCPP StopTransaction API
   ↓
3. OCPP server updates Transaction record
   - Records MeterStop from charge point
   - Records StopTime
   ↓
4. Retrieve Transaction from database
   - Get actual MeterStop value
   - Get actual StopTime
   ↓
5. Calculate from actual OCPP data:
   - Energy = MeterStop - MeterStart
   - Cost = Energy × Tariff
   - Speed = Energy / Duration
   ↓
6. Update ChargingSession record
   - Store final meter readings
   - Store energy consumed
   - Store total cost
   ↓
7. Process wallet transaction
   - Check balance
   - Debit cost
   - Create transaction log
   ↓
8. Return complete session details with costs
```

## 🔍 Meter Reading Sources

### Priority Order:
1. **OCPP Transaction Table** (Primary, Most Accurate)
   - `transaction.MeterStart` - From charge point
   - `transaction.MeterStop` - From charge point
   - `transaction.StartTime` - Actual start
   - `transaction.StopTime` - Actual stop

2. **Manual Readings** (Fallback)
   - Used only if Transaction data unavailable
   - `request.StartMeterReading`
   - `request.EndMeterReading`

3. **Session Data** (Existing)
   - `session.StartMeterReading`
   - `session.EndMeterReading`

## 💰 Cost Calculation Formula

```
Energy Consumed (kWh) = MeterStop - MeterStart
Total Cost ($) = Energy Consumed × Tariff Rate
Charging Speed (kW) = Energy Consumed / Duration (hours)
```

### Example Calculation:
```
MeterStart:  1250.50 kWh
MeterStop:   1300.50 kWh
Energy:       50.00 kWh
Tariff:        $0.25/kWh
Duration:      60 minutes

Cost = 50.00 × 0.25 = $12.50
Speed = 50.00 / 1.0 = 50.00 kW
```

## 🆕 API Endpoints

### 1. Start Charging Session
**POST** `/api/ChargingSession/start-charging-session`

**Request:**
```json
{
  "chargingStationId": "station-guid",
  "userId": "user-guid",
  "connectorId": 1,
  "chargeTagId": "TAG001",
  "chargingTariff": "0.25"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Charging session started successfully.",
  "data": {
    "session": {
      "recId": "session-guid",
      "transactionId": 12345,
      "startMeterReading": "1250.50",
      "chargingTariff": "0.25"
    },
    "transactionId": 12345,
    "meterStart": 1250.50,
    "tariff": "0.25"
  }
}
```

### 2. End Charging Session
**POST** `/api/ChargingSession/end-charging-session`

**Request:**
```json
{
  "sessionId": "session-guid",
  "endMeterReading": "1300.50"  // Optional - uses OCPP data if available
}
```

**Response:**
```json
{
  "success": true,
  "message": "Charging session ended successfully. $12.50 debited.",
  "data": {
    "session": { ... },
    "transactionId": 12345,
    "energyConsumed": 50.00,
    "cost": 12.50,
    "meterStart": 1250.50,
    "meterStop": 1300.50,
    "duration": 60.5,
    "chargingSpeed": "49.50",
    "walletTransaction": {
      "transactionId": "wallet-tx-guid",
      "previousBalance": 100.00,
      "amountDebited": 12.50,
      "newBalance": 87.50
    }
  }
}
```

### 3. Enhanced Session Details (Real-Time Updates)
**GET** `/api/ChargingSession/charging-session-details/{sessionId}`

Now includes comprehensive real-time data for active sessions:
```json
{
  "success": true,
  "data": {
    "session": {
      "recId": "session-guid",
      "startTime": "2024-01-22T12:00:00Z",
      "status": "Active"
    },
    "transactionId": 12345,
    "status": "Charging",
    "isActive": true,
    "meterStart": 1250.50,
    "meterCurrent": 1275.25,
    "energyConsumed": 24.75,
    "estimatedCost": 6.19,
    "chargingSpeed": 49.50,
    "tariff": "0.25",
    "duration": {
      "totalMinutes": 30,
      "hours": 0,
      "minutes": 30,
      "totalHours": 0.5
    },
    "startTime": "2024-01-22T12:00:00Z",
    "endTime": null,
    "lastUpdate": "2024-01-22T12:30:00Z"
  }
}
```

## 🔧 Technical Implementation Details

### Transaction Lookup Logic

```csharp
// After starting OCPP transaction, wait briefly for DB update
await Task.Delay(1000);

// Get the actual OCPP transaction
var ocppTransaction = await _dbContext.Transactions
    .Where(t => t.ChargePointId == chargePointId && 
               t.ConnectorId == connectorId &&
               t.StartTagId == chargeTagId)
    .OrderByDescending(t => t.TransactionId)
    .FirstOrDefaultAsync();

int? transactionId = ocppTransaction?.TransactionId;
double meterStart = ocppTransaction?.MeterStart ?? 0;
```

### Meter Reading Retrieval

```csharp
// Get actual meter readings from OCPP Transaction
if (session.TransactionId.HasValue)
{
    var transaction = await _dbContext.Transactions
        .FirstOrDefaultAsync(t => t.TransactionId == session.TransactionId.Value);

    if (transaction != null)
    {
        startReading = transaction.MeterStart;
        endReading = transaction.MeterStop ?? endReading;
        actualStartTime = transaction.StartTime;
        actualEndTime = transaction.StopTime ?? DateTime.UtcNow;
    }
}
```

### Energy Calculation

```csharp
double energyTransmitted = Math.Max(0, endReading - startReading);
session.EnergyTransmitted = energyTransmitted.ToString("F2");

// Calculate cost
if (double.TryParse(session.ChargingTariff, out double tariff))
{
    decimal totalFee = (decimal)(energyTransmitted * tariff);
    session.ChargingTotalFee = totalFee.ToString("F2");
}
```

## 📝 Wallet Transaction Details

Enhanced wallet transaction logs now include:

```csharp
var walletTransaction = new WalletTransactionLog
{
    RecId = Guid.NewGuid().ToString(),
    UserId = session.UserId,
    PreviousCreditBalance = previousBalance.ToString("F2"),
    CurrentCreditBalance = newBalance.ToString("F2"),
    TransactionType = "Debit",
    ChargingSessionId = session.RecId,
    AdditionalInfo1 = $"Charging at {station} (OCPP Txn: {transactionId})",
    AdditionalInfo2 = $"Energy: {energy:F2} kWh @ ${tariff}/kWh = ${cost:F2}",
    AdditionalInfo3 = $"Meter: {start:F2} → {stop:F2} | Duration: {minutes:F0}min"
};
```

## ⚠️ Important Notes

### 1. Timing Considerations
- 1-second delay after OCPP call to allow DB update
- Real-time data may have slight lag
- Poll status endpoint for live updates

### 2. Fallback Mechanism
- Uses OCPP transaction data when available (preferred)
- Falls back to manual readings if transaction missing
- Logs warnings when fallback is used

### 3. Data Accuracy
- **OCPP Transaction = Source of Truth**
- Manual readings used only as backup
- Always prefer transaction meter values

### 4. Error Handling
- Continues even if OCPP transaction not found
- Logs warnings for missing transaction data
- Provides detailed error messages

## 🔄 Migration Required

Run this migration to add `TransactionId` column:

```bash
cd OCPP.Core.Database
dotnet ef migrations add AddTransactionIdToChargingSession --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
dotnet ef database update --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
```

## ✅ Testing Checklist

### Start Session Tests
- [ ] Verify TransactionId is captured
- [ ] Check MeterStart from transaction
- [ ] Confirm tariff rate is stored
- [ ] Validate response includes transaction data

### End Session Tests
- [ ] Verify meter readings from transaction
- [ ] Check energy calculation accuracy
- [ ] Validate cost calculation
- [ ] Test wallet debit with transaction details
- [ ] Verify enhanced response data

### Real-Time Status Tests
- [ ] Poll active session for updates
- [ ] Check energy consumed increases
- [ ] Verify estimated cost updates
- [ ] Test charging speed calculation

### Error Handling Tests
- [ ] Test with missing transaction
- [ ] Test with manual readings fallback
- [ ] Test insufficient wallet balance
- [ ] Test OCPP server offline

## 🎯 Benefits

### Accuracy
✅ Uses actual OCPP protocol meter readings
✅ Eliminates manual entry errors
✅ Consistent with OCPP standards
✅ Reliable cost calculations

### Transparency
✅ Shows actual vs estimated costs
✅ Real-time energy consumption
✅ Detailed transaction breakdown
✅ Clear meter reading sources

### Integration
✅ Links to OCPP Transactions table
✅ Maintains existing wallet system
✅ Compatible with OCPP 1.6/2.0
✅ Proper audit trail

### User Experience
✅ Real-time charging updates
✅ Accurate cost estimates
✅ Transparent billing
✅ No surprises at checkout

## 📊 Example Scenario

### Complete Charging Session:

**1. Start Charging:**
```
User initiates → OCPP starts → Transaction #12345 created
MeterStart: 1250.50 kWh
Session created with TransactionId: 12345
```

**2. During Charging (Real-time):**
```
Poll status endpoint every 30 seconds:
- MeterCurrent: 1275.25 kWh
- Energy: 24.75 kWh
- Estimated Cost: $6.19
- Speed: 49.50 kW
```

**3. End Charging:**
```
User stops → OCPP stops → Transaction updated
MeterStop: 1300.50 kWh
Energy: 50.00 kWh
Cost: $12.50
Wallet debited: $12.50
```

**4. Wallet Log:**
```
"Charging at CP001 (OCPP Txn: 12345)"
"Energy: 50.00 kWh @ $0.25/kWh = $12.50"
"Meter: 1250.50 → 1300.50 | Duration: 60min"
```

## 🔮 Future Enhancements

1. **WebSocket Updates**: Push real-time data to clients
2. **Billing Reconciliation**: Compare OCPP vs session data
3. **Energy Analytics**: Historical consumption patterns
4. **Dynamic Pricing**: Time-of-use tariff rates
5. **Multi-Currency**: Support different currencies
6. **Tax Calculations**: Add tax to billing
7. **Promo Codes**: Discount support

## 📞 Support

### Logs to Check:
- `"Using OCPP transaction data: Start={start}, Stop={stop}"`
- `"Transaction {id} not found, using fallback values"`
- `"No TransactionId found, using manual meter readings"`
- `"Real-time update for session {id}: {energy} kWh consumed"`

### Common Issues:

**Transaction not found:**
- Check OCPP server connectivity
- Verify charge point is online
- Ensure tag is authorized
- Check database synchronization

**Incorrect meter readings:**
- Verify OCPP protocol version
- Check meter value units (Wh vs kWh)
- Validate charge point configuration
- Review transaction logs

---

**Implementation Date**: 2024-01-22  
**Status**: ✅ Complete and Ready for Testing  
**Version**: 2.0 with OCPP Integration
