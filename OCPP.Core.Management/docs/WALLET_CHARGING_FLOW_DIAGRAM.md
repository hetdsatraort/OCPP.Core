# Charging Session & Wallet Integration Flow Diagram

## Complete Transaction Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                    START CHARGING SESSION                            │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  User Request    │
                    │  with JWT Token  │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │ Extract UserId   │
                    │ from Token/Body  │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  Validate User   │
                    │   is Active      │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │ Validate Station │
                    │  & Charge Point  │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  Call OCPP API   │
                    │ StartTransaction │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │ Create Session   │
                    │  with UserId     │
                    │  StartReading    │
                    │    Tariff        │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │ Return Session   │
                    │      ID          │
                    └──────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                     END CHARGING SESSION                             │
└─────────────────────────────────────────────────────────────────────┘
                              ↓
                    ┌──────────────────┐
                    │ User Request with│
                    │ SessionId +      │
                    │ EndMeterReading  │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  Retrieve        │
                    │  Session Record  │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  Call OCPP API   │
                    │ StopTransaction  │
                    └──────────────────┘
                              ↓
                    ┌──────────────────────────────────┐
                    │   CALCULATE FEE                  │
                    │                                  │
                    │ Energy = EndReading - StartReading│
                    │ Fee = Energy × Tariff            │
                    │ Speed = Energy / Duration        │
                    └──────────────────────────────────┘
                              ↓
                    ┌──────────────────┐
                    │  Get Current     │
                    │  Wallet Balance  │
                    │  (Last Txn)      │
                    └──────────────────┘
                              ↓
                    ┌──────────────────┐
                    │ Balance >= Fee?  │
                    └──────────────────┘
                         ↓         ↓
                   YES            NO
                    ↓              ↓
          ┌──────────────┐   ┌─────────────────┐
          │ Calculate    │   │ Return Error:   │
          │ New Balance  │   │ "Insufficient   │
          └──────────────┘   │  Balance"       │
                ↓             └─────────────────┘
          ┌──────────────┐
          │ Create       │
          │ Wallet Txn   │
          │ Log (Debit)  │
          └──────────────┘
                ↓
          ┌──────────────┐
          │ Update       │
          │ Session with │
          │ End Details  │
          └──────────────┘
                ↓
          ┌──────────────┐
          │ Save to DB   │
          │ (Transaction)│
          └──────────────┘
                ↓
          ┌──────────────┐
          │ Return       │
          │ Session +    │
          │ Wallet Info  │
          └──────────────┘
```

## Data Flow Diagram

```
┌────────────────┐         ┌────────────────┐         ┌────────────────┐
│                │         │                │         │                │
│  ChargingHub   │◄────────│ ChargingStation│◄────────│ ChargingSession│
│                │         │                │         │                │
└────────────────┘         └────────────────┘         └────────┬───────┘
                                                                │
                                                                │ UserId
                                                                │
                                                                ↓
┌────────────────┐         ┌────────────────┐         ┌────────────────┐
│                │         │                │         │                │
│     Users      │◄────────│ WalletTxnLog   │◄────────│ ChargingSession│
│                │         │   (Debit)      │         │   (Completed)  │
└────────────────┘         └────────────────┘         └────────────────┘
                                    │
                                    ├─ PreviousBalance
                                    ├─ CurrentBalance
                                    ├─ TransactionType = "Debit"
                                    └─ ChargingSessionId (FK)
```

## Wallet Balance Calculation Flow

```
Step 1: Get Last Transaction
┌────────────────────────────────────────┐
│ SELECT TOP 1 *                         │
│ FROM WalletTransactionLogs             │
│ WHERE UserId = @UserId                 │
│   AND Active = 1                       │
│ ORDER BY CreatedOn DESC                │
└────────────────────────────────────────┘
                  ↓
          CurrentCreditBalance = 100.00

Step 2: Calculate Charging Fee
┌────────────────────────────────────────┐
│ StartReading = 100.00 kWh              │
│ EndReading   = 150.00 kWh              │
│ Energy       = 50.00 kWh               │
│ Tariff       = 0.25 per kWh            │
│ Fee          = 50.00 × 0.25 = 12.50    │
└────────────────────────────────────────┘
                  ↓
              Fee = 12.50

Step 3: Calculate New Balance
┌────────────────────────────────────────┐
│ PreviousBalance = 100.00               │
│ Fee             = 12.50                │
│ NewBalance      = 100.00 - 12.50       │
│                 = 87.50                │
└────────────────────────────────────────┘
                  ↓
          NewBalance = 87.50

Step 4: Create Transaction Log
┌────────────────────────────────────────┐
│ INSERT INTO WalletTransactionLogs      │
│ (                                      │
│   UserId,                              │
│   PreviousCreditBalance = "100.00",    │
│   CurrentCreditBalance = "87.50",      │
│   TransactionType = "Debit",           │
│   ChargingSessionId = @SessionId,      │
│   CreatedOn = UTC_NOW                  │
│ )                                      │
└────────────────────────────────────────┘
```

## Database Relationships

```
Users Table
├─ RecId (PK)
├─ EMailID
├─ PhoneNumber
└─ Active
     │
     │ 1:N
     ↓
ChargingSessions Table
├─ RecId (PK)
├─ UserId (FK) ────────┐
├─ ChargingStationID   │
├─ StartMeterReading   │
├─ EndMeterReading     │
├─ EnergyTransmitted   │
├─ ChargingTariff      │
├─ ChargingTotalFee    │
└─ Active              │
                       │ 1:N
                       ↓
WalletTransactionLogs Table
├─ RecId (PK)
├─ UserId (FK)
├─ ChargingSessionId (FK)
├─ PreviousCreditBalance
├─ CurrentCreditBalance
├─ TransactionType
├─ AdditionalInfo1
├─ AdditionalInfo2
├─ AdditionalInfo3
└─ Active
```

## Sequence Diagram

```
User          API           Database        OCPP          Wallet
 │             │                │             │             │
 │─Start ─────>│                │             │             │
 │             │─Validate User─>│             │             │
 │             │<──User OK──────│             │             │
 │             │─Start Txn──────┼────────────>│             │
 │             │<──Accepted─────┼─────────────│             │
 │             │─Save Session──>│             │             │
 │<─Session ID─│                │             │             │
 │             │                │             │             │
 │─End ───────>│                │             │             │
 │             │─Get Session───>│             │             │
 │             │<──Session──────│             │             │
 │             │─Stop Txn───────┼────────────>│             │
 │             │<──Stopped──────┼─────────────│             │
 │             │                │             │             │
 │             │─Calculate Fee─────────────────────────────>│
 │             │                │             │             │
 │             │─Get Balance────┼─────────────┼────────────>│
 │             │<──Balance──────┼─────────────┼─────────────│
 │             │                │             │             │
 │             │─Check Balance──────────────────────────────│
 │             │<──OK───────────┼─────────────┼─────────────│
 │             │                │             │             │
 │             │─Debit Wallet───┼─────────────┼────────────>│
 │             │─Create Txn Log>│             │             │
 │             │─Update Session>│             │             │
 │<─Success────│                │             │             │
 │  + Wallet   │                │             │             │
 │    Info     │                │             │             │
```

## State Transitions

```
ChargingSession States:

┌─────────────┐   Start Session   ┌─────────────┐
│             │──────────────────>│             │
│  Not Exist  │                   │   Active    │
│             │                   │ EndTime=Min │
└─────────────┘                   └──────┬──────┘
                                         │
                                         │ End Session
                                         │ + Debit Wallet
                                         ↓
                                  ┌─────────────┐
                                  │             │
                                  │  Completed  │
                                  │ EndTime=Set │
                                  └─────────────┘

WalletTransactionLog States:

┌─────────────┐   Create Debit    ┌─────────────┐
│             │──────────────────>│             │
│  No Record  │   Transaction     │   Active    │
│             │                   │  Active=1   │
└─────────────┘                   └─────────────┘
```

## Error Handling Flow

```
                    ┌──────────────────┐
                    │  End Session     │
                    │    Request       │
                    └────────┬─────────┘
                             ↓
                    ┌────────────────────┐
                    │ Session Exists?    │
                    └────────┬───────────┘
                         Yes │   No
                             ↓    └─────> Error: "Session not found"
                    ┌────────────────────┐
                    │ Already Ended?     │
                    └────────┬───────────┘
                         No  │   Yes
                             ↓    └─────> Error: "Already ended"
                    ┌────────────────────┐
                    │ User Exists?       │
                    └────────┬───────────┘
                         Yes │   No
                             ↓    └─────> Error: "User not found"
                    ┌────────────────────┐
                    │ Calculate Fee      │
                    └────────┬───────────┘
                             ↓
                    ┌────────────────────┐
                    │ Get Balance        │
                    └────────┬───────────┘
                             ↓
                    ┌────────────────────┐
                    │ Balance >= Fee?    │
                    └────────┬───────────┘
                         Yes │   No
                             ↓    └─────> Error: "Insufficient balance"
                    ┌────────────────────┐
                    │ Debit & Create Txn │
                    └────────┬───────────┘
                             ↓
                    ┌────────────────────┐
                    │ Success Response   │
                    └────────────────────┘
```

## Real-World Example

```
User: John Doe
Station: Fast Charger #3
Date: 2024-01-22

Timeline:
─────────────────────────────────────────────────────────────

10:00 AM - Start Session
├─ StartReading: 1250.00 kWh
├─ Tariff: $0.30/kWh
├─ Wallet Balance: $50.00
└─ Session ID: session-abc-123

10:30 AM - User Charging (30 minutes)
├─ Current Usage: ~25 kWh
└─ Estimated Cost: ~$7.50

11:00 AM - End Session
├─ EndReading: 1300.00 kWh
├─ Energy Used: 50.00 kWh
├─ Duration: 60 minutes
├─ Speed: 50 kW
└─ Total Fee: $15.00

Wallet Transaction:
├─ Previous Balance: $50.00
├─ Amount Debited: $15.00
├─ New Balance: $35.00
└─ Transaction ID: tx-def-456

Result:
├─ Session Completed ✓
├─ Wallet Debited ✓
├─ Transaction Log Created ✓
└─ User Notified ✓
```

## Integration Points

```
┌──────────────────────────────────────────────────────────┐
│                    External Systems                       │
└──────────────────────────────────────────────────────────┘
                             ↓
┌──────────────────────────────────────────────────────────┐
│                    OCPP Protocol Layer                    │
│  (ChargePoint Communication - Start/Stop Transaction)    │
└──────────────┬───────────────────────────────────────────┘
               ↓
┌──────────────────────────────────────────────────────────┐
│              ChargingSessionController                    │
│  - Authentication (JWT)                                   │
│  - Business Logic                                         │
│  - Fee Calculation                                        │
└──────────────┬───────────────────────────────────────────┘
               ↓
┌──────────────────────────────────────────────────────────┐
│                   Database Layer                          │
│  - ChargingSessions                                       │
│  - WalletTransactionLogs                                  │
│  - Users                                                  │
└──────────────────────────────────────────────────────────┘
```

## Summary

This integration provides:
1. ✅ Automatic fee calculation
2. ✅ Wallet balance validation
3. ✅ Automatic payment processing
4. ✅ Detailed transaction logging
5. ✅ Comprehensive error handling
6. ✅ Complete audit trail

All components work together seamlessly to provide a complete charging and payment solution.
