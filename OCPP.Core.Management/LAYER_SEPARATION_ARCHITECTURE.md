# Layer Separation Architecture - OCPP vs Management

## Overview

This document explains the clear separation between the **OCPP Protocol Layer** and the **Management/Cosmetic Layer** in the charging infrastructure system.

---

## Architecture Layers

### 1. **OCPP Layer** (Protocol/Core)
The underlying OCPP protocol layer that handles actual charging operations:
- **ChargePoint** - Physical charge point device
- **ConnectorStatus** - Physical connector/gun status

**Purpose:** Handle OCPP protocol communication, charging sessions, real-time status

**Characteristics:**
- Used by OCPP protocol handlers
- Updated by actual charge points
- Real-time status information
- Core system functionality

### 2. **Management Layer** (User-Facing/Cosmetic)
The user-facing management layer with additional business information:
- **ChargingHub** - Physical location with address, amenities, hours
- **ChargingStation** - User-facing station with images, descriptions
- **Charger/Gun** (uses ConnectorStatus with Active flag)

**Purpose:** Provide user-friendly management, location information, cosmetic details

**Characteristics:**
- Used by mobile apps and web interfaces
- Includes images, descriptions, reviews
- Business logic and analytics
- Soft delete support

---

## Data Flow & Cascade Behavior

### Adding a Charging Station

**Management Layer:**
```
User creates ChargingStation
  ↓
ChargingStation record created (RecId, Images, GunCount, etc.)
  ↓
Linked to ChargingHub
```

**OCPP Layer (Auto-managed):**
```
If ChargePoint doesn't exist:
  ↓
ChargePoint created automatically
  ↓
ChargePointId = ChargingStation.ChargingPointId
```

**Code:**
```csharp
// Management creates station
POST /api/charginghub/charging-station-add
{
  "chargingHubId": "hub-123",
  "chargingPointId": "CP001",  // Links to OCPP layer
  "chargingGunCount": 2,
  "chargingStationImage": "url"
}

// If CP001 doesn't exist in OCPP layer, it's created automatically
ChargePoint {
  ChargePointId = "CP001",
  Name = "Station CP001",
  Comment = "Created via Management API"
}
```

### Adding a Charger/Gun

**Management Layer:**
```
User adds Charger (gun) to Station
  ↓
Specifies connector details (name, type)
```

**OCPP Layer:**
```
ConnectorStatus created/updated
  ↓
ChargePointId + ConnectorId
  ↓
Active = 1 (visible in management)
  ↓
Available for OCPP protocol
```

**Code:**
```csharp
// Management creates charger
POST /api/charginghub/chargers-add
{
  "chargePointId": "CP001",
  "connectorId": 1,
  "connectorName": "Gun 1 - Type 2"
}

// Creates in OCPP layer
ConnectorStatus {
  ChargePointId = "CP001",
  ConnectorId = 1,
  ConnectorName = "Gun 1 - Type 2",
  LastStatus = "Available",
  Active = 1  // Management flag
}
```

### Updating a Charging Station

**Management Layer:**
```
Update ChargingStation details
  ↓
Images, descriptions, hub assignment
```

**OCPP Layer:**
```
If ChargingPointId changes:
  ↓
Create new ChargePoint if needed
  ↓
Link to new ChargePoint
```

### Deleting a Charging Station

**Management Layer:**
```
Soft delete ChargingStation (Active = 0)
  ↓
Station hidden from users
```

**OCPP Layer (Cascade):**
```
All associated ConnectorStatus records
  ↓
Soft deleted (Active = 0)
  ↓
Still in database for audit
  ↓
ChargePoint remains (for OCPP protocol)
```

**Code:**
```csharp
DELETE /api/charginghub/charging-station-delete/{stationId}

// Soft deletes:
ChargingStation.Active = 0
  +
All ConnectorStatus where ChargePointId = station.ChargingPointId
  → Active = 0
```

### Deleting a Charger/Gun

**Management Layer:**
```
Soft delete Charger (Active = 0)
```

**OCPP Layer:**
```
ConnectorStatus.Active = 0
  ↓
Hidden from management
  ↓
Still in database
  ↓
ChargePoint remains active
```

---

## Key Relationships

```
┌─────────────────────────────────────────────────────────────┐
│                    MANAGEMENT LAYER                         │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ChargingHub                                                │
│  └── ChargingStation (images, cosmetics)                    │
│      └── Linked to ChargePoint via ChargingPointId          │
│                                                             │
└─────────────────────┬───────────────────────────────────────┘
                      │ Links via ChargingPointId
                      ↓
┌─────────────────────────────────────────────────────────────┐
│                      OCPP LAYER                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ChargePoint (OCPP protocol device)                         │
│  └── ConnectorStatus (physical connectors)                  │
│      └── Active flag (for management visibility)            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## Transaction Flow (Future Implementation)

### Current State
```
OCPP Protocol
  ↓
Directly uses ChargePoint & ConnectorStatus
  ↓
Transactions table
```

### Target State (Layer Separation)
```
User initiates charging
  ↓
Management Layer: ChargingStation + Charger/Gun
  ↓
Maps to OCPP Layer: ChargePoint + ConnectorStatus
  ↓
OCPP Protocol handles charging
  ↓
Status updates both layers
```

---

## Database Schema

### Management Layer Tables

**ChargingHub**
```sql
- RecId (PK)
- AddressLine1, AddressLine2, AddressLine3
- City, State, Pincode
- Latitude, Longitude
- OpeningTime, ClosingTime
- ChargingHubImage
- TypeATariff, TypeBTariff
- Amenities
- Active
```

**ChargingStation**
```sql
- RecId (PK)
- ChargingHubId (FK → ChargingHub)
- ChargingPointId (FK → ChargePoint) ← Links to OCPP layer
- ChargingGunCount
- ChargingStationImage
- Active
```

### OCPP Layer Tables

**ChargePoint**
```sql
- ChargePointId (PK) ← Linked from ChargingStation
- Name
- Comment
- Username, Password
- ClientCertThumb
```

**ConnectorStatus**
```sql
- ChargePointId (PK, FK → ChargePoint)
- ConnectorId (PK)
- ConnectorName
- LastStatus
- LastStatusTime
- LastMeter, LastMeterTime
- Active ← Management visibility flag
```

---

## Cascade Behavior Summary

| Operation | ChargingStation | ChargingHub | ChargePoint | ConnectorStatus |
|-----------|----------------|-------------|-------------|-----------------|
| **Add Station** | Created | Must exist | Auto-created if missing | - |
| **Update Station** | Updated | - | Auto-created if changed | - |
| **Delete Station** | Active=0 | - | Remains active | All Active=0 |
| **Add Charger** | Must exist | - | Must exist | Created, Active=1 |
| **Update Charger** | - | - | - | Updated |
| **Delete Charger** | - | - | Remains active | Active=0 |
| **Delete Hub** | All Active=0 | Active=0 | Remains active | Cascade via stations |

---

## Benefits of Layer Separation

### 1. **Isolation**
- Management changes don't break OCPP protocol
- OCPP protocol changes don't affect user interface
- Clear boundaries between systems

### 2. **Flexibility**
- Add cosmetic features without touching OCPP core
- Multiple management views of same OCPP infrastructure
- Easy to add new business logic

### 3. **Data Integrity**
- Soft delete in management layer
- OCPP layer remains intact for audit
- Historical data preserved

### 4. **Scalability**
- Management layer can be scaled independently
- OCPP layer focuses on protocol performance
- Different caching strategies per layer

### 5. **Multi-Tenancy**
- Multiple ChargingHubs can share ChargePoints
- Multiple ChargingStations can represent same ChargePoint
- Flexible business models

---

## API Behavior

### Adding Station (Creates ChargePoint if needed)
```bash
POST /api/charginghub/charging-station-add
{
  "chargingHubId": "hub-123",
  "chargingPointId": "CP001",  # If doesn't exist, creates it
  "chargingGunCount": 2
}
```

### Deleting Station (Cascades to Connectors)
```bash
DELETE /api/charginghub/charging-station-delete/station-123

# Results in:
# - ChargingStation.Active = 0
# - All ConnectorStatus (for that ChargePoint) Active = 0
# - ChargePoint remains (for OCPP protocol)
```

### Updating Station (Handles ChargePoint changes)
```bash
PUT /api/charginghub/charging-station-update
{
  "recId": "station-123",
  "chargingPointId": "CP002",  # Changed from CP001
  # Creates CP002 if doesn't exist
}
```

---

## Code Examples

### Management Layer Access
```csharp
// Get all active stations in a hub (user-facing)
var stations = await _dbContext.ChargingStations
    .Where(s => s.ChargingHubId == hubId && s.Active == 1)
    .ToListAsync();

// Get all active chargers for a station (user-facing)
var chargers = await _dbContext.ConnectorStatuses
    .Where(c => c.ChargePointId == station.ChargingPointId && c.Active == 1)
    .ToListAsync();
```

### OCPP Layer Access
```csharp
// Get charge point for OCPP protocol (all connectors)
var chargePoint = await _dbContext.ChargePoints
    .FirstOrDefaultAsync(cp => cp.ChargePointId == chargePointId);

// Get connector status for OCPP (ignores Active flag)
var connector = await _dbContext.ConnectorStatuses
    .FirstOrDefaultAsync(c => 
        c.ChargePointId == chargePointId && 
        c.ConnectorId == connectorId);
// Note: OCPP layer doesn't filter by Active
```

---

## Migration Path

### Phase 1: Current Implementation ✅
- Management layer uses ChargingStation + ChargingHub
- Direct use of ConnectorStatus with Active flag
- ChargePoint auto-created when needed

### Phase 2: Transaction Isolation (Future)
- Transactions reference ChargingStation instead of ChargePoint
- Management API doesn't expose ChargePoint IDs
- Complete layer separation

### Phase 3: Enhanced Features (Future)
- Multiple ChargingStations per ChargePoint
- Station-specific pricing and features
- Advanced analytics on management layer

---

## Best Practices

### DO ✅
- Always use ChargingStation APIs for user-facing features
- Use Active flag for management visibility
- Auto-create ChargePoints when adding stations
- Cascade soft deletes from stations to connectors
- Keep OCPP layer intact for protocol operations

### DON'T ❌
- Don't expose ChargePoint IDs to end users
- Don't hard delete ConnectorStatus records
- Don't modify ChargePoint via management APIs
- Don't filter OCPP protocol queries by Active flag
- Don't create ConnectorStatus without ChargingStation

---

## Troubleshooting

**Problem:** Connector not showing in management
- **Check:** ConnectorStatus.Active = 1
- **Fix:** Ensure it wasn't soft-deleted

**Problem:** OCPP protocol can't find connector
- **Check:** ChargePoint and ConnectorStatus exist
- **Fix:** Don't filter by Active in OCPP layer

**Problem:** Station deleted but connectors still work
- **Expected:** ConnectorStatus remains for OCPP
- **Note:** Only Active = 0, still usable by protocol

**Problem:** Multiple stations with same ChargePointId
- **Expected:** Currently one-to-one
- **Future:** Will support one-to-many

---

## Summary

The architecture maintains clear separation:
- **Management Layer** = User-facing, cosmetic, soft-deletable
- **OCPP Layer** = Protocol-level, core functionality, persistent

This separation enables:
- ✅ Independent evolution of both layers
- ✅ Flexible business models
- ✅ Data integrity and audit trails
- ✅ Clean transaction handling
- ✅ Scalable architecture

All operations cascade properly while maintaining layer independence! 🎯
