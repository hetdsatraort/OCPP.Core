# Wallet Transaction Integration with Charging Sessions

## Overview
Successfully integrated wallet transaction functionality with charging sessions. When a charging session ends, the system automatically calculates the fee based on energy consumed and tariff, then debits the user's wallet.

## Changes Made

### 1. Database Schema Update
**File: `OCPP.Core.Database\EVCDTO\ChargingSession.cs`**
- Added `UserId` field to track which user initiated the charging session
- This links charging sessions to users for wallet transactions

```csharp
public class ChargingSession
{
    public string RecId { get; set; }
    public string UserId { get; set; }  // ✨ NEW FIELD
    public string ChargingGunId { get; set; }
    // ... other fields
}
```

### 2. Start Charging Session Enhancement
**File: `OCPP.Core.Management\Controllers\ChargingSessionController.cs`**

#### Changes in `StartChargingSession`:
- Extracts `UserId` from request or JWT token
- Validates user exists and is active
- Stores `UserId` in the charging session record
- Enhanced logging to include user information

```csharp
// Get user ID from token or request
string userId = request.UserId;
if (string.IsNullOrEmpty(userId))
{
    userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
}

// Verify user exists and is active
var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId && u.Active == 1);
```

### 3. End Charging Session with Wallet Transaction
**File: `OCPP.Core.Management\Controllers\ChargingSessionController.cs`**

#### Changes in `EndChargingSession`:
1. **Fee Calculation**
   - Calculates energy consumed: `EndMeterReading - StartMeterReading`
   - Calculates total fee: `EnergyConsumed × ChargingTariff`

2. **Wallet Balance Check**
   - Retrieves user's current wallet balance from last transaction
   - Validates sufficient funds before processing
   - Returns error if insufficient balance

3. **Wallet Transaction Creation**
   - Debits the calculated fee from user's wallet
   - Creates detailed transaction log with:
     - Previous balance
     - Current balance
     - Transaction type: "Debit"
     - Charging session reference
     - Detailed information about the charge

4. **Enhanced Response**
   - Returns both session details and wallet transaction information
   - Includes previous balance, amount debited, and new balance

## Workflow

### Starting a Charging Session
```
1. User initiates charging session
2. System validates user and station
3. OCPP server starts transaction
4. Session created with UserId
5. Session ID returned to user
```

### Ending a Charging Session
```
1. User/System ends charging session
2. System retrieves session details
3. Calculates:
   ├─ Energy consumed = EndReading - StartReading
   ├─ Total fee = Energy × Tariff
   └─ Charging speed = Energy / Duration
4. Checks wallet balance
5. If sufficient balance:
   ├─ Debits wallet
   ├─ Creates transaction log
   └─ Updates session
6. Returns session and wallet details
```

## Fee Calculation Formula

```
Energy Consumed (kWh) = End Meter Reading - Start Meter Reading
Total Fee = Energy Consumed × Charging Tariff
Charging Speed (kW) = Energy Consumed / Duration (hours)
```

### Example:
- Start Meter Reading: 100.00 kWh
- End Meter Reading: 150.00 kWh
- Energy Consumed: 50.00 kWh
- Charging Tariff: $0.25 per kWh
- **Total Fee: $12.50**

## Wallet Transaction Details

### Transaction Log Fields:
| Field | Value | Description |
|-------|-------|-------------|
| RecId | GUID | Unique transaction ID |
| UserId | User GUID | User who was charged |
| PreviousCreditBalance | Decimal | Balance before transaction |
| CurrentCreditBalance | Decimal | Balance after transaction |
| TransactionType | "Debit" | Type of transaction |
| ChargingSessionId | Session GUID | Reference to charging session |
| AdditionalInfo1 | Station details | Charging point information |
| AdditionalInfo2 | Energy & Rate | Energy consumed and tariff |
| AdditionalInfo3 | Duration | Session duration in minutes |

### Example Transaction Log:
```json
{
  "RecId": "tx-12345-guid",
  "UserId": "user-67890-guid",
  "PreviousCreditBalance": "100.00",
  "CurrentCreditBalance": "87.50",
  "TransactionType": "Debit",
  "ChargingSessionId": "session-abc-guid",
  "AdditionalInfo1": "Charging session at CP001",
  "AdditionalInfo2": "Energy: 50.00 kWh, Rate: 0.25",
  "AdditionalInfo3": "Duration: 120 minutes",
  "CreatedOn": "2024-01-22T10:30:00Z"
}
```

## API Response Examples

### Success Response (Sufficient Balance):
```json
{
  "success": true,
  "message": "Charging session ended successfully. Amount 12.50 debited from wallet. OCPP Status: Stop transaction accepted",
  "data": {
    "session": {
      "recId": "session-guid",
      "userId": "user-guid",
      "energyTransmitted": "50.00",
      "chargingTotalFee": "12.50",
      "status": "Completed"
    },
    "walletTransaction": {
      "transactionId": "tx-guid",
      "previousBalance": 100.00,
      "amountDebited": 12.50,
      "newBalance": 87.50
    }
  }
}
```

### Error Response (Insufficient Balance):
```json
{
  "success": false,
  "message": "Insufficient wallet balance. Required: 12.50, Available: 5.00. Please recharge your wallet.",
  "data": {
    "recId": "session-guid",
    "chargingTotalFee": "12.50",
    "status": "Completed"
  }
}
```

## Security & Validation

### User Authentication:
- JWT token required for all endpoints
- User ID extracted from token or request
- User existence and active status validated

### Balance Validation:
- Current balance retrieved from latest wallet transaction
- Insufficient balance check before processing
- Transaction only created if sufficient funds

### Data Integrity:
- All operations wrapped in try-catch
- Database transaction consistency
- Detailed error logging

## Database Migration Required

After implementing these changes, run the following migration:

```bash
cd OCPP.Core.Database
dotnet ef migrations add AddUserIdToChargingSession --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
dotnet ef database update --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
```

This will add the `UserId` column to the `ChargingSessions` table.

## Testing Guide

### Test Scenario 1: Successful Charging with Payment
1. **Setup**: User has $100 in wallet
2. **Action**: Start and end charging session (50 kWh @ $0.25/kWh)
3. **Expected**: 
   - Fee calculated: $12.50
   - Wallet debited: $12.50
   - New balance: $87.50
   - Transaction log created

### Test Scenario 2: Insufficient Balance
1. **Setup**: User has $5 in wallet
2. **Action**: Start and end charging session (50 kWh @ $0.25/kWh)
3. **Expected**:
   - Fee calculated: $12.50
   - Balance check fails
   - Error message returned
   - Session marked complete but no wallet debit
   - User prompted to recharge wallet

### Test Scenario 3: Zero Cost Session
1. **Setup**: Free charging (tariff = $0)
2. **Action**: Start and end charging session
3. **Expected**:
   - Fee: $0
   - Wallet transaction created with $0 debit
   - Balance remains unchanged

## API Endpoints Updated

### POST `/api/ChargingSession/start-charging-session`
**Request:**
```json
{
  "chargingGunId": "GUN1",
  "chargingStationId": "station-guid",
  "userId": "user-guid",
  "chargeTagId": "TAG001",
  "connectorId": 1,
  "startMeterReading": "100.00",
  "chargingTariff": "0.25"
}
```

**New Behavior:**
- Captures and validates `userId`
- Stores `userId` in session record

### POST `/api/ChargingSession/end-charging-session`
**Request:**
```json
{
  "sessionId": "session-guid",
  "endMeterReading": "150.00"
}
```

**New Behavior:**
- Calculates energy consumed
- Calculates total fee
- Checks wallet balance
- Debits wallet if sufficient funds
- Creates wallet transaction log
- Returns enhanced response with wallet details

## Logging

Enhanced logging includes:
- User ID in session start logs
- Fee calculation details
- Wallet balance checks
- Transaction creation
- Insufficient balance warnings

Example log entries:
```
INFO: Charging session started: session-123 for user user-456 at station station-789
INFO: Charging session ended: session-123. Fee: 12.50 debited from wallet. New balance: 87.50
WARN: Insufficient wallet balance for user user-456. Required: 12.50, Available: 5.00
```

## Error Handling

### Comprehensive error handling for:
1. **User Validation**
   - User not found
   - User inactive
   - Invalid authentication

2. **Balance Issues**
   - Insufficient balance
   - Wallet transaction errors
   - Balance calculation errors

3. **Session Issues**
   - Session not found
   - Session already ended
   - Invalid meter readings

4. **System Errors**
   - Database errors
   - OCPP communication failures
   - Calculation errors

## Integration with Existing Features

### Compatible with:
- ✅ JWT Authentication
- ✅ User Management
- ✅ Wallet System
- ✅ OCPP Protocol
- ✅ Charging Stations
- ✅ Transaction Logging

### Future Enhancements:
- Payment gateway integration for wallet recharge
- Email/SMS notifications for low balance
- Automatic wallet recharge
- Billing history reports
- Tax calculations
- Discount codes/promotions
- Subscription-based charging plans

## Summary

| Feature | Status | Description |
|---------|--------|-------------|
| UserId in Session | ✅ Complete | Track user for each session |
| Fee Calculation | ✅ Complete | Energy × Tariff formula |
| Balance Validation | ✅ Complete | Check before debit |
| Wallet Debit | ✅ Complete | Automatic payment processing |
| Transaction Log | ✅ Complete | Detailed audit trail |
| Error Handling | ✅ Complete | Comprehensive validation |
| API Enhancement | ✅ Complete | Enhanced responses |
| Documentation | ✅ Complete | This document |

## Migration Checklist

- [ ] Run database migration to add `UserId` to `ChargingSessions`
- [ ] Test with existing sessions (may need to backfill UserId)
- [ ] Test with new charging sessions
- [ ] Test insufficient balance scenario
- [ ] Test wallet transaction creation
- [ ] Verify transaction logs
- [ ] Update API documentation
- [ ] Update Postman collection
- [ ] Notify users about wallet requirements
- [ ] Monitor logs for any issues

## Next Steps

1. **Run Migration**: Add `UserId` column to database
2. **Test Thoroughly**: Cover all scenarios
3. **Monitor**: Watch for wallet balance issues
4. **User Communication**: Inform users about wallet requirements
5. **Enhancement**: Consider adding wallet recharge reminders

---

**Implementation Date**: 2024-01-22  
**Status**: Ready for Testing  
**Breaking Changes**: None (backward compatible)
