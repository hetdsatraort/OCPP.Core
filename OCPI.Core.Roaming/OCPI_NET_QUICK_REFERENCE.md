# OCPI.Net Quick Reference

## 🚀 Quick Start Commands

```bash
# Run the application
cd OCPI.Core.Roaming
dotnet run

# Access Swagger UI
https://localhost:6101

# Test health
GET https://localhost:6101/versions
```

## 📡 OCPI Endpoints

### 1. Versions (Public - No Auth Required)

```http
GET /versions
GET /versions/2.2.1
```

### 2. Credentials (Receiver Role)

```http
GET /2.2.1/credentials
POST /2.2.1/credentials
PUT /2.2.1/credentials
DELETE /2.2.1/credentials

Header: Authorization: Token <your-partner-token>
```

### 3. Locations (Sender Role)

```http
GET /2.2.1/locations
GET /2.2.1/locations/{countryCode}/{partyId}/{locationId}
GET /2.2.1/locations/{countryCode}/{partyId}/{locationId}/{evseUid}
GET /2.2.1/locations/{countryCode}/{partyId}/{locationId}/{evseUid}/{connectorId}

Header: Authorization: Token <your-partner-token>
```

### 4. Locations (Receiver Role)

```http
PUT /2.2.1/locations/receiver/{countryCode}/{partyId}/{locationId}
PATCH /2.2.1/locations/receiver/{countryCode}/{partyId}/{locationId}
PUT /2.2.1/locations/receiver/{countryCode}/{partyId}/{locationId}/{evseUid}
PUT /2.2.1/locations/receiver/{countryCode}/{partyId}/{locationId}/{evseUid}/{connectorId}

Header: Authorization: Token <your-partner-token>
```

### 5. Sessions (Receiver Role)

```http
PUT /2.2.1/sessions/{countryCode}/{partyId}/{sessionId}
PATCH /2.2.1/sessions/{countryCode}/{partyId}/{sessionId}

Header: Authorization: Token <your-partner-token>
```

### 6. CDRs (Receiver Role)

```http
POST /2.2.1/cdrs
GET /2.2.1/cdrs/{cdrId}

Header: Authorization: Token <your-partner-token>
```

### 7. Tariffs (Sender Role)

```http
GET /2.2.1/tariffs
GET /2.2.1/tariffs/{countryCode}/{partyId}/{tariffId}

Header: Authorization: Token <your-partner-token>
```

### 8. Tokens (Receiver Role)

```http
POST /2.2.1/tokens/{countryCode}/{partyId}/{tokenUid}/authorize
PUT /2.2.1/tokens/{countryCode}/{partyId}/{tokenUid}
PATCH /2.2.1/tokens/{countryCode}/{partyId}/{tokenUid}

Header: Authorization: Token <your-partner-token>
```

## 🔑 Configuration

### appsettings.json

```json
{
  "OCPI": {
    "BaseUrl": "https://your-domain.com",
    "Token": "your-secure-platform-token",
    "CountryCode": "US",
    "PartyId": "CPO",
    "BusinessName": "Your Business Name",
    "Website": "https://your-website.com"
  }
}
```

## 🎯 OCPI Roles

| Role | Description | Your Implementation |
|------|-------------|---------------------|
| **CPO** (Charge Point Operator) | Owns/operates charging stations | ✅ Locations Sender, Tariffs Sender |
| **eMSP** (e-Mobility Service Provider) | Provides EV driver services | ✅ Sessions Receiver, Tokens Receiver, CDRs Receiver |

## 🏗️ Controller Patterns

### Basic Controller

```csharp
[OcpiEndpoint(OcpiModule.ModuleName, "Sender/Receiver", "2.2.1")]
[Route("2.2.1/modulename")]
[OcpiAuthorize]
public class OcpiModuleController : OcpiController
{
    [HttpGet]
    public IActionResult Get()
    {
        var data = GetDataFromDatabase();
        return OcpiOk(data);
    }

    [HttpPut("{id}")]
    public IActionResult Put([FromRoute] string id, [FromBody] OcpiModel model)
    {
        OcpiValidate(model); // Validates using built-in validators
        SaveToDatabase(model);
        return OcpiOk(model);
    }
}
```

### Paginated Response

```csharp
[HttpGet]
public IActionResult GetAll([FromQuery] OcpiPageRequest pageRequest)
{
    SetMaxLimit(pageRequest, 100);

    var allData = GetAllDataFromDatabase();
    
    var pagedData = allData
        .Skip(pageRequest.Offset ?? 0)
        .Take(pageRequest.Limit ?? 100)
        .ToList();

    var result = new PageResult<OcpiLocation>(pagedData)
    {
        TotalCount = allData.Count,
        Offset = pageRequest.Offset ?? 0,
        Limit = pageRequest.Limit ?? 100
    };

    return OcpiOk(result); // Automatically adds pagination headers
}
```

## 🔐 Authentication

### Token Format

```
Authorization: Token YOUR_SECRET_TOKEN_HERE
```

### Configure in appsettings.json

```json
{
  "OCPI": {
    "Token": "change-this-to-secure-token"
  }
}
```

## ⚠️ Error Handling

OCPI.Net automatically handles exceptions:

```csharp
// Throw standard OCPI exceptions
throw OcpiException.UnknownLocation("Location not found");
throw OcpiException.InvalidParameters("Missing required field");
throw OcpiException.MethodNotAllowed("Already registered");
throw OcpiException.ClientApiError("Invalid request");

// Custom status code
throw OcpiException.Custom("Custom error", 2010);
```

## 📦 Built-in OCPI Contracts

```csharp
using OCPI.Contracts;

// Credentials
OcpiCredentials
OcpiCredentialsRole
OcpiBusinessDetails

// Locations
OcpiLocation
OcpiEvse
OcpiConnector
OcpiGeoLocation
OcpiEvseStatus
OcpiConnectorType
OcpiConnectorFormat
OcpiPowerType

// Sessions
OcpiSession
OcpiChargingPeriod
OcpiCdrDimension

// CDRs
OcpiCdr
OcpiCdrLocation
OcpiPrice

// Tariffs
OcpiTariff
OcpiTariffElement
OcpiPriceComponent
OcpiTariffDimensionType

// Tokens
OcpiToken
OcpiAuthorizationInfo
OcpiAllowed
OcpiLocationReferences

// Common
CountryCode (enum)
PartyRole (enum - Cpo, Emsp)
OcpiPageRequest
PageResult<T>
```

## 🧪 Testing Examples

### 1. Get Versions (No Auth)

```bash
curl https://localhost:6101/versions
```

### 2. Register Partner

```bash
curl -X POST https://localhost:6101/2.2.1/credentials \
  -H "Authorization: Token partner-token-123" \
  -H "Content-Type: application/json" \
  -d '{
    "token": "partner-token-123",
    "url": "https://partner.com/versions",
    "roles": [{
      "country_code": "US",
      "party_id": "EMSP",
      "role": "EMSP",
      "business_details": {
        "name": "Partner Co"
      }
    }]
  }'
```

### 3. Get Locations (With Auth)

```bash
curl https://localhost:6101/2.2.1/locations \
  -H "Authorization: Token your-secure-token"
```

## 📊 Database Mapping Example

```csharp
private OcpiLocation MapToOcpiLocation(ChargingHub hub)
{
    return new OcpiLocation
    {
        CountryCode = CountryCode.Usa,
        PartyId = "CPO",
        Id = hub.RecId,
        Name = hub.ChargingHubName,
        Address = hub.AddressLine1,
        City = hub.City,
        PostalCode = hub.Pincode,
        Country = CountryCode.Usa,
        Coordinates = new OcpiGeoLocation
        {
            Latitude = decimal.Parse(hub.Latitude),
            Longitude = decimal.Parse(hub.Longitude)
        },
        Evses = hub.ChargingStations?.Select(MapToEvse).ToList(),
        LastUpdated = hub.UpdatedOn
    };
}
```

## 🔄 Status Mapping

```csharp
// OCPP -> OCPI Status Mapping
private OcpiEvseStatus MapStatus(string? ocppStatus)
{
    return ocppStatus?.ToUpper() switch
    {
        "AVAILABLE" => OcpiEvseStatus.Available,
        "OCCUPIED" or "CHARGING" => OcpiEvseStatus.Charging,
        "RESERVED" => OcpiEvseStatus.Reserved,
        "UNAVAILABLE" or "FAULTED" => OcpiEvseStatus.Blocked,
        _ => OcpiEvseStatus.Unknown
    };
}

// Connector Type Mapping
private OcpiConnectorType MapConnectorType(string? type)
{
    return type?.ToUpper() switch
    {
        "TYPE2" or "TYPE 2" => OcpiConnectorType.Iec621962T2,
        "CHADEMO" => OcpiConnectorType.Chademo,
        "CCS" or "CCS2" => OcpiConnectorType.Iec621962T2Combo,
        "TYPE1" => OcpiConnectorType.Iec621962T1,
        _ => OcpiConnectorType.Iec621962T2
    };
}
```

## 📝 Notes

- **[OcpiAuthorize]** validates the `Authorization: Token` header
- **OcpiValidate()** uses FluentValidation internally
- **OcpiOk()** formats responses according to OCPI spec
- **IOcpiVersionService** automatically discovers `[OcpiEndpoint]` controllers
- **PageResult<T>** automatically adds pagination headers (Link, X-Limit, X-Total-Count)

## 📚 Resources

- **Docs**: https://bitzart.github.io/OCPI.Net/
- **GitHub**: https://github.com/BitzArt/OCPI.Net
- **OCPI Spec**: https://github.com/ocpi/ocpi
- **Sample**: https://github.com/BitzArt/OCPI.Net/tree/main/sample
