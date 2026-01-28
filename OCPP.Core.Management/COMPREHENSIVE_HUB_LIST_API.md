# Comprehensive Charging Hub Listing API

## Overview
This API endpoint provides a complete listing of charging hubs with their stations and chargers (guns) in a single request. It supports advanced search, filtering, pagination, and sorting options.

## Endpoint

**POST** `/api/ChargingHub/comprehensive-list`

**Authorization:** Not required (AllowAnonymous)

**Content-Type:** `application/json`

## Features

✅ **Hierarchical Data**: Hubs → Stations → Chargers  
✅ **Location-Based Search**: Find hubs within a radius  
✅ **Text Search**: Search by hub name, city, state, or address  
✅ **Multiple Filters**: City, state, pincode, charger status, availability  
✅ **Pagination**: Control page size and number  
✅ **Flexible Sorting**: Sort by distance, name, rating, or charger count  
✅ **Real-time Status**: Current charger availability  
✅ **Ratings**: Average ratings and review counts  

## Request Body

```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "latitude": 40.7128,
  "longitude": -74.0060,
  "radiusKm": 50,
  "searchTerm": "downtown",
  "city": "New York",
  "state": "NY",
  "pincode": "10001",
  "chargerStatus": "Available",
  "hasAvailableChargers": true,
  "sortBy": "Distance",
  "sortOrder": "Asc"
}
```

### Request Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `pageNumber` | int | No | 1 | Page number (1-based) |
| `pageSize` | int | No | 10 | Number of items per page (1-100) |
| `latitude` | double? | No | null | User's latitude for distance calculation |
| `longitude` | double? | No | null | User's longitude for distance calculation |
| `radiusKm` | double? | No | null | Search radius in kilometers |
| `searchTerm` | string | No | null | Search in hub name, city, state, address |
| `city` | string | No | null | Filter by specific city |
| `state` | string | No | null | Filter by specific state |
| `pincode` | string | No | null | Filter by specific pincode |
| `chargerStatus` | string | No | null | Filter chargers by status (Available, Charging, Faulted, etc.) |
| `hasAvailableChargers` | bool? | No | null | Only show hubs with available chargers |
| `sortBy` | string | No | "Distance" | Sort criteria |
| `sortOrder` | string | No | "Asc" | Sort order (Asc/Desc) |

### Sort Options

- **Distance**: Sort by distance from user location
- **Name**: Sort by hub name alphabetically
- **Rating**: Sort by average rating
- **AvailableChargers**: Sort by number of available chargers
- **TotalChargers**: Sort by total number of chargers

## Response Structure

```json
{
  "success": true,
  "message": "Found 3 charging hub(s) matching criteria",
  "hubs": [
    {
      "recId": "hub-guid-123",
      "chargingHubName": "Downtown Fast Charge Hub",
      "addressLine1": "123 Main Street",
      "city": "New York",
      "state": "NY",
      "pincode": "10001",
      "latitude": "40.7128",
      "longitude": "-74.0060",
      "chargingHubImage": "https://example.com/hub1.jpg",
      "openingTime": "06:00:00",
      "closingTime": "22:00:00",
      "typeATariff": "0.25",
      "typeBTariff": "0.35",
      "amenities": "WiFi, Restroom, Cafe",
      "distanceKm": 2.5,
      "averageRating": 4.5,
      "totalReviews": 120,
      "totalStations": 5,
      "totalChargers": 15,
      "availableChargers": 8,
      "stations": [
        {
          "recId": "station-guid-456",
          "chargingPointId": "CP001",
          "chargePointName": "Station A",
          "chargingGunCount": 3,
          "chargingStationImage": "https://example.com/station1.jpg",
          "totalChargers": 3,
          "availableChargers": 2,
          "chargers": [
            {
              "chargePointId": "CP001",
              "connectorId": 1,
              "connectorName": "Gun 1",
              "lastStatus": "Available",
              "lastStatusTime": "2024-01-22T10:30:00Z",
              "lastMeter": "1250.50",
              "lastMeterTime": "2024-01-22T10:30:00Z",
              "stationRecId": "station-guid-456",
              "chargePointName": "Station A"
            },
            {
              "chargePointId": "CP001",
              "connectorId": 2,
              "connectorName": "Gun 2",
              "lastStatus": "Charging",
              "lastStatusTime": "2024-01-22T10:15:00Z",
              "lastMeter": "1320.75",
              "lastMeterTime": "2024-01-22T10:15:00Z",
              "stationRecId": "station-guid-456",
              "chargePointName": "Station A"
            }
          ]
        }
      ]
    }
  ],
  "totalCount": 3,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 1
}
```

## Use Cases

### 1. Find Nearby Charging Hubs

Search for all hubs within 10km with available chargers:

```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "latitude": 40.7128,
  "longitude": -74.0060,
  "radiusKm": 10,
  "hasAvailableChargers": true,
  "sortBy": "Distance",
  "sortOrder": "Asc"
}
```

### 2. Search by City

Find all hubs in a specific city:

```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "city": "Los Angeles",
  "sortBy": "Rating",
  "sortOrder": "Desc"
}
```

### 3. Search by Text

Search for hubs containing "fast" or "rapid" in any field:

```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "searchTerm": "fast",
  "sortBy": "AvailableChargers",
  "sortOrder": "Desc"
}
```

### 4. Find Hubs with Specific Charger Status

Find hubs with available Type-A chargers:

```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "chargerStatus": "Available",
  "hasAvailableChargers": true,
  "sortBy": "Distance",
  "sortOrder": "Asc"
}
```

### 5. Browse All Hubs

Get paginated list of all hubs:

```json
{
  "pageNumber": 1,
  "pageSize": 20,
  "sortBy": "Name",
  "sortOrder": "Asc"
}
```

## Response Fields

### Hub Level
| Field | Type | Description |
|-------|------|-------------|
| `recId` | string | Unique hub identifier |
| `chargingHubName` | string | Name of the charging hub |
| `addressLine1` | string | Primary address |
| `city` | string | City |
| `state` | string | State/Province |
| `pincode` | string | Postal/ZIP code |
| `latitude` | string | Geographic latitude |
| `longitude` | string | Geographic longitude |
| `chargingHubImage` | string | Hub image URL |
| `openingTime` | string | Opening time (HH:mm:ss) |
| `closingTime` | string | Closing time (HH:mm:ss) |
| `typeATariff` | string | Type A charging rate |
| `typeBTariff` | string | Type B charging rate |
| `amenities` | string | Available amenities |
| `distanceKm` | double? | Distance from user (if location provided) |
| `averageRating` | double | Average rating (0-5) |
| `totalReviews` | int | Number of reviews |
| `totalStations` | int | Number of stations at this hub |
| `totalChargers` | int | Total number of chargers |
| `availableChargers` | int | Number of available chargers |

### Station Level
| Field | Type | Description |
|-------|------|-------------|
| `recId` | string | Unique station identifier |
| `chargingPointId` | string | Charge point ID (OCPP) |
| `chargePointName` | string | Station name |
| `chargingGunCount` | int | Configured number of guns |
| `chargingStationImage` | string | Station image URL |
| `totalChargers` | int | Total chargers at this station |
| `availableChargers` | int | Available chargers at this station |

### Charger Level
| Field | Type | Description |
|-------|------|-------------|
| `chargePointId` | string | Charge point ID |
| `connectorId` | int | Connector/Gun number |
| `connectorName` | string | Connector/Gun name |
| `lastStatus` | string | Current status (Available, Charging, Faulted, etc.) |
| `lastStatusTime` | DateTime | Last status update time |
| `lastMeter` | string | Last meter reading (kWh) |
| `lastMeterTime` | DateTime? | Last meter reading time |
| `stationRecId` | string | Parent station ID |
| `chargePointName` | string | Charge point name |

## Status Codes

| Code | Description |
|------|-------------|
| 200 | Success - Returns hub list |
| 400 | Bad Request - Invalid parameters |
| 500 | Internal Server Error |

## Examples

### Example 1: cURL - Find Nearby Hubs

```bash
curl -X POST "https://localhost:5001/api/ChargingHub/comprehensive-list" \
  -H "Content-Type: application/json" \
  -d '{
    "pageNumber": 1,
    "pageSize": 10,
    "latitude": 40.7128,
    "longitude": -74.0060,
    "radiusKm": 5,
    "hasAvailableChargers": true,
    "sortBy": "Distance",
    "sortOrder": "Asc"
  }'
```

### Example 2: JavaScript/Fetch

```javascript
const searchRequest = {
  pageNumber: 1,
  pageSize: 10,
  latitude: 40.7128,
  longitude: -74.0060,
  radiusKm: 5,
  hasAvailableChargers: true,
  sortBy: "Distance",
  sortOrder: "Asc"
};

fetch('https://localhost:5001/api/ChargingHub/comprehensive-list', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json'
  },
  body: JSON.stringify(searchRequest)
})
.then(response => response.json())
.then(data => {
  console.log('Found hubs:', data.hubs.length);
  data.hubs.forEach(hub => {
    console.log(`${hub.chargingHubName}: ${hub.availableChargers}/${hub.totalChargers} available`);
  });
});
```

### Example 3: C# Client

```csharp
using var httpClient = new HttpClient();
var request = new
{
    PageNumber = 1,
    PageSize = 10,
    Latitude = 40.7128,
    Longitude = -74.0060,
    RadiusKm = 5,
    HasAvailableChargers = true,
    SortBy = "Distance",
    SortOrder = "Asc"
};

var json = JsonSerializer.Serialize(request);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await httpClient.PostAsync(
    "https://localhost:5001/api/ChargingHub/comprehensive-list",
    content
);

var result = await response.Content.ReadFromJsonAsync<ChargingHubComprehensiveResponseDto>();
Console.WriteLine($"Found {result.TotalCount} hubs");
```

## Performance Considerations

### Optimization Tips

1. **Use Pagination**: Keep `pageSize` reasonable (10-50) for better performance
2. **Location Filtering**: Provide latitude/longitude and radius to reduce dataset
3. **Specific Filters**: Use city/state filters instead of broad searches
4. **Limit Radius**: Smaller radius values improve performance
5. **Status Filtering**: Filter by charger status to reduce data volume

### Response Size

- **Minimal** (1 hub, 2 stations, 6 chargers): ~2-3 KB
- **Typical** (10 hubs, 30 stations, 90 chargers): ~50-70 KB
- **Large** (50 hubs, 150 stations, 450 chargers): ~200-300 KB

## Data Hierarchy

```
ChargingHub (Level 1)
├── Basic Info (name, address, location, hours, tariff, amenities)
├── Statistics (distance, rating, reviews, station/charger counts)
└── Stations[] (Level 2)
    ├── Station Info (name, charge point ID, image)
    ├── Statistics (total/available chargers)
    └── Chargers[] (Level 3)
        └── Charger Info (connector ID, name, status, meter reading)
```

## Filtering Logic

### Text Search (`searchTerm`)
Searches in:
- Hub name (case-insensitive)
- City (case-insensitive)
- State (case-insensitive)
- Address line 1 (case-insensitive)

### Location Search
- Calculates distance using Haversine formula
- Filters by `radiusKm` if provided
- Sorts by distance when `sortBy = "Distance"`

### Charger Status Filter
- Filters individual chargers by status
- Affects station-level `availableChargers` count
- Affects hub-level `totalChargers` and `availableChargers` counts

### Available Chargers Filter
- When `hasAvailableChargers = true`
- Only returns hubs with at least one available charger
- Applied after all other filters

## Integration Examples

### Mobile App - Map View

```javascript
// Get user location
navigator.geolocation.getCurrentPosition(async (position) => {
  const response = await fetch('/api/ChargingHub/comprehensive-list', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      radiusKm: 10,
      hasAvailableChargers: true,
      pageSize: 50,
      sortBy: "Distance"
    })
  });
  
  const data = await response.json();
  
  // Plot on map
  data.hubs.forEach(hub => {
    addMarkerToMap({
      lat: parseFloat(hub.latitude),
      lng: parseFloat(hub.longitude),
      title: hub.chargingHubName,
      info: `${hub.availableChargers}/${hub.totalChargers} available`,
      distance: hub.distanceKm
    });
  });
});
```

### Web Dashboard - Hub Management

```javascript
// Load hub list with stations and chargers
async function loadHubDashboard(page = 1) {
  const response = await fetch('/api/ChargingHub/comprehensive-list', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      pageNumber: page,
      pageSize: 20,
      sortBy: "Name",
      sortOrder: "Asc"
    })
  });
  
  const data = await response.json();
  
  // Render hub cards
  data.hubs.forEach(hub => {
    const hubCard = createHubCard(hub);
    hub.stations.forEach(station => {
      const stationRow = createStationRow(station);
      station.chargers.forEach(charger => {
        const chargerBadge = createChargerBadge(charger);
        stationRow.appendChild(chargerBadge);
      });
      hubCard.appendChild(stationRow);
    });
    document.getElementById('hub-list').appendChild(hubCard);
  });
  
  // Update pagination
  updatePagination(data.pageNumber, data.totalPages);
}
```

## Best Practices

### For API Consumers

1. **Cache Results**: Cache responses for a few minutes to reduce server load
2. **Incremental Loading**: Load additional pages on demand (infinite scroll)
3. **Progressive Display**: Show hubs first, then load stations/chargers on expand
4. **Location Permission**: Request user location for better results
5. **Error Handling**: Handle network errors and empty results gracefully

### For Mobile Apps

1. **Battery Optimization**: Don't continuously poll for updates
2. **Offline Support**: Cache last known hub data
3. **Background Updates**: Update charger status periodically when app is active
4. **Data Compression**: Use gzip compression for API requests

### For Web Apps

1. **Lazy Loading**: Load charger details when station is expanded
2. **Virtual Scrolling**: Use virtual scrolling for large lists
3. **Debounce Search**: Debounce search input to avoid excessive requests
4. **Loading States**: Show skeleton screens while loading

## Troubleshooting

### No Results Returned

Check:
- Are there active hubs in the database?
- Is the radius too small?
- Are filters too restrictive?
- Is the location correct?

### Slow Performance

Optimize by:
- Reducing page size
- Adding location filters
- Using specific city/state filters
- Limiting radius

### Missing Charger Data

Verify:
- Chargers are marked as Active = 1
- ChargePointId matches between Station and Connector
- OCPP connection is working for status updates

## API Versioning

Current Version: **v1**

Future enhancements may include:
- Real-time availability using WebSocket
- Booking/reservation status
- Price comparisons
- EV compatibility filtering
- Route planning integration

## Support

For issues or questions:
- Check application logs
- Verify database connectivity
- Test with minimal filters first
- Check OCPP server connectivity

---

**Last Updated**: 2024-01-22  
**API Version**: 1.0  
**Endpoint**: `/api/ChargingHub/comprehensive-list`
