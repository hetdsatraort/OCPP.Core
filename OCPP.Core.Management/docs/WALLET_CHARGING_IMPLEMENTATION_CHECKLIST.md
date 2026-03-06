# Implementation Checklist: Wallet Transaction with Charging Sessions

## ✅ Completed Changes

### Code Changes
- [x] Added `UserId` field to `ChargingSession` entity
- [x] Updated `StartChargingSession` to capture and validate UserId
- [x] Enhanced `EndChargingSession` with wallet transaction logic
- [x] Implemented fee calculation: `Energy × Tariff`
- [x] Added wallet balance validation
- [x] Created automatic wallet debit functionality
- [x] Added comprehensive error handling
- [x] Enhanced API responses with wallet information

### Documentation
- [x] Created comprehensive summary document
- [x] Created quick reference guide
- [x] Created flow diagrams
- [x] Created implementation checklist (this file)

## 🔲 Required Next Steps

### 1. Database Migration
```bash
cd E:\Work\ORT\EV Charging\OCPP.Core\OCPP.Core.Database
dotnet ef migrations add AddUserIdToChargingSession --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
dotnet ef database update --startup-project ..\OCPP.Core.Management\OCPP.Core.Management.csproj
```

**Expected Changes:**
- Adds `UserId` column to `ChargingSessions` table (nvarchar(50), nullable)
- Column will reference `Users.RecId`

### 2. Testing

#### Test Case 1: Normal Charging with Payment
```bash
# Prerequisites
- User with wallet balance: $100.00
- Charging station available
- Valid JWT token

# Steps
1. Start charging session
   POST /api/ChargingSession/start-charging-session
   {
     "chargingGunId": "GUN1",
     "chargingStationId": "station-guid",
     "userId": "user-guid",
     "chargeTagId": "TAG001",
     "connectorId": 1,
     "startMeterReading": "100.00",
     "chargingTariff": "0.25"
   }

2. End charging session
   POST /api/ChargingSession/end-charging-session
   {
     "sessionId": "session-guid",
     "endMeterReading": "150.00"
   }

# Expected Results
- Session created with UserId ✓
- Fee calculated: 50 kWh × $0.25 = $12.50 ✓
- Wallet debited: $100.00 - $12.50 = $87.50 ✓
- Transaction log created ✓
- Success response with wallet info ✓
```

#### Test Case 2: Insufficient Balance
```bash
# Prerequisites
- User with wallet balance: $5.00
- Charging station available
- Valid JWT token

# Steps
1. Start charging session (same as above)
2. End charging session with high consumption
   {
     "sessionId": "session-guid",
     "endMeterReading": "150.00"  // 50 kWh = $12.50
   }

# Expected Results
- Fee calculated: $12.50 ✓
- Balance check fails ✓
- Error message returned ✓
- No wallet debit ✓
- Session still marked as completed ✓
```

#### Test Case 3: Zero Tariff (Free Charging)
```bash
# Prerequisites
- User with wallet balance: $50.00
- Free charging station (tariff = 0)

# Steps
1. Start with chargingTariff: "0.00"
2. End session

# Expected Results
- Fee calculated: $0.00 ✓
- Transaction log created with $0 debit ✓
- Balance unchanged ✓
```

#### Test Case 4: Missing UserId
```bash
# Prerequisites
- No UserId in request
- Invalid or missing JWT token

# Expected Results
- Error: "User not authenticated" ✓
- Session not created ✓
```

### 3. Database Verification

#### Check ChargingSessions Table
```sql
-- Verify UserId column exists
SELECT TOP 10 
    RecId, 
    UserId, 
    ChargingStationID,
    StartMeterReading,
    EndMeterReading,
    ChargingTariff,
    ChargingTotalFee
FROM ChargingSessions
ORDER BY CreatedOn DESC;
```

#### Check WalletTransactionLogs
```sql
-- Verify wallet transactions created for charging
SELECT TOP 10
    RecId,
    UserId,
    PreviousCreditBalance,
    CurrentCreditBalance,
    TransactionType,
    ChargingSessionId,
    AdditionalInfo1,
    CreatedOn
FROM WalletTransactionLogs
WHERE ChargingSessionId IS NOT NULL
ORDER BY CreatedOn DESC;
```

#### Check User Wallet Balance
```sql
-- Get current balance for a user
SELECT TOP 1
    UserId,
    CurrentCreditBalance,
    CreatedOn
FROM WalletTransactionLogs
WHERE UserId = 'user-guid'
  AND Active = 1
ORDER BY CreatedOn DESC;
```

### 4. API Testing with Postman

#### Import and Configure
- [ ] Import existing Postman collection
- [ ] Add new test cases for wallet transactions
- [ ] Configure environment variables:
  - `base_url`: https://localhost:5001
  - `jwt_token`: (from login)
  - `user_id`: (from token)
  - `session_id`: (from start session)

#### Test Endpoints
- [ ] POST /api/User/login - Get JWT token
- [ ] POST /api/User/add-wallet-credits - Add test balance
- [ ] GET /api/User/wallet-details - Check initial balance
- [ ] POST /api/ChargingSession/start-charging-session
- [ ] POST /api/ChargingSession/end-charging-session
- [ ] GET /api/User/wallet-details - Check final balance

### 5. Monitoring and Logging

#### Log Files to Monitor
```
Location: OCPP.Core.Management/logs/

Watch for:
- "Charging session started: [sessionId] for user [userId]"
- "Charging session ended: [sessionId]. Fee: [fee] debited from wallet"
- "Insufficient wallet balance for user [userId]"
- Any ERROR or EXCEPTION entries
```

#### Key Metrics
- [ ] Session creation success rate
- [ ] Wallet debit success rate
- [ ] Average transaction time
- [ ] Insufficient balance occurrences

### 6. Data Migration (if needed)

If you have existing charging sessions without UserId:
```sql
-- Option 1: Set UserId to NULL (allowed)
-- Migration already handles this

-- Option 2: Backfill UserId if possible
UPDATE ChargingSessions
SET UserId = (
    SELECT TOP 1 UserId
    FROM WalletTransactionLogs wtl
    WHERE wtl.ChargingSessionId = ChargingSessions.RecId
)
WHERE UserId IS NULL
  AND EXISTS (
    SELECT 1 FROM WalletTransactionLogs wtl
    WHERE wtl.ChargingSessionId = ChargingSessions.RecId
);
```

### 7. User Communication

#### Notify Users About:
- [ ] Wallet balance requirement for charging
- [ ] How to check wallet balance
- [ ] How to add wallet credits
- [ ] Fee calculation method
- [ ] What happens with insufficient balance

#### Sample User Communication:
```
Subject: Important: Wallet Balance Required for Charging

Dear Valued Customer,

We've enhanced our charging system to provide a seamless payment experience.

Key Changes:
✓ Automatic payment processing from your wallet
✓ Real-time balance updates
✓ Detailed transaction history
✓ Energy-based billing (kWh × Rate)

Before Your Next Charge:
1. Check your wallet balance
2. Add credits if needed
3. Enjoy hassle-free charging!

Questions? Contact support@yourcompany.com
```

## 🔍 Validation Checklist

### Code Quality
- [x] No compilation errors
- [x] Proper error handling
- [x] Input validation
- [x] Database transactions
- [x] Logging implemented
- [x] Code comments clear

### Security
- [x] JWT authentication required
- [x] User validation
- [x] Balance validation
- [x] SQL injection prevention (EF Core)
- [x] Sensitive data handling

### Performance
- [x] Efficient database queries
- [x] Proper indexing (existing)
- [x] No N+1 queries
- [x] Transaction scope appropriate

### Documentation
- [x] API endpoints documented
- [x] Code comments added
- [x] User guide created
- [x] Flow diagrams provided

## 📊 Success Criteria

### Technical
- [ ] All tests pass
- [ ] No errors in logs
- [ ] Database migration successful
- [ ] API responses correct

### Business
- [ ] Fee calculation accurate
- [ ] Wallet balance updates correctly
- [ ] Transaction logs complete
- [ ] User experience smooth

### Operational
- [ ] Monitoring in place
- [ ] Logs reviewed
- [ ] Support team trained
- [ ] Documentation updated

## 🚀 Deployment Steps

### Pre-Deployment
1. [ ] Review all code changes
2. [ ] Run all tests
3. [ ] Backup database
4. [ ] Prepare rollback plan

### Deployment
1. [ ] Stop application
2. [ ] Deploy code changes
3. [ ] Run database migration
4. [ ] Start application
5. [ ] Verify health check

### Post-Deployment
1. [ ] Test critical paths
2. [ ] Monitor logs for 1 hour
3. [ ] Check error rates
4. [ ] Validate transactions

## 📝 Rollback Plan

If issues occur:
```sql
-- Remove UserId column (if needed)
ALTER TABLE ChargingSessions
DROP COLUMN UserId;

-- Revert to previous application version
-- Deploy previous code version
```

## 🎯 Final Verification

Before marking complete, verify:
- [ ] Database migration applied
- [ ] All tests passed
- [ ] API responses correct
- [ ] Wallet transactions working
- [ ] Error handling working
- [ ] Logs showing correct info
- [ ] Documentation complete
- [ ] Team notified

## 📞 Support Contacts

**Technical Issues:**
- Developer: [Your Name]
- Email: dev@company.com

**Business Questions:**
- Product Manager: [PM Name]
- Email: pm@company.com

## 📅 Timeline

- [x] Code Implementation: Completed
- [x] Documentation: Completed
- [ ] Database Migration: Pending
- [ ] Testing: Pending
- [ ] Deployment: Pending
- [ ] Monitoring: Pending

## 🎉 Completion

When all items are checked:
1. Mark this implementation as complete
2. Archive this checklist
3. Update project documentation
4. Celebrate success! 🎊

---

**Last Updated**: 2024-01-22  
**Status**: Ready for Testing  
**Version**: 1.0
