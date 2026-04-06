# OCPI Roaming Implementation

## Overview

This project implements the **OCPI (Open Charge Point Interface) v2.2.1** protocol for EV charging roaming using the **OCPI.Net** NuGet package. The implementation enables seamless integration with other charging networks and e-Mobility Service Providers (eMSPs).

## Features

### Implemented OCPI Modules

- ✅ **Versions** - Version information and endpoint discovery
- ✅ **Credentials** - Credential registration and management
- ✅ **Locations** - Charging location and EVSE information
- ✅ **Sessions** - Charging session management
- ⏳ **CDRs** - Charge Detail Records (structure ready, service pending)
- ⏳ **Tariffs** - Pricing information (structure ready, service pending)
- ⏳ **Tokens** - Authorization tokens (structure ready, service pending)

## Project Structure

```
OCPI.Core.Roaming/
├── Controllers/
│   ├── OcpiVersionsController.cs      # Version endpoints
│   ├── OcpiCredentialsController.cs   # Credentials management
│   ├── OcpiLocationsController.cs     # Location/EVSE/Connector endpoints
│   └── OcpiSessionsController.cs      # Session management
├── Models/
│   └── OCPI/
│       ├── OcpiCredentialsDto.cs      # Credentials DTOs
│       ├── OcpiLocationDto.cs         # Location/EVSE/Connector DTOs
│       ├── OcpiSessionDto.cs          # Session DTOs
│       ├── OcpiTariffDto.cs           # Tariff DTOs
│       ├── OcpiCdrDto.cs              # CDR DTOs
│       ├── OcpiTokenDto.cs            # Token DTOs
│       ├── OcpiVersionDto.cs          # Version DTOs
│       └── OcpiCommandDto.cs          # Command DTOs
├── Services/
│   ├── Interfaces/
│   │   └── IOcpiServices.cs           # Service interfaces
│   ├── OcpiLocationService.cs         # Location service implementation
│   ├── OcpiSessionService.cs          # Session service implementation
│   ├── OcpiCredentialsService.cs      # Credentials service implementation
│   └── OcpiVersionService.cs          # Version service implementation
├── Program.cs                         # Application configuration
├── appsettings.json                   # Configuration settings
└── OCPI_README.md                     # This file
```

## Configuration

### appsettings.json

```json
{
  "OCPI": {
    "BaseUrl": "https://localhost:5001/ocpi",
    "Token": "YOUR_OCPI_TOKEN_HERE",
    "CountryCode": "US",
    "PartyId": "CPO",
    "BusinessName": "EV Charging Platform",
    "Website": "https://evcharging.com",
    "Version": "2.2.1"
  }
}
```

### Configuration Parameters

- **BaseUrl**: Base URL for your OCPI endpoints
- **Token**: Authorization token for OCPI communication
- **CountryCode**: ISO 3166-1 alpha-2 country code (e.g., "US", "GB", "NL")
- **PartyId**: CPO (Charge Point Operator) or EMSP (e-Mobility Service Provider) identifier
- **BusinessName**: Your organization name
- **Website**: Your organization website

## API Endpoints

### Version Endpoints

```
GET /ocpi/versions
GET /ocpi/versions/{version}
```

### Credentials Endpoints (v2.2.1)

```
GET    /ocpi/2.2.1/credentials
POST   /ocpi/2.2.1/credentials
PUT    /ocpi/2.2.1/credentials
DELETE /ocpi/2.2.1/credentials
```

### Location Endpoints (v2.2.1)

```
GET /ocpi/2.2.1/locations
GET /ocpi/2.2.1/locations/{locationId}
PUT /ocpi/2.2.1/locations/{locationId}
GET /ocpi/2.2.1/locations/{locationId}/{evseUid}
GET /ocpi/2.2.1/locations/{locationId}/{evseUid}/{connectorId}
```

### Session Endpoints (v2.2.1)

```
GET  /ocpi/2.2.1/sessions
GET  /ocpi/2.2.1/sessions/{sessionId}
PUT  /ocpi/2.2.1/sessions/{sessionId}
POST /ocpi/2.2.1/sessions/start
POST /ocpi/2.2.1/sessions/stop
```

## Usage Examples

### 1. Register Credentials

```bash
POST /ocpi/2.2.1/credentials
Content-Type: application/json

{
  "token": "partner-token-12345",
  "url": "https://partner.com/ocpi/versions",
  "businessDetails": {
    "name": "Partner Company",
    "website": "https://partner.com"
  },
  "countryCode": "US",
  "partyId": "EMSP"
}
```

### 2. Get Versions

```bash
GET /ocpi/versions
```

Response:
```json
{
  "statusCode": 1000,
  "statusMessage": "Success",
  "data": [
    {
      "version": "2.2.1",
      "url": "https://localhost:5001/ocpi/2.2.1"
    }
  ],
  "timestamp": "2026-04-06T10:30:00Z"
}
```

### 3. Create/Update Location

```bash
PUT /ocpi/2.2.1/locations/LOC001
Content-Type: application/json

{
  "id": "LOC001",
  "type": "ON_STREET",
  "name": "Main Street Charging Hub",
  "address": "123 Main Street",
  "city": "San Francisco",
  "postalCode": "94102",
  "country": "USA",
  "coordinates": {
    "latitude": 37.7749,
    "longitude": -122.4194
  },
  "evses": [
    {
      "uid": "EVSE001",
      "status": "AVAILABLE",
      "connectors": [
        {
          "id": "1",
          "standard": "IEC_62196_T2",
          "format": "SOCKET",
          "powerType": "AC_3_PHASE",
          "maxVoltage": 230,
          "maxAmperage": 32,
          "maxElectricPower": 22000,
          "lastUpdated": "2026-04-06T10:00:00Z"
        }
      ],
      "lastUpdated": "2026-04-06T10:00:00Z"
    }
  ],
  "lastUpdated": "2026-04-06T10:00:00Z"
}
```

### 4. Start Charging Session

```bash
POST /ocpi/2.2.1/sessions/start
Content-Type: application/json

{
  "locationId": "LOC001",
  "evseUid": "EVSE001",
  "connectorId": "1",
  "tokenUid": "USER123",
  "authorizationReference": "AUTH-REF-456"
}
```

### 5. Stop Charging Session

```bash
POST /ocpi/2.2.1/sessions/stop
Content-Type: application/json

{
  "sessionId": "SESSION-UUID-HERE"
}
```

## OCPI Response Format

All OCPI responses follow the standard format:

```json
{
  "statusCode": 1000,
  "statusMessage": "Success",
  "data": { /* response data */ },
  "timestamp": "2026-04-06T10:30:00Z"
}
```

### OCPI Status Codes

- **1000**: Success
- **2000**: Generic server error
- **2001**: Invalid or missing parameters
- **3001**: Resource not found
- **3003**: Unsupported version

## Development

### Running the Application

```bash
cd OCPI.Core.Roaming
dotnet restore
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `https://localhost:5001`

### Testing with Swagger

1. Navigate to `https://localhost:5001`
2. Explore the available endpoints
3. Use the "Try it out" feature to test endpoints
4. Add the Authorization token in the "OCPI-Token" section

## Integration with OCPP Management

This OCPI implementation works alongside the existing OCPP.Core.Management system:

- **OCPP.Core.Management**: Manages charging stations via OCPP protocol
- **OCPI.Core.Roaming**: Exposes charging infrastructure to external networks via OCPI

### Data Synchronization

To integrate with your existing charging infrastructure:

1. **Locations**: Map `ChargingHub` → `OcpiLocationDto`
2. **EVSEs**: Map `ChargingStation` → `OcpiEvseDto`
3. **Connectors**: Map `ChargingGuns` → `OcpiConnectorDto`
4. **Sessions**: Map `ChargingSession` → `OcpiSessionDto`

Example service enhancement:
```csharp
public class OcpiLocationService : IOcpiLocationService
{
    private readonly OCPPCoreContext _dbContext;
    
    public async Task<List<OcpiLocationDto>> GetLocationsAsync()
    {
        var hubs = await _dbContext.ChargingHubs.ToListAsync();
        return hubs.Select(MapToOcpiLocation).ToList();
    }
}
```

## Security

### Authentication

OCPI uses token-based authentication. Include the token in the Authorization header:

```
Authorization: Token YOUR_OCPI_TOKEN_HERE
```

### Best Practices

1. Use HTTPS in production
2. Rotate tokens regularly
3. Validate all incoming requests
4. Implement rate limiting
5. Log all OCPI transactions
6. Monitor for suspicious activity

## Next Steps

### Recommended Enhancements

1. **Database Integration**: Connect services to your database instead of in-memory storage
2. **CDR Service**: Implement complete CDR generation and management
3. **Tariff Service**: Add tariff calculation and management
4. **Token Service**: Implement token authorization and whitelisting
5. **Commands Module**: Add remote start/stop/unlock commands
6. **Webhooks**: Implement push notifications for real-time updates
7. **Background Jobs**: Add periodic synchronization tasks
8. **Authentication Middleware**: Implement OCPI token validation middleware

### Database Integration Example

```csharp
public class OcpiLocationService : IOcpiLocationService
{
    private readonly OCPPCoreContext _dbContext;
    
    public async Task<OcpiLocationDto> GetLocationByIdAsync(string locationId)
    {
        var hub = await _dbContext.ChargingHubs
            .Include(h => h.ChargingStations)
            .ThenInclude(s => s.ChargingGuns)
            .FirstOrDefaultAsync(h => h.RecId == locationId);
            
        return MapToOcpiLocation(hub);
    }
}
```

## References

- [OCPI.Net Documentation](https://bitzart.github.io/OCPI.Net/)
- [OCPI.Net GitHub](https://github.com/BitzArt/OCPI.Net)
- [OCPI Specification](https://github.com/ocpi/ocpi)
- [OCPI v2.2.1 Specification](https://github.com/ocpi/ocpi/tree/2.2.1)

## License

This implementation follows the same license as the parent OCPP.Core project.

## Support

For issues or questions:
1. Check the OCPI.Net documentation
2. Review the OCPI specification
3. Consult the sample application in the OCPI.Net repository
4. Check the logs for detailed error messages

---

**Version**: 1.0  
**OCPI Version**: 2.2.1  
**Last Updated**: April 6, 2026
