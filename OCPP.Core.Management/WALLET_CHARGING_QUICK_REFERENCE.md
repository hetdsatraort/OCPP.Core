# Quick Reference: Wallet Transaction with Charging Session

## Key Changes Summary

### 1. ChargingSession Entity
```csharp
// Added UserId field
public string UserId { get; set; }
```

### 2. Fee Calculation Formula
```
Energy Consumed (kWh) = End Meter Reading - Start Meter Reading
Total Fee = Energy Consumed × Charging Tariff
```

### 3. Wallet Transaction Flow
```
End Session → Calculate Fee → Check Balance → Debit Wallet → Create Transaction Log
```

## Code Snippets

### How Fee is Calculated
```csharp
double energyTransmitted = endReading - startReading;  // kWh
decimal totalFee = (decimal)(energyTransmitted * tariff);  // Fee in currency
```

### How Wallet is Debited
```csharp
// Get current balance
var lastTransaction = await _dbContext.WalletTransactionLogs
    .Where(w => w.UserId == session.UserId && w.Active == 1)
    .OrderByDescending(w => w.CreatedOn)
    .FirstOrDefaultAsync();

decimal previousBalance = decimal.Parse(lastTransaction.CurrentCreditBalance);
decimal newBalance = previousBalance - totalFee;

// Create transaction log
var walletTransaction = new WalletTransactionLog
{
    UserId = session.UserId,
    PreviousCreditBalance = previousBalance.ToString("F2"),
    CurrentCreditBalance = newBalance.ToString("F2"),
    TransactionType = "Debit",
    ChargingSessionId = session.RecId,
    // ... additional info
};
```

## API Usage Examples

### Start Charging Session
```bash
curl -X POST "https://localhost:5001/api/ChargingSession/start-charging-session" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "chargingGunId": "GUN1",
    "chargingStationId": "station-guid",
    "userId": "user-guid",
    "chargeTagId": "TAG001",
    "connectorId": 1,
    "startMeterReading": "100.00",
    "chargingTariff": "0.25"
  }'
```

### End Charging Session (with wallet debit)
```bash
curl -X POST "https://localhost:5001/api/ChargingSession/end-charging-session" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "session-guid",
    "endMeterReading": "150.00"
  }'
```

### Response with Wallet Info
```json
{
  "success": true,
  "message": "Charging session ended successfully. Amount 12.50 debited from wallet.",
  "data": {
    "session": {
      "recId": "session-guid",
      "energyTransmitted": "50.00",
      "chargingTotalFee": "12.50"
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

## Database Migration Command

```bash
cd OCPP.Core.Database
dotnet ef migrations add AddUserIdToChargingSession --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
dotnet ef database update --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
```

## Testing Checklist

- [ ] Start session with valid user
- [ ] End session and verify fee calculation
- [ ] Check wallet balance deduction
- [ ] Test insufficient balance scenario
- [ ] Verify transaction log created
- [ ] Check enhanced API response
- [ ] Test with zero tariff
- [ ] Test with JWT token authentication

## Common Scenarios

### Scenario 1: Normal Charging
- Start: 100 kWh
- End: 150 kWh
- Energy: 50 kWh
- Tariff: $0.25/kWh
- **Fee: $12.50**

### Scenario 2: Insufficient Balance
- Required: $12.50
- Available: $5.00
- **Result: Error message, no debit**

### Scenario 3: Free Charging
- Tariff: $0.00/kWh
- **Fee: $0.00** (transaction log still created)

## Transaction Log Details

Each wallet transaction includes:
- Previous balance
- Current balance
- Charging session reference
- Station information
- Energy consumed details
- Session duration

## Error Messages

| Error | Reason | Action |
|-------|--------|--------|
| "User not authenticated" | No JWT token or invalid userId | Provide valid token |
| "User not found or inactive" | User doesn't exist | Check userId |
| "Insufficient wallet balance" | Balance < Fee | Recharge wallet |
| "Charging session not found" | Invalid sessionId | Check session ID |

## Important Notes

1. **Balance Check**: System checks balance before processing
2. **Transaction Type**: Always "Debit" for charging sessions
3. **Decimal Precision**: All amounts formatted to 2 decimal places
4. **UTC Timestamps**: All dates in UTC timezone
5. **Active Flag**: Only active transactions counted for balance

## Support

For issues or questions, check:
- `WALLET_TRANSACTION_CHARGING_SESSION_SUMMARY.md` - Full documentation
- Application logs in OCPP.Core.Management
- Database `WalletTransactionLogs` table for transaction history
