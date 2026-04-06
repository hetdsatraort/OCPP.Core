# OCPI Quick Reference Guide

## Base URLs

- **Local Development**: `https://localhost:5001/ocpi`
- **Production**: Configure in appsettings.json

## Quick Start

### 1. Start the Service
```bash
cd OCPI.Core.Roaming
dotnet run
```

### 2. Test Version Endpoint
```bash
curl https://localhost:5001/ocpi/versions
```

### 3. View Swagger Documentation
Open browser: `https://localhost:5001`

## Common Operations

### Get Supported Versions
```http
GET https://localhost:5001/ocpi/versions
```

### Get Version Details
```http
GET https://localhost:5001/ocpi/versions/2.2.1
```

### Register Credentials
```http
POST https://localhost:5001/ocpi/2.2.1/credentials
Content-Type: application/json

{
  "token": "partner-token",
  "url": "https://partner.com/ocpi/versions"
}
```

### Create Location
```http
PUT https://localhost:5001/ocpi/2.2.1/locations/LOC001
Content-Type: application/json

{
  "id": "LOC001",
  "type": "ON_STREET",
  "name": "Test Hub",
  "address": "123 Test St",
  "city": "Test City",
  "country": "US",
  "coordinates": {
    "latitude": 37.7749,
    "longitude": -122.4194
  },
  "evses": [],
  "lastUpdated": "2026-04-06T10:00:00Z"
}
```

### Start Session
```http
POST https://localhost:5001/ocpi/2.2.1/sessions/start
Content-Type: application/json

{
  "locationId": "LOC001",
  "evseUid": "EVSE001",
  "connectorId": "1",
  "tokenUid": "USER123"
}
```

## Response Codes

| Code | Meaning |
|------|---------|
| 1000 | Success |
| 2000 | Server error |
| 2001 | Invalid parameters |
| 3001 | Not found |
| 3003 | Unsupported version |

## Configuration

Edit `appsettings.json`:
```json
{
  "OCPI": {
    "BaseUrl": "https://localhost:5001/ocpi",
    "CountryCode": "US",
    "PartyId": "CPO"
  }
}
```

## Service Architecture

```
Controllers → Services → Data Layer
     ↓           ↓            ↓
   HTTP      Business     Storage
  Requests    Logic      (In-Memory)
```

## Next Steps

1. ✅ Test endpoints using Swagger UI
2. ⏳ Integrate with database (replace in-memory storage)
3. ⏳ Add authentication middleware
4. ⏳ Implement remaining modules (CDR, Tariffs, Tokens)
5. ⏳ Add webhook support
6. ⏳ Connect to OCPP stations

## Troubleshooting

### Common Issues

1. **Certificate errors**: Use `dotnet dev-certs https --trust`
2. **Port already in use**: Change ports in appsettings.json
3. **No data returned**: Data is in-memory, restart clears it

### Logs

Check console output for detailed OCPI operation logs.
