# Charging Guns Integration Summary

## Overview
Integrated the `ChargingGuns` table into the database and updated the CRUD system to properly map charging guns to connectors, similar to how charging stations are linked to charge points.

## Database Changes

### 1. Added ChargingGuns Entity to OCPPCoreContext
- **File**: `OCPP.Core.Database\OCPPCoreContext.cs`
- Added `DbSet<EVCDTO.ChargingGuns> ChargingGuns` to the context
- Configured entity with proper relationships:
  - Foreign key to `ChargingStation` (one-to-many)
  - Foreign key to `ChargingHub` (one-to-many, for easier querying)
  - Foreign key to `ChargerTypeMaster` (one-to-many)
- Configured column constraints (max lengths, default values)

### 2. ChargingGuns Entity Properties
- **RecId**: Primary key (GUID string)
- **ChargingStationId**: Link to charging station
- **ConnectorId**: Link to OCPP ConnectorStatus (stored as string)
- **ChargingHubId**: Link to charging hub (denormalized for performance)
- **ChargerTypeId**: Link to ChargerTypeMaster
- **ChargerTariff**: Pricing information
- **PowerOutput**: Power output specifications
- **ChargerStatus**: Current status (Available, Occupied, etc.)
- **ChargerMeterReading**: Current meter reading
- **AdditionalInfo1/2**: Extra information fields
- **Active**: Soft delete flag (default: 1)
- **CreatedOn/UpdatedOn**: Timestamps

## Model Changes

### 1. Updated ChargerRequestDto
- **File**: `OCPP.Core.Management\Models\ChargingHub\ChargerRequestDto.cs`
- Changed from ConnectorStatus-based to ChargingGuns-based
- Required fields:
  - `ChargingStationId`: Station to which charger belongs
  - `ChargePointId`: OCPP charge point
  - `ConnectorId`: Connector identifier (string)
  - `ChargerTypeId`: Type of charger
- Optional fields:
  - `ChargerTariff`: Pricing
  - `PowerOutput`: Power specs
  - `AdditionalInfo1/2`: Extra info

### 2. Updated ChargerUpdateDto
- Changed to use `RecId` instead of ChargePointId + ConnectorId composite key
- Fields: ChargerTypeId, ChargerTariff, PowerOutput, ChargerStatus, AdditionalInfo1/2

### 3. Updated ChargerDto
- **File**: `OCPP.Core.Management\Models\ChargingHub\ChargerDto.cs`
- Enhanced to include both ChargingGuns data and ConnectorStatus data:
  - ChargingGuns properties (RecId, ChargingStationId, ChargerTypeId, etc.)
  - ConnectorStatus properties (LastStatus, LastMeter, LastStatusTime, etc.)
  - Related entity names (ChargePointName, ChargingHubName, ChargerTypeName)

## Controller Changes

### 1. AddCharger Method
- **File**: `OCPP.Core.Management\Controllers\ChargingHubController.cs`
- Validates station and hub existence
- Verifies ChargePoint exists in OCPP layer
- Creates ConnectorStatus in OCPP layer if doesn't exist
- Creates ChargingGuns record in management layer
- Returns mapped ChargerDto with all details

### 2. UpdateCharger Method
- Uses `RecId` to find charger
- Updates ChargingGuns properties only
- Does NOT modify ConnectorStatus (OCPP layer managed separately)

### 3. DeleteCharger Method
- Changed from composite key (ChargePointId + ConnectorId) to single `RecId`
- Performs soft delete on ChargingGuns record only
- ConnectorStatus remains active for OCPP operations

### 4. GetChargerList Method
- Queries ChargingGuns by ChargingStationId
- Uses async mapping to fetch related data
- Returns enriched ChargerDto with ConnectorStatus info

### 5. GetChargerDetails Method
- Changed from composite key to single `RecId`
- Returns full charger details with OCPP connector info

### 6. DeleteChargingStation Method (CASCADE)
- Now soft deletes associated ChargingGuns records
- Still soft deletes ConnectorStatus records for OCPP compatibility

### 7. GetChargingStationDetails Method
- Changed to query ChargingGuns instead of ConnectorStatus
- Uses async mapping for each charger

### 8. GetComprehensiveList Method
- Updated to use ChargingGuns for charger data
- Checks both ChargerStatus (from ChargingGuns) and LastStatus (from ConnectorStatus) for availability
- Filters by ChargerStatus from ChargingGuns

## Helper Methods

### 1. MapToChargerDtoAsync (NEW)
- **Async method** to map ChargingGuns entity to ChargerDto
- Fetches related entities:
  - ChargingStation (to get ChargePointId)
  - ChargingHub (to get hub name)
  - ChargePoint (to get charge point name)
  - ChargerTypeMaster (to get charger type name)
  - ConnectorStatus (to get OCPP real-time data)
- Combines management layer data with OCPP layer data

### 2. MapToChargerDto (KEPT)
- Original method kept for backward compatibility
- Maps ConnectorStatus directly to ChargerDto
- Used in cases where only OCPP data is needed

## Architecture

### Layer Separation
```
Management Layer (ChargingGuns)
    ├── Business logic and pricing
    ├── Charger types and tariffs
    ├── Metadata and configuration
    └── Links to: ChargingStation, ChargingHub, ChargerTypeMaster

OCPP Layer (ConnectorStatus)
    ├── Real-time OCPP protocol data
    ├── Connector status from charge points
    ├── Meter readings and availability
    └── Links to: ChargePoint

Integration Point
    └── ChargePointId + ConnectorId (string)
```

### Relationship Flow
```
ChargingHub
    └── ChargingStation
        ├── ChargingGuns (Management)
        │   └── ConnectorId (string reference)
        └── ChargePoint (OCPP)
            └── ConnectorStatus (OCPP)
                └── ConnectorId (int)
```

## Migration Required

You need to create a migration to:
1. Create the `ChargingGuns` table
2. Add foreign key constraints
3. Add indexes for performance

Run these commands:
```bash
cd "E:\Work\ORT\EV Charging\OCPP.Core"
dotnet ef migrations add AddChargingGunsTable --project OCPP.Core.Database --startup-project OCPP.Core.Management
dotnet ef database update --project OCPP.Core.Database --startup-project OCPP.Core.Management
```

## API Endpoints Updated

### Changed Endpoints
- **POST** `/api/ChargingHub/chargers-add`
  - Now requires: ChargingStationId, ChargePointId, ConnectorId, ChargerTypeId
  
- **PUT** `/api/ChargingHub/chargers-update`
  - Now uses: RecId instead of ChargePointId + ConnectorId
  
- **DELETE** `/api/ChargingHub/chargers-delete/{chargerId}`
  - Changed from: `/chargers-delete/{chargePointId}/{connectorId}`
  - Now uses: Single RecId parameter
  
- **GET** `/api/ChargingHub/charger-details/{chargerId}`
  - Changed from: `/charger-details/{chargePointId}/{connectorId}`
  - Now uses: Single RecId parameter

### Unchanged Endpoints
- **GET** `/api/ChargingHub/charger-list/{stationId}` (internal implementation changed)
- **POST** `/api/ChargingHub/comprehensive-list` (internal implementation changed)

## Benefits

1. **Better Data Organization**: Management layer data separated from OCPP protocol data
2. **Enhanced Functionality**: Can store charger types, tariffs, and business metadata
3. **Flexible Pricing**: Each charger can have its own tariff
4. **Better Tracking**: Soft delete and audit fields for chargers
5. **Type Safety**: Charger types linked to master data
6. **Performance**: Direct queries without complex joins
7. **Scalability**: Can add more charger-specific fields without affecting OCPP layer

## Backward Compatibility

- ConnectorStatus table and operations remain unchanged
- OCPP protocol operations continue to work normally
- Old MapToChargerDto method kept for direct ConnectorStatus mapping
- No breaking changes to OCPP message handling

## Testing Checklist

- [ ] Add new charger with all fields
- [ ] Update charger details
- [ ] Delete charger (verify soft delete)
- [ ] Get charger list for a station
- [ ] Get charger details by RecId
- [ ] Get station details (should include chargers)
- [ ] Get comprehensive list with charger filtering
- [ ] Verify cascade delete when station is deleted
- [ ] Verify ConnectorStatus integration (real-time status)
- [ ] Test with multiple chargers per station

## Notes

- ConnectorId is stored as string in ChargingGuns for flexibility
- ChargerStatus in ChargingGuns is management-level status
- LastStatus from ConnectorStatus is real-time OCPP status
- Both statuses are available in ChargerDto for comprehensive view
- The system now has two sources of truth working together harmoniously
