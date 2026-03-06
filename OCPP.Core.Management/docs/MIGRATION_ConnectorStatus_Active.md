# Database Migration - ConnectorStatus Active Field

## Change Summary

Added `Active` field to `ConnectorStatus` table to support soft delete functionality for chargers/guns/connectors.

## Database Changes Required

### SQL Migration Script

```sql
-- Add Active column to ConnectorStatus table
ALTER TABLE ConnectorStatus
ADD Active INT NOT NULL DEFAULT 1;

-- Create index on Active column for better query performance
CREATE INDEX IX_ConnectorStatus_Active ON ConnectorStatus(Active);

-- Update existing records to Active = 1
UPDATE ConnectorStatus
SET Active = 1
WHERE Active IS NULL OR Active = 0;
```

### Entity Framework Migration

```bash
# Create migration
dotnet ef migrations add AddActiveToConnectorStatus --project OCPP.Core.Database --startup-project OCPP.Core.Management

# Apply migration
dotnet ef database update --project OCPP.Core.Database --startup-project OCPP.Core.Management
```

## Code Changes Made

### 1. ConnectorStatus Entity
- Added `public int Active { get; set; }` property

### 2. ChargingHubController
All charger/connector operations now use soft delete:

- **AddCharger**: Sets `Active = 1` on creation
- **UpdateCharger**: Filters by `Active == 1`
- **DeleteCharger**: Sets `Active = 0` instead of removing from database
- **GetChargerList**: Filters by `Active == 1`
- **GetChargerDetails**: Filters by `Active == 1`
- **GetChargingStationDetails**: Filters chargers by `Active == 1`

## Benefits

### 1. **Data Integrity**
- Maintains historical records
- Preserves referential integrity
- Audit trail for deleted chargers

### 2. **Consistency**
- All entities now use same soft delete pattern:
  - ChargingHub: `Active = 0`
  - ChargingStation: `Active = 0`
  - ChargingHubReview: `Active = 0`
  - ConnectorStatus: `Active = 0` ← **NEW**

### 3. **OCPP Compliance**
- Doesn't break existing ChargePoint → Connector relationships
- Allows "decommissioning" connectors without data loss
- Can be reactivated if needed

### 4. **Query Performance**
- Index on `Active` field ensures fast filtering
- No performance degradation for active connectors

## Backward Compatibility

### Existing Data
- All existing `ConnectorStatus` records will be set to `Active = 1`
- No data loss
- No breaking changes to existing functionality

### Existing Queries
⚠️ **IMPORTANT**: Any existing queries that don't filter by `Active` status should be reviewed:

```csharp
// OLD - Returns all connectors including deleted
var connectors = await _dbContext.ConnectorStatuses
    .Where(c => c.ChargePointId == chargePointId)
    .ToListAsync();

// NEW - Returns only active connectors
var connectors = await _dbContext.ConnectorStatuses
    .Where(c => c.ChargePointId == chargePointId && c.Active == 1)
    .ToListAsync();
```

## Testing Checklist

- [ ] Verify migration runs successfully
- [ ] Test adding new charger (Active = 1)
- [ ] Test updating charger (queries Active = 1)
- [ ] Test deleting charger (sets Active = 0)
- [ ] Test listing chargers (only shows Active = 1)
- [ ] Test getting charger details (only shows Active = 1)
- [ ] Verify existing connectors are set to Active = 1
- [ ] Test OCPP operations still work correctly

## Rollback Plan

If needed, you can rollback this change:

```sql
-- Remove Active column
ALTER TABLE ConnectorStatus
DROP COLUMN Active;
```

However, this will lose information about which connectors were soft-deleted.

## Notes

1. **Default Value**: New connectors default to `Active = 1`
2. **Soft Delete**: Deleted connectors have `Active = 0`
3. **Reactivation**: Can reactivate by setting `Active = 1` again
4. **Filtering**: All queries now filter by `Active == 1`
5. **LastStatusTime**: Updated when soft deleting for audit purposes

## Impact Assessment

### Low Risk
- Additive change (adding column)
- Default value ensures backward compatibility
- No data loss
- No breaking changes to API contracts

### Medium Risk
- Existing code that queries `ConnectorStatuses` without `Active` filter will now include soft-deleted records
- Review all queries in:
  - `ControllerOCPP16.cs`
  - `HomeController.ChargePoint.cs`
  - Any other files that query `ConnectorStatuses`

## Recommendation

✅ **Apply this migration** - it provides consistency across all entities and maintains data integrity while supporting the soft delete pattern used throughout the application.
