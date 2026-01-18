# Charging Hub Management APIs - Documentation

This document describes the comprehensive API endpoints for managing charging hubs, charging stations, chargers (guns/connectors), and reviews.

## Architecture Overview

**Hierarchy:**
```
ChargingHub (Location with multiple stations)
  └── ChargingStation (Corresponds to ChargePoint)
      └── Charger/Gun (Corresponds to Connector)
```

**Key Relationships:**
- A **ChargingHub** is a physical location (address, coordinates)
- A **ChargingStation** links to a **ChargePoint** in the OCPP system
- **Chargers** (guns) are **ConnectorStatus** entries in the OCPP system
- **Reviews** can be for either hubs or specific stations

---

## Charging Hub Management APIs

### 1. Add Charging Hub
**Endpoint:** `POST /api/charginghub/charging-hub-add`

**Authorization:** Required

**Request Body:**
```json
{
  "addressLine1": "123 Main Street",
  "addressLine2": "Building A",
  "addressLine3": null,
  "chargingHubImage": "https://example.com/image.jpg",
  "city": "San Francisco",
  "state": "California",
  "pincode": "94102",
  "latitude": "37.7749",
  "longitude": "-122.4194",
  "openingTime": "08:00:00",
  "closingTime": "20:00:00",
  "typeATariff": "0.30",
  "typeBTariff": "0.50",
  "amenities": "WiFi,Restroom,Cafe",
  "additionalInfo1": "24/7 Security",
  "additionalInfo2": null,
  "additionalInfo3": null
}
```

**Response:**
```json
{
  "success": true,
  "message": "Charging hub added successfully",
  "hub": {
    "recId": "guid",
    "addressLine1": "123 Main Street",
    "city": "San Francisco",
    "state": "California",
    "latitude": "37.7749",
    "longitude": "-122.4194",
    "openingTime": "08:00:00",
    "closingTime": "20:00:00",
    "stationCount": 0,
    "averageRating": null
  }
}
```

### 2. Update Charging Hub
**Endpoint:** `PUT /api/charginghub/charging-hub-update`

**Authorization:** Required

**Request Body:**
```json
{
  "recId": "hub-guid",
  "addressLine1": "123 Main Street Updated",
  "city": "San Francisco",
  "state": "California",
  "pincode": "94102",
  "latitude": "37.7749",
  "longitude": "-122.4194",
  "openingTime": "07:00:00",
  "closingTime": "22:00:00",
  "typeATariff": "0.35",
  "typeBTariff": "0.55"
}
```

### 3. Delete Charging Hub
**Endpoint:** `DELETE /api/charginghub/charging-hub-delete/{hubId}`

**Authorization:** Required

**Note:** Performs soft delete and also soft deletes all associated stations.

### 4. Get Charging Hub List
**Endpoint:** `GET /api/charginghub/charging-hub-list`

**Authorization:** None (Public)

**Query Parameters:**
- `pageNumber` (default: 1)
- `pageSize` (default: 10)

**Response:**
```json
{
  "success": true,
  "message": "Charging hubs retrieved successfully",
  "hubs": [
    {
      "recId": "guid",
      "city": "San Francisco",
      "state": "California",
      "latitude": "37.7749",
      "longitude": "-122.4194",
      "stationCount": 5,
      "averageRating": 4.5,
      "amenities": "WiFi,Restroom,Cafe"
    }
  ],
  "totalCount": 50
}
```

### 5. Get Charging Hub Details
**Endpoint:** `GET /api/charginghub/charging-hub-details/{hubId}`

**Authorization:** None (Public)

**Response:**
```json
{
  "success": true,
  "message": "Charging hub details retrieved successfully",
  "hub": {
    "recId": "guid",
    "addressLine1": "123 Main Street",
    "city": "San Francisco",
    "openingTime": "08:00:00",
    "closingTime": "20:00:00"
  },
  "stations": [
    {
      "recId": "station-guid",
      "chargingPointId": "CP001",
      "chargingGunCount": 2,
      "chargePointName": "Station A"
    }
  ],
  "reviews": [...],
  "averageRating": 4.5,
  "totalReviews": 23
}
```

### 6. Search Charging Hubs by Location
**Endpoint:** `POST /api/charginghub/charging-hub-search`

**Authorization:** None (Public)

**Request Body:**
```json
{
  "latitude": 37.7749,
  "longitude": -122.4194,
  "radiusKm": 10.0
}
```

**Response:**
```json
{
  "success": true,
  "message": "Found 8 charging hubs within 10km",
  "hubs": [
    {
      "recId": "guid",
      "city": "San Francisco",
      "distanceKm": 2.35,
      "stationCount": 5,
      "averageRating": 4.5
    },
    {
      "recId": "guid2",
      "city": "Oakland",
      "distanceKm": 8.92,
      "stationCount": 3,
      "averageRating": 4.2
    }
  ],
  "totalCount": 8
}
```

**Note:** Results are ordered by distance (closest first)

---

## Charging Station Management APIs

### 7. Add Charging Station
**Endpoint:** `POST /api/charginghub/charging-station-add`

**Authorization:** Required

**Request Body:**
```json
{
  "chargingHubId": "hub-guid",
  "chargingPointId": "CP001",
  "chargingGunCount": 2,
  "chargingStationImage": "https://example.com/station.jpg"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Charging station added successfully",
  "station": {
    "recId": "station-guid",
    "chargingPointId": "CP001",
    "chargingHubId": "hub-guid",
    "chargingGunCount": 2,
    "chargePointName": "Station A",
    "hubCity": "San Francisco"
  }
}
```

### 8. Update Charging Station
**Endpoint:** `PUT /api/charginghub/charging-station-update`

**Authorization:** Required

**Request Body:**
```json
{
  "recId": "station-guid",
  "chargingHubId": "hub-guid",
  "chargingPointId": "CP001",
  "chargingGunCount": 3,
  "chargingStationImage": "https://example.com/updated.jpg"
}
```

### 9. Delete Charging Station
**Endpoint:** `DELETE /api/charginghub/charging-station-delete/{stationId}`

**Authorization:** Required

### 10. Get Charging Station List (per Hub)
**Endpoint:** `GET /api/charginghub/charging-station-list/{hubId}`

**Authorization:** None (Public)

**Response:**
```json
{
  "success": true,
  "message": "Charging stations retrieved successfully",
  "stations": [
    {
      "recId": "station-guid",
      "chargingPointId": "CP001",
      "chargingGunCount": 2,
      "chargePointName": "Station A"
    }
  ],
  "totalCount": 5
}
```

### 11. Get Charging Station Details
**Endpoint:** `GET /api/charginghub/charging-station-details/{stationId}`

**Authorization:** None (Public)

**Response:**
```json
{
  "success": true,
  "message": "Charging station details retrieved successfully",
  "station": {
    "recId": "station-guid",
    "chargingPointId": "CP001",
    "chargingGunCount": 2
  },
  "chargers": [
    {
      "chargePointId": "CP001",
      "connectorId": 1,
      "connectorName": "Gun 1",
      "lastStatus": "Available"
    }
  ],
  "reviews": [...]
}
```

---

## Charger/Gun Management APIs

### 12. Add Charger (Gun/Connector)
**Endpoint:** `POST /api/charginghub/chargers-add`

**Authorization:** Required

**Request Body:**
```json
{
  "chargePointId": "CP001",
  "connectorId": 1,
  "connectorName": "Gun 1 - Type 2"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Charger added successfully",
  "charger": {
    "chargePointId": "CP001",
    "connectorId": 1,
    "connectorName": "Gun 1 - Type 2",
    "lastStatus": "Available",
    "chargePointName": "Station A"
  }
}
```

### 13. Update Charger
**Endpoint:** `PUT /api/charginghub/chargers-update`

**Authorization:** Required

**Request Body:**
```json
{
  "chargePointId": "CP001",
  "connectorId": 1,
  "connectorName": "Gun 1 - Type 2 Updated",
  "lastStatus": "Charging"
}
```

### 14. Delete Charger
**Endpoint:** `DELETE /api/charginghub/chargers-delete/{chargePointId}/{connectorId}`

**Authorization:** Required

**Example:** `DELETE /api/charginghub/chargers-delete/CP001/1`

### 15. Get Charger List (per Station)
**Endpoint:** `GET /api/charginghub/charger-list/{stationId}`

**Authorization:** None (Public)

**Response:**
```json
{
  "success": true,
  "message": "Chargers retrieved successfully",
  "chargers": [
    {
      "chargePointId": "CP001",
      "connectorId": 1,
      "connectorName": "Gun 1",
      "lastStatus": "Available",
      "lastStatusTime": "2024-01-15T10:30:00Z"
    },
    {
      "chargePointId": "CP001",
      "connectorId": 2,
      "connectorName": "Gun 2",
      "lastStatus": "Charging",
      "lastStatusTime": "2024-01-15T09:15:00Z"
    }
  ],
  "totalCount": 2
}
```

### 16. Get Charger Details
**Endpoint:** `GET /api/charginghub/charger-details/{chargePointId}/{connectorId}`

**Authorization:** None (Public)

**Example:** `GET /api/charginghub/charger-details/CP001/1`

**Response:**
```json
{
  "success": true,
  "message": "Charger details retrieved successfully",
  "charger": {
    "chargePointId": "CP001",
    "connectorId": 1,
    "connectorName": "Gun 1 - Type 2",
    "lastStatus": "Available",
    "lastStatusTime": "2024-01-15T10:30:00Z",
    "lastMeter": 12345.67,
    "lastMeterTime": "2024-01-15T10:30:00Z"
  }
}
```

---

## Review Management APIs

### 17. Add Hub Review
**Endpoint:** `POST /api/charginghub/charging-hub-review-add`

**Authorization:** Required

**Request Body:**
```json
{
  "chargingHubId": "hub-guid",
  "chargingStationId": null,
  "rating": 5,
  "description": "Great location with lots of amenities!",
  "reviewImage1": "https://example.com/review1.jpg",
  "reviewImage2": null,
  "reviewImage3": null,
  "reviewImage4": null
}
```

### 18. Add Station Review
**Endpoint:** `POST /api/charginghub/charging-stn-review-add`

**Authorization:** Required

**Request Body:**
```json
{
  "chargingHubId": "hub-guid",
  "chargingStationId": "station-guid",
  "rating": 4,
  "description": "Fast charging, worked perfectly!",
  "reviewImage1": null
}
```

### 19. Update Review
**Endpoint:** `PUT /api/charginghub/charging-hub-review-update`

**Authorization:** Required

**Request Body:**
```json
{
  "recId": "review-guid",
  "chargingHubId": "hub-guid",
  "rating": 5,
  "description": "Updated review - even better than before!"
}
```

### 20. Delete Review
**Endpoint:** `DELETE /api/charginghub/charging-hub-review-delete/{reviewId}`

**Authorization:** Required

### 21. Get Hub Review List
**Endpoint:** `GET /api/charginghub/charging-hub-review-list/{hubId}`

**Authorization:** None (Public)

**Response:**
```json
{
  "success": true,
  "message": "Reviews retrieved successfully",
  "reviews": [
    {
      "recId": "review-guid",
      "chargingHubId": "hub-guid",
      "rating": 5,
      "description": "Great location!",
      "reviewTime": "2024-01-15T10:00:00Z"
    }
  ],
  "totalCount": 23,
  "averageRating": 4.5
}
```

---

## cURL Examples

### Search Hubs by Location
```bash
curl -X POST http://localhost:8082/api/charginghub/charging-hub-search \
  -H "Content-Type: application/json" \
  -d '{
    "latitude": 37.7749,
    "longitude": -122.4194,
    "radiusKm": 10.0
  }'
```

### Add Charging Hub
```bash
curl -X POST http://localhost:8082/api/charginghub/charging-hub-add \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{
    "addressLine1": "123 Main St",
    "city": "San Francisco",
    "state": "California",
    "pincode": "94102",
    "latitude": "37.7749",
    "longitude": "-122.4194",
    "openingTime": "08:00:00",
    "closingTime": "20:00:00"
  }'
```

### Add Station to Hub
```bash
curl -X POST http://localhost:8082/api/charginghub/charging-station-add \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{
    "chargingHubId": "hub-guid",
    "chargingPointId": "CP001",
    "chargingGunCount": 2
  }'
```

### Add Charger/Gun
```bash
curl -X POST http://localhost:8082/api/charginghub/chargers-add \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{
    "chargePointId": "CP001",
    "connectorId": 1,
    "connectorName": "Gun 1 - Type 2"
  }'
```

### Add Review
```bash
curl -X POST http://localhost:8082/api/charginghub/charging-hub-review-add \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d '{
    "chargingHubId": "hub-guid",
    "rating": 5,
    "description": "Excellent service!"
  }'
```

### Get Nearby Hubs
```bash
curl -X POST http://localhost:8082/api/charginghub/charging-hub-search \
  -H "Content-Type: application/json" \
  -d '{"latitude": 37.7749, "longitude": -122.4194, "radiusKm": 5}'
```

---

## JavaScript/Fetch Examples

### Search for Nearby Charging Hubs
```javascript
const searchNearbyHubs = async (lat, lng, radius) => {
  const response = await fetch('http://localhost:8082/api/charginghub/charging-hub-search', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      latitude: lat,
      longitude: lng,
      radiusKm: radius
    })
  });
  
  const data = await response.json();
  return data.hubs;
};

// Usage
const nearbyHubs = await searchNearbyHubs(37.7749, -122.4194, 10);
```

### Add Charging Hub
```javascript
const addHub = async (hubData) => {
  const response = await fetch('http://localhost:8082/api/charginghub/charging-hub-add', {
    method: 'POST',
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(hubData)
  });
  
  return await response.json();
};
```

### Get Hub Details with Stations
```javascript
const getHubDetails = async (hubId) => {
  const response = await fetch(
    `http://localhost:8082/api/charginghub/charging-hub-details/${hubId}`
  );
  
  const data = await response.json();
  return {
    hub: data.hub,
    stations: data.stations,
    reviews: data.reviews,
    rating: data.averageRating
  };
};
```

---

## Key Features

### 1. **Location-Based Search**
- Uses Haversine formula for accurate distance calculation
- Search by radius in kilometers
- Results ordered by distance

### 2. **Integrated OCPP System**
- Stations link to ChargePoints
- Chargers map to ConnectorStatus
- Real-time status from OCPP protocol

### 3. **Review System**
- Reviews for both hubs and stations
- Average rating calculations
- Multiple review images support

### 4. **Soft Delete**
- All entities use soft delete (Active = 0)
- Maintains referential integrity
- Cascade delete for hub -> stations

### 5. **Public vs Protected**
- List/search endpoints are public (AllowAnonymous)
- Add/Update/Delete require authentication
- Easy to add role-based restrictions

---

## Common Use Cases

### Mobile App - Find Nearest Charger
```
1. GET user's GPS location
2. POST /charging-hub-search with location + radius
3. Display hubs ordered by distance
4. GET /charging-hub-details/{id} for selected hub
5. Show stations, available chargers, reviews
```

### Admin - Setup New Location
```
1. POST /charging-hub-add (create hub)
2. POST /charging-station-add (add station to hub)
3. POST /chargers-add (add guns to station)
4. Verify with GET /charging-hub-details/{id}
```

### User - Leave Review
```
1. After charging session completes
2. POST /charging-hub-review-add or /charging-stn-review-add
3. Include rating, description, optional images
```

---

## Error Responses

All endpoints return standardized error responses:

```json
{
  "success": false,
  "message": "Error description"
}
```

**Common HTTP Status Codes:**
- `200 OK` - Success
- `400 Bad Request` - Invalid input
- `401 Unauthorized` - Authentication required
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

---

## Notes

1. **Time Format:** Use `HH:mm:ss` format for opening/closing times
2. **Coordinates:** Latitude/Longitude as strings in the database
3. **Tariffs:** Store as strings (e.g., "0.30" for $0.30/kWh)
4. **Images:** Store URLs, not binary data
5. **Amenities:** Comma-separated string (e.g., "WiFi,Restroom,Cafe")
6. **Reviews:** Can be for hub only, station only, or both
7. **Distance:** Always returned in kilometers when using location search
