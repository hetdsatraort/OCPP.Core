# Quick Reference: Comprehensive Hub List API

## Endpoint
```
POST /api/ChargingHub/comprehensive-list
```

## Quick Examples

### 1. Find Nearest Hubs (with available chargers)
```json
{
  "latitude": 40.7128,
  "longitude": -74.0060,
  "radiusKm": 10,
  "hasAvailableChargers": true,
  "pageSize": 10
}
```

### 2. Search by City
```json
{
  "city": "Los Angeles",
  "pageSize": 20
}
```

### 3. Text Search
```json
{
  "searchTerm": "downtown",
  "pageSize": 10
}
```

### 4. All Hubs (Paginated)
```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "sortBy": "Name"
}
```

## Response Structure
```
Hub
├── Basic Info (name, address, location, etc.)
├── Stats (distance, rating, charger counts)
└── Stations[]
    ├── Station Info
    └── Chargers[]
        └── Charger Status
```

## Sort Options
- `Distance` - By distance from user
- `Name` - Alphabetically
- `Rating` - By average rating
- `AvailableChargers` - By available count
- `TotalChargers` - By total count

## Filter Options
| Filter | Type | Description |
|--------|------|-------------|
| `searchTerm` | string | Search in name/city/state/address |
| `city` | string | Exact city match |
| `state` | string | Exact state match |
| `pincode` | string | Exact pincode match |
| `radiusKm` | double | Distance radius (with lat/lng) |
| `chargerStatus` | string | Filter by charger status |
| `hasAvailableChargers` | bool | Only hubs with available chargers |

## Charger Status Values
- `Available`
- `Charging`
- `Preparing`
- `Finishing`
- `Faulted`
- `Unavailable`

## Pagination
```json
{
  "pageNumber": 1,      // Page to retrieve (1-based)
  "pageSize": 10        // Items per page (default: 10)
}
```

Response includes:
```json
{
  "totalCount": 50,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 5
}
```

## Use Cases

### Mobile App Map View
```javascript
// Get user location and find nearby hubs
const hubs = await searchHubs({
  latitude: userLat,
  longitude: userLng,
  radiusKm: 20,
  hasAvailableChargers: true,
  sortBy: "Distance"
});

// Plot on map
hubs.forEach(hub => addMarker(hub));
```

### Web Dashboard
```javascript
// Browse all hubs with pagination
const results = await searchHubs({
  pageNumber: currentPage,
  pageSize: 20,
  sortBy: "Name",
  sortOrder: "Asc"
});

// Display hub cards
renderHubList(results.hubs);
renderPagination(results.totalPages);
```

### Route Planning
```javascript
// Find hubs along route
const waypoints = [
  {lat: 40.7128, lng: -74.0060},
  {lat: 41.8781, lng: -87.6298}
];

const hubsOnRoute = [];
for (const point of waypoints) {
  const hubs = await searchHubs({
    latitude: point.lat,
    longitude: point.lng,
    radiusKm: 5,
    hasAvailableChargers: true
  });
  hubsOnRoute.push(...hubs);
}
```

## Response Data Points

### Hub Level
- Location & address
- Operating hours
- Tariff rates (Type A & B)
- Amenities
- Distance from user
- Rating & reviews
- Total stations
- Total chargers
- Available chargers

### Station Level
- Charge point ID
- Station name
- Gun count
- Available chargers
- All chargers with status

### Charger Level
- Connector ID
- Status (real-time)
- Last meter reading
- Last status update time

## Performance Tips

1. **Use location filtering** - Provide lat/lng with radius
2. **Limit page size** - Keep pageSize between 10-50
3. **Specific filters** - Use city/state instead of searchTerm
4. **Cache results** - Cache for 2-5 minutes
5. **Lazy load details** - Load stations/chargers on demand

## Common Patterns

### Pattern 1: Mobile App Home Screen
```json
{
  "latitude": {{userLat}},
  "longitude": {{userLng}},
  "radiusKm": 10,
  "hasAvailableChargers": true,
  "pageSize": 20,
  "sortBy": "Distance"
}
```

### Pattern 2: City Browse
```json
{
  "city": "{{selectedCity}}",
  "pageNumber": {{currentPage}},
  "pageSize": 10,
  "sortBy": "Rating",
  "sortOrder": "Desc"
}
```

### Pattern 3: Search Results
```json
{
  "searchTerm": "{{userInput}}",
  "pageNumber": 1,
  "pageSize": 20,
  "sortBy": "Name"
}
```

## Testing with cURL

```bash
# Basic search
curl -X POST "http://localhost:5001/api/ChargingHub/comprehensive-list" \
  -H "Content-Type: application/json" \
  -d '{"pageSize": 10}'

# Location-based search
curl -X POST "http://localhost:5001/api/ChargingHub/comprehensive-list" \
  -H "Content-Type: application/json" \
  -d '{
    "latitude": 40.7128,
    "longitude": -74.0060,
    "radiusKm": 5,
    "pageSize": 10
  }'

# City search
curl -X POST "http://localhost:5001/api/ChargingHub/comprehensive-list" \
  -H "Content-Type: application/json" \
  -d '{
    "city": "New York",
    "pageSize": 20
  }'
```

## Error Handling

```javascript
try {
  const response = await fetch('/api/ChargingHub/comprehensive-list', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(searchParams)
  });
  
  if (!response.ok) {
    throw new Error(`HTTP ${response.status}`);
  }
  
  const data = await response.json();
  
  if (!data.success) {
    console.error(data.message);
    return;
  }
  
  // Process results
  processHubs(data.hubs);
  
} catch (error) {
  console.error('Search failed:', error);
  showError('Unable to load charging hubs');
}
```

## Data Processing Examples

### Calculate Total Available
```javascript
const totalAvailable = hubs.reduce(
  (sum, hub) => sum + hub.availableChargers, 
  0
);
```

### Filter by Amenities
```javascript
const hubsWithWifi = hubs.filter(hub => 
  hub.amenities?.toLowerCase().includes('wifi')
);
```

### Group by City
```javascript
const hubsByCity = hubs.reduce((groups, hub) => {
  const city = hub.city;
  if (!groups[city]) groups[city] = [];
  groups[city].push(hub);
  return groups;
}, {});
```

### Find Nearest Hub
```javascript
const nearest = hubs.reduce((prev, curr) => 
  (curr.distanceKm < prev.distanceKm) ? curr : prev
);
```

## Defaults

If not specified:
- `pageNumber`: 1
- `pageSize`: 10
- `sortBy`: "Distance"
- `sortOrder`: "Asc"

## Limits

- Max `pageSize`: 100 (recommended: 10-50)
- Max `radiusKm`: No limit (recommended: 5-50)
- `searchTerm`: Case-insensitive, partial match

## Notes

- All datetime values in UTC
- Distance calculated using Haversine formula
- Charger status from last OCPP update
- Ratings calculated from active reviews only
- Only returns active (non-deleted) hubs/stations/chargers

---

**Quick Tip**: For mobile apps, start with radius=10km and increase if no results found.
