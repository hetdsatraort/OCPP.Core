# OCPI Implementation Guide

## ✅ What Has Been Implemented

### 1. DTOs (Data Transfer Objects)
- ✅ OcpiCredentialsDto - Credential registration and management
- ✅ OcpiLocationDto - Location, EVSE, and Connector structures
- ✅ OcpiSessionDto - Charging session management
- ✅ OcpiTariffDto - Pricing and tariff structures
- ✅ OcpiCdrDto - Charge Detail Records
- ✅ OcpiTokenDto - Authorization tokens
- ✅ OcpiVersionDto - Version management
- ✅ OcpiCommandDto - Remote commands

### 2. Services
- ✅ IOcpiLocationService + Implementation
- ✅ IOcpiSessionService + Implementation
- ✅ IOcpiCredentialsService + Implementation
- ✅ IOcpiVersionService + Implementation

### 3. Controllers
- ✅ OcpiVersionsController - `/ocpi/versions`
- ✅ OcpiCredentialsController - `/ocpi/2.2.1/credentials`
- ✅ OcpiLocationsController - `/ocpi/2.2.1/locations`
- ✅ OcpiSessionsController - `/ocpi/2.2.1/sessions`

### 4. Configuration
- ✅ Program.cs configured with services
- ✅ appsettings.json with OCPI configuration
- ✅ Swagger/OpenAPI documentation
- ✅ CORS configuration
- ✅ JSON serialization settings

### 5. Documentation
- ✅ OCPI_README.md - Comprehensive guide
- ✅ OCPI_QUICK_REFERENCE.md - Quick reference
- ✅ OCPI.Core.Roaming.http - HTTP test file

## ⏳ Next Steps - Database Integration

### Step 1: Update OcpiLocationService to Use Database

```csharp
public class OcpiLocationService : IOcpiLocationService
{
    private readonly OCPPCoreContext _dbContext;
    private readonly ILogger<OcpiLocationService> _logger;

    public OcpiLocationService(
        OCPPCoreContext dbContext,
        ILogger<OcpiLocationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<OcpiLocationDto>> GetLocationsAsync(string countryCode = null, string partyId = null)
    {
        var query = _dbContext.ChargingHubs
            .Include(h => h.ChargingStations)
            .ThenInclude(s => s.ChargingGuns)
            .AsQueryable();

        var hubs = await query.ToListAsync();
        
        return hubs.Select(MapToOcpiLocation).ToList();
    }

    private OcpiLocationDto MapToOcpiLocation(ChargingHub hub)
    {
        return new OcpiLocationDto
        {
            Id = hub.RecId,
            Type = "PARKING_GARAGE",
            Name = hub.ChargingHubName,
            Address = hub.AddressLine1,
            City = hub.City,
            PostalCode = hub.Pincode,
            State = hub.State,
            Country = "USA",
            Coordinates = new GeoLocation
            {
                Latitude = decimal.Parse(hub.Latitude),
                Longitude = decimal.Parse(hub.Longitude)
            },
            Evses = hub.ChargingStations?.Select(MapToOcpiEvse).ToList(),
            LastUpdated = hub.UpdatedOn
        };
    }

    private OcpiEvseDto MapToOcpiEvse(ChargingStation station)
    {
        return new OcpiEvseDto
        {
            Uid = station.RecId,
            EvseId = $"US*CPO*{station.ChargingPointId}",
            Status = MapStatus(station.ChargePoint?.LastStatus),
            Connectors = station.ChargingGuns?.Select(MapToOcpiConnector).ToList(),
            LastUpdated = station.UpdatedOn
        };
    }

    private OcpiConnectorDto MapToOcpiConnector(ChargingGuns gun)
    {
        return new OcpiConnectorDto
        {
            Id = gun.ConnectorId,
            Standard = MapConnectorType(gun.ChargerType),
            Format = "SOCKET",
            PowerType = gun.PowerOutput?.Contains("DC") == true ? "DC" : "AC_3_PHASE",
            MaxElectricPower = ParsePower(gun.PowerOutput),
            LastUpdated = gun.UpdatedOn
        };
    }

    private string MapStatus(string ocppStatus)
    {
        return ocppStatus?.ToUpper() switch
        {
            "AVAILABLE" => "AVAILABLE",
            "OCCUPIED" or "CHARGING" => "CHARGING",
            "RESERVED" => "RESERVED",
            "UNAVAILABLE" or "FAULTED" => "BLOCKED",
            _ => "UNKNOWN"
        };
    }

    private string MapConnectorType(string chargerType)
    {
        return chargerType?.ToUpper() switch
        {
            "TYPE2" or "TYPE 2" => "IEC_62196_T2",
            "CHADEMO" => "CHADEMO",
            "CCS" => "IEC_62196_T2_COMBO",
            _ => "IEC_62196_T2"
        };
    }

    private int? ParsePower(string powerOutput)
    {
        if (string.IsNullOrEmpty(powerOutput))
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(powerOutput, @"(\d+)");
        if (match.Success && int.TryParse(match.Value, out int power))
        {
            // Convert to watts if in kW
            return powerOutput.Contains("kW") ? power * 1000 : power;
        }
        return null;
    }
}
```

### Step 2: Update OcpiSessionService to Use Database

```csharp
public class OcpiSessionService : IOcpiSessionService
{
    private readonly OCPPCoreContext _dbContext;
    private readonly ILogger<OcpiSessionService> _logger;

    public async Task<OcpiSessionDto> StartSessionAsync(StartSessionRequestDto request)
    {
        // 1. Validate location and EVSE exist
        var station = await _dbContext.ChargingStations
            .Include(s => s.ChargingGuns)
            .FirstOrDefaultAsync(s => s.RecId == request.EvseUid);

        if (station == null)
            throw new InvalidOperationException($"EVSE not found: {request.EvseUid}");

        // 2. Create OCPP transaction via existing ChargingSessionController
        // Call your existing StartChargingSession endpoint

        // 3. Create OCPI session record
        var session = new OcpiSessionDto
        {
            Id = Guid.NewGuid().ToString(),
            StartDateTime = DateTime.UtcNow,
            LocationId = request.LocationId,
            EvseUid = request.EvseUid,
            ConnectorId = request.ConnectorId,
            // ... map other fields
        };

        // Store in database if needed
        return session;
    }
}
```

### Step 3: Add Database Context to Services

In `Program.cs`:
```csharp
// Add database context
builder.Services.AddDbContext<OCPPCoreContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// Update service registrations
builder.Services.AddScoped<IOcpiLocationService, OcpiLocationService>();
builder.Services.AddScoped<IOcpiSessionService, OcpiSessionService>();
```

## ⏳ Additional Modules to Implement

### 1. Tariffs Module

```csharp
public interface IOcpiTariffService
{
    Task<List<OcpiTariffDto>> GetTariffsAsync();
    Task<OcpiTariffDto> CreateOrUpdateTariffAsync(OcpiTariffDto tariff);
}
```

### 2. CDR Module

```csharp
public interface IOcpiCdrService
{
    Task<OcpiCdrDto> CreateCdrAsync(OcpiCdrDto cdr);
    Task<List<OcpiCdrDto>> GetCdrsAsync();
}
```

### 3. Tokens Module

```csharp
public interface IOcpiTokenService
{
    Task<AuthorizationInfo> AuthorizeTokenAsync(TokenAuthorizationRequestDto request);
    Task<OcpiTokenDto> CreateOrUpdateTokenAsync(OcpiTokenDto token);
}
```

### 4. Commands Module (Future)

For remote operations like:
- Start/Stop sessions remotely
- Reserve charging points
- Unlock connectors

## 🔐 Security Implementation

### Add Authentication Middleware

```csharp
public class OcpiAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for version endpoint
        if (context.Request.Path.StartsWithSegments("/ocpi/versions"))
        {
            await _next(context);
            return;
        }

        // Validate OCPI token
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Token "))
        {
            context.Response.StatusCode = 401;
            return;
        }

        var token = authHeader.Substring("Token ".Length).Trim();
        var validToken = _configuration["OCPI:Token"];

        if (token != validToken)
        {
            context.Response.StatusCode = 403;
            return;
        }

        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<OcpiAuthenticationMiddleware>();
```

## 📊 Monitoring and Logging

### Add Structured Logging

```csharp
_logger.LogInformation(
    "OCPI Location retrieved: {LocationId} by {PartnerId}", 
    locationId, 
    partnerId);

_logger.LogWarning(
    "Failed authorization attempt: Token {Token} for Location {LocationId}",
    tokenUid,
    locationId);

_logger.LogError(
    ex,
    "Error processing OCPI session {SessionId}",
    sessionId);
```

## 🔄 Background Synchronization

### Periodic Updates

```csharp
public class OcpiSyncBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Sync locations
            await SyncLocationsWithDatabase();
            
            // Update session statuses
            await UpdateActiveSessionStatuses();
            
            // Generate CDRs for completed sessions
            await GenerateCdrsForCompletedSessions();
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}

// Register in Program.cs
builder.Services.AddHostedService<OcpiSyncBackgroundService>();
```

## 🧪 Testing

### Unit Tests

```csharp
[Fact]
public async Task GetLocation_ValidId_ReturnsLocation()
{
    // Arrange
    var service = new OcpiLocationService(_mockDbContext, _mockLogger);
    
    // Act
    var result = await service.GetLocationByIdAsync("LOC001");
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal("LOC001", result.Id);
}
```

### Integration Tests

Test the complete flow:
1. Register credentials
2. Create location
3. Start session
4. Stop session
5. Generate CDR

## 📋 Deployment Checklist

- [ ] Update appsettings.json with production values
- [ ] Configure HTTPS certificates
- [ ] Set up database connection strings
- [ ] Implement authentication middleware
- [ ] Add rate limiting
- [ ] Configure logging to persistent storage
- [ ] Set up monitoring and alerts
- [ ] Document partner onboarding process
- [ ] Create runbook for operations team

## 🔗 Integration Points

### With OCPP.Core.Management

1. **Location Sync**: ChargingHub → OCPI Location
2. **Session Sync**: ChargingSession → OCPI Session
3. **Status Updates**: ConnectorStatus → EVSE Status
4. **User Authorization**: UserVehicle/ChargeTag → OCPI Token

### Partner Integration

1. Credentials exchange (handshake)
2. Location data sharing
3. Session start/stop coordination
4. CDR settlement
5. Real-time status updates

---

**Implementation Status**: ✅ Core Framework Complete  
**Next Priority**: Database Integration  
**Timeline**: Ready for integration testing
