# OCPI.Net Implementation Guide

## ✅ Implementation Complete

This project now uses the **OCPI.Net NuGet package** (v0.18.0) for a standards-compliant OCPI v2.2.1 implementation.

## 🏗️ Architecture Overview

### Framework Components

1. **OCPI.Net Package** - Provides:
   - `OcpiController` base class with automatic error handling
   - Built-in validation with `OcpiValidate()` method
   - Response formatting with `OcpiOk()` method
   - `IOcpiVersionService` for automatic endpoint discovery
   - `[OcpiEndpoint]` attribute for module registration
   - `[OcpiAuthorize]` attribute for authentication

2. **Controllers** - All inherit from `OcpiController`:
   - `OcpiVersionsController` - `/versions` (auto-generated from OcpiEndpoint attributes)
   - `OcpiCredentialsController` - `/2.2.1/credentials` (Receiver role)
   - `OcpiLocations_SenderController` - `/2.2.1/locations` (Sender role - publish your locations)
   - `OcpiLocations_ReceiverController` - `/2.2.1/locations/receiver` (Receiver role - receive partner locations)
   - `OcpiSessionsController` - `/2.2.1/sessions` (Receiver role)
   - `OcpiCdrsController` - `/2.2.1/cdrs` (Receiver role)
   - `OcpiTariffsController` - `/2.2.1/tariffs` (Sender role)
   - `OcpiTokensController` - `/2.2.1/tokens` (Receiver role - authorize tokens)

3. **Built-in OCPI Contracts** from `OCPI.Contracts` namespace:
   - `OcpiCredentials`, `OcpiCredentialsRole`
   - `OcpiLocation`, `OcpiEvse`, `OcpiConnector`
   - `OcpiSession`, `OcpiChargingPeriod`
   - `OcpiCdr`, `OcpiCdrLocation`
   - `OcpiTariff`, `OcpiTariffElement`, `OcpiPriceComponent`
   - `OcpiToken`, `OcpiAuthorizationInfo`

## 🚀 Quick Start

### 1. Configuration

Update `appsettings.json`:

```json
{
  "OCPI": {
    "BaseUrl": "https://your-domain.com",
    "Token": "your-secure-platform-token",
    "CountryCode": "US",
    "PartyId": "CPO",
    "BusinessName": "Your Business Name",
    "Website": "https://your-website.com"
  },
  "ConnectionStrings": {
    "SqlServer": "Your-Database-Connection-String"
  }
}
```

### 2. Run the Application

```bash
cd OCPI.Core.Roaming
dotnet run
```

Access Swagger UI at: `https://localhost:6101`

### 3. Test OCPI Endpoints

#### Step 1: Version Discovery (No Auth Required)
```http
GET https://localhost:6101/versions
```

Response will show available OCPI versions and modules.

#### Step 2: Credentials Exchange (Initial Handshake)
```http
POST https://localhost:6101/2.2.1/credentials
Authorization: Token YOUR_PARTNER_TOKEN
Content-Type: application/json

{
  "token": "partner-platform-token",
  "url": "https://partner-platform.com/versions",
  "roles": [{
    "country_code": "US",
    "party_id": "EMSP",
    "role": "EMSP",
    "business_details": {
      "name": "Partner eMSP"
    }
  }]
}
```

#### Step 3: Get Locations (Requires Auth)
```http
GET https://localhost:6101/2.2.1/locations
Authorization: Token YOUR_PARTNER_TOKEN
```

## 🔐 Authentication

OCPI authentication uses the `[OcpiAuthorize]` attribute on controllers. This validates the `Authorization: Token <token>` header.

**Important:** The `/versions` endpoint is public (no auth required) as per OCPI spec. All other endpoints require authentication.

## 📋 Modules Implemented

| Module | Role | Status | Endpoints |
|--------|------|--------|-----------|
| **Credentials** | Receiver | ✅ Complete | GET, POST, PUT, DELETE |
| **Locations** | Sender | ✅ Complete | GET (list), GET (single), GET (evse), GET (connector) |
| **Locations** | Receiver | ✅ Complete | PUT (location), PATCH (location), PUT (evse), PUT (connector) |
| **Sessions** | Receiver | ✅ Complete | PUT, PATCH |
| **CDRs** | Receiver | ✅ Complete | POST, GET |
| **Tariffs** | Sender | ✅ Complete | GET (list), GET (single) |
| **Tokens** | Receiver | ✅ Complete | POST (authorize), PUT, PATCH |

## 🔄 Next Steps for Production

### 1. Database Integration

Currently, all controllers return sample data. Integrate with `OCPPCoreContext`:

```csharp
[OcpiEndpoint(OcpiModule.Locations, "Sender", "2.2.1")]
[Route("2.2.1/locations")]
[OcpiAuthorize]
public class OcpiLocations_SenderController : OcpiController
{
    private readonly OCPPCoreContext _dbContext;

    public OcpiLocations_SenderController(OCPPCoreContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetLocations([FromQuery] OcpiPageRequest pageRequest)
    {
        SetMaxLimit(pageRequest, 100);

        // Fetch from database
        var hubs = await _dbContext.ChargingHubs
            .Include(h => h.ChargingStations)
            .ThenInclude(s => s.ChargingGuns)
            .ToListAsync();

        // Map to OCPI.Contracts.OcpiLocation
        var locations = hubs.Select(MapToOcpiLocation).ToList();

        var pagedLocations = locations
            .Skip(pageRequest.Offset ?? 0)
            .Take(pageRequest.Limit ?? 100)
            .ToList();

        var result = new PageResult<OcpiLocation>(pagedLocations)
        {
            TotalCount = locations.Count,
            Offset = pageRequest.Offset ?? 0,
            Limit = pageRequest.Limit ?? 100
        };

        return OcpiOk(result);
    }

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
            Evses = hub.ChargingStations?.Select(s => new OcpiEvse
            {
                Uid = s.RecId,
                EvseId = $"US*CPO*{s.ChargingPointId}",
                Status = MapStatus(s.ChargePoint?.LastStatus),
                Connectors = s.ChargingGuns?.Select(g => new OcpiConnector
                {
                    Id = g.ConnectorId,
                    Standard = MapConnectorType(g.ChargerType),
                    Format = OcpiConnectorFormat.Socket,
                    PowerType = g.PowerOutput?.Contains("DC") == true 
                        ? OcpiPowerType.Dc 
                        : OcpiPowerType.Ac3Phase,
                    LastUpdated = g.UpdatedOn
                }).ToList(),
                LastUpdated = s.UpdatedOn
            }).ToList(),
            LastUpdated = hub.UpdatedOn
        };
    }

    private OcpiEvseStatus MapStatus(string? ocppStatus)
    {
        return ocppStatus?.ToUpper() switch
        {
            "AVAILABLE" => OcpiEvseStatus.Available,
            "OCCUPIED" or "CHARGING" => OcpiEvseStatus.Charging,
            "RESERVED" => OcpiEvseStatus.Reserved,
            _ => OcpiEvseStatus.Blocked
        };
    }

    private OcpiConnectorType MapConnectorType(string? chargerType)
    {
        return chargerType?.ToUpper() switch
        {
            "TYPE2" or "TYPE 2" => OcpiConnectorType.Iec621962T2,
            "CHADEMO" => OcpiConnectorType.Chademo,
            "CCS" => OcpiConnectorType.Iec621962T2Combo,
            _ => OcpiConnectorType.Iec621962T2
        };
    }
}
```

### 2. Implement Authentication Storage

Store partner credentials in database:

```csharp
public class OcpiPartnerCredential
{
    public int Id { get; set; }
    public string Token { get; set; }
    public string Url { get; set; }
    public string CountryCode { get; set; }
    public string PartyId { get; set; }
    public string Role { get; set; } // CPO or EMSP
    public string BusinessName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime LastSyncOn { get; set; }
}
```

Update `OcpiCredentialsController`:

```csharp
[HttpPost]
public async Task<IActionResult> Post([FromBody] OcpiCredentials partnerCredentials)
{
    OcpiValidate(partnerCredentials);

    // Check if already registered
    var existing = await _dbContext.OcpiPartners
        .FirstOrDefaultAsync(p => p.Token == partnerCredentials.Token);
    
    if (existing != null)
        throw OcpiException.MethodNotAllowed("Platform is already registered");

    // Store partner credentials
    var partner = new OcpiPartnerCredential
    {
        Token = partnerCredentials.Token,
        Url = partnerCredentials.Url,
        CountryCode = partnerCredentials.Roles[0].CountryCode.ToString(),
        PartyId = partnerCredentials.Roles[0].PartyId,
        Role = partnerCredentials.Roles[0].Role.ToString(),
        BusinessName = partnerCredentials.Roles[0].BusinessDetails.Name,
        IsActive = true,
        CreatedOn = DateTime.UtcNow
    };

    _dbContext.OcpiPartners.Add(partner);
    await _dbContext.SaveChangesAsync();

    // Return your platform's credentials
    return OcpiOk(GetPlatformCredentials());
}
```

### 3. Background Synchronization

Implement periodic sync with partners:

```csharp
public class OcpiSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OcpiSyncBackgroundService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();

                // Sync locations with active partners
                var partners = await dbContext.OcpiPartners
                    .Where(p => p.IsActive && p.Role == "EMSP")
                    .ToListAsync();

                foreach (var partner in partners)
                {
                    await SyncLocationsWithPartner(partner);
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OCPI sync");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    private async Task SyncLocationsWithPartner(OcpiPartnerCredential partner)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Token {partner.Token}");

        // Push updated locations to partner
        var response = await client.GetAsync($"{partner.Url}/2.2.1/locations");
        // Process response...
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddHostedService<OcpiSyncBackgroundService>();
```

### 4. Real-Time Status Updates

When EVSE status changes in your OCPP system, update partners:

```csharp
public class OcpiEventHandler
{
    public async Task OnEvseStatusChanged(string evseUid, OcpiEvseStatus newStatus)
    {
        var partners = await _dbContext.OcpiPartners
            .Where(p => p.IsActive && p.Role == "EMSP")
            .ToListAsync();

        foreach (var partner in partners)
        {
            await NotifyPartnerOfStatusChange(partner, evseUid, newStatus);
        }
    }
}
```

## 🧪 Testing

### Using Swagger UI

1. Navigate to `https://localhost:6101`
2. Authorize with token: `YOUR_PLATFORM_TOKEN_HERE_CHANGE_THIS`
3. Test endpoints directly in the browser

### Using HTTP Files

See [OCPI.Core.Roaming.http](OCPI.Core.Roaming.http) for test requests.

## 📚 References

- **OCPI.Net Documentation**: https://bitzart.github.io/OCPI.Net/
- **OCPI.Net GitHub**: https://github.com/BitzArt/OCPI.Net
- **OCPI Official Spec**: https://github.com/ocpi/ocpi
- **Sample Application**: https://github.com/BitzArt/OCPI.Net/tree/main/sample

## 🎯 Key Differences from Custom Implementation

### Before (Custom DTOs and Services)
```csharp
[Route("ocpi/2.2.1/locations")]
[ApiController]
public class OcpiLocationsController : ControllerBase
{
    private readonly IOcpiLocationService _locationService;

    [HttpGet]
    public async Task<IActionResult> GetLocations()
    {
        var locations = await _locationService.GetLocationsAsync();
        return Ok(new OcpiResponseDto<List<OcpiLocationDto>> 
        { 
            StatusCode = 1000,
            Data = locations 
        });
    }
}
```

### After (OCPI.Net Framework)
```csharp
[OcpiEndpoint(OcpiModule.Locations, "Sender", "2.2.1")]
[Route("2.2.1/locations")]
[OcpiAuthorize]
public class OcpiLocations_SenderController : OcpiController
{
    [HttpGet]
    public IActionResult GetLocations([FromQuery] OcpiPageRequest pageRequest)
    {
        SetMaxLimit(pageRequest, 100);
        var locations = GetLocationsFromDatabase();
        var result = new PageResult<OcpiLocation>(locations);
        return OcpiOk(result); // Auto-formats response, adds headers
    }
}
```

## ✨ Benefits

1. **Standards Compliance** - Uses official OCPI contract models
2. **Automatic Validation** - Built-in validation with `OcpiValidate()`
3. **Error Handling** - Exceptions automatically converted to OCPI error responses
4. **Version Discovery** - `IOcpiVersionService` auto-scans controllers
5. **Pagination** - Built-in pagination support with `PageResult<T>`
6. **Authentication** - `[OcpiAuthorize]` attribute for token validation
7. **Less Code** - Framework handles boilerplate, you focus on business logic

---

**Status**: ✅ Framework Implementation Complete - Ready for Database Integration
