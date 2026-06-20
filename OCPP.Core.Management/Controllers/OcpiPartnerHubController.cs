using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    /// <summary>
    /// Exposes OCPI partner locations (synced from partner CPOs) through the app's
    /// Hub → Station → Gun structure, mirroring <see cref="ChargingHubController"/> but
    /// sourced from OcpiPartnerLocation / OcpiPartnerEvse / OcpiPartnerConnector tables.
    ///
    /// Mapping:
    ///   OcpiPartnerLocation  → Hub
    ///   OcpiPartnerEvse      → Station
    ///   OcpiPartnerConnector → Gun / Charger
    ///
    /// Also provides session management for charging at partner CPO stations (eMSP role).
    /// Session commands are proxied to the OCPI roaming service via OcpiRoamingApiUrl config.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class OcpiPartnerHubController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiPartnerHubController> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public OcpiPartnerHubController(
            OCPPCoreContext dbContext,
            ILogger<OcpiPartnerHubController> logger,
            IConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        // ── Hub (Partner Location) Listing ────────────────────────────────────

        /// <summary>
        /// Paginated list of all partner OCPI locations exposed as hubs.
        /// Optional filters: partnerName, city, country.
        /// </summary>
        [HttpGet("list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPartnerHubList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? partnerName = null,
            [FromQuery] string? city = null,
            [FromQuery] string? country = null)
        {
            try
            {
                var query = _dbContext.OcpiPartnerLocations
                    .Join(_dbContext.OcpiPartnerCredentials,
                        l => l.PartnerCredentialId,
                        p => p.Id,
                        (l, p) => new { Location = l, Partner = p })
                    .Where(x => x.Partner.IsActive);

                if (!string.IsNullOrWhiteSpace(partnerName))
                    query = query.Where(x => x.Partner.BusinessName.Contains(partnerName));
                if (!string.IsNullOrWhiteSpace(city))
                    query = query.Where(x => x.Location.City.Contains(city));
                if (!string.IsNullOrWhiteSpace(country))
                    query = query.Where(x =>
                        x.Location.Country == country || x.Location.CountryCode == country);

                var totalCount = await query.CountAsync();

                var locations = await query
                    .OrderBy(x => x.Partner.BusinessName).ThenBy(x => x.Location.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var locationIds = locations.Select(x => x.Location.Id).ToList();
                var evseCounts = await _dbContext.OcpiPartnerEvses
                    .Where(e => locationIds.Contains(e.PartnerLocationId))
                    .GroupBy(e => e.PartnerLocationId)
                    .Select(g => new { LocationId = g.Key, Count = g.Count(), Available = g.Count(e => e.Status == "AVAILABLE") })
                    .ToDictionaryAsync(x => x.LocationId, x => new { x.Count, x.Available });

                var hubs = locations.Select(x =>
                {
                    var counts = evseCounts.TryGetValue(x.Location.Id, out var c) ? c : null;
                    return MapLocationToHubDto(x.Location, x.Partner, counts?.Count ?? 0, counts?.Available ?? 0);
                });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalCount,
                        page,
                        pageSize,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                        hubs
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner hub list");
                return Ok(new { success = false, message = "Error retrieving partner hub list" });
            }
        }

        /// <summary>
        /// Get a single partner location (hub) with its EVSEs (stations) and connectors (guns).
        /// </summary>
        [HttpGet("details/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPartnerHubDetails([FromRoute] int id)
        {
            try
            {
                var location = await _dbContext.OcpiPartnerLocations
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (location == null)
                    return NotFound(new { success = false, message = "Partner hub not found" });

                var partner = await _dbContext.OcpiPartnerCredentials
                    .FirstOrDefaultAsync(p => p.Id == location.PartnerCredentialId && p.IsActive);

                if (partner == null)
                    return NotFound(new { success = false, message = "Partner not found or inactive" });

                var evses = await _dbContext.OcpiPartnerEvses
                    .Where(e => e.PartnerLocationId == id)
                    .ToListAsync();

                var evseIds = evses.Select(e => e.Id).ToList();
                var connectors = await _dbContext.OcpiPartnerConnectors
                    .Where(c => evseIds.Contains(c.PartnerEvseId))
                    .ToListAsync();

                int availableEvses = evses.Count(e =>
                    string.Equals(e.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase));

                var stations = evses.Select(e =>
                    MapEvseToStationDto(e, id, connectors.Where(c => c.PartnerEvseId == e.Id)));

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        hub = MapLocationToHubDto(location, partner, evses.Count, availableEvses),
                        stations
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner hub details for id={Id}", id);
                return Ok(new { success = false, message = "Error retrieving partner hub details" });
            }
        }

        /// <summary>
        /// Get a single EVSE (station) with its connectors (guns).
        /// </summary>
        [HttpGet("evse/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPartnerEvseDetails([FromRoute] int id)
        {
            try
            {
                var evse = await _dbContext.OcpiPartnerEvses
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (evse == null)
                    return NotFound(new { success = false, message = "EVSE not found" });

                var connectors = await _dbContext.OcpiPartnerConnectors
                    .Where(c => c.PartnerEvseId == id)
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = MapEvseToStationDto(evse, evse.PartnerLocationId, connectors)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner EVSE details for id={Id}", id);
                return Ok(new { success = false, message = "Error retrieving EVSE details" });
            }
        }

        /// <summary>
        /// Get a single connector (gun) detail.
        /// </summary>
        [HttpGet("connector/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPartnerConnectorDetails([FromRoute] int id)
        {
            try
            {
                var connector = await _dbContext.OcpiPartnerConnectors
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (connector == null)
                    return NotFound(new { success = false, message = "Connector not found" });

                var evse = await _dbContext.OcpiPartnerEvses
                    .FirstOrDefaultAsync(e => e.Id == connector.PartnerEvseId);

                return Ok(new
                {
                    success = true,
                    data = evse != null ? MapConnectorToGunDto(evse, connector) : (object)connector
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner connector details for id={Id}", id);
                return Ok(new { success = false, message = "Error retrieving connector details" });
            }
        }

        // ── Search ────────────────────────────────────────────────────────────

        /// <summary>
        /// Search partner hubs by geographic coordinates and radius (Haversine distance).
        /// </summary>
        [HttpPost("search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchPartnerHubs([FromBody] PartnerHubSearchDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid search parameters" });

                // Load all locations with partner info (filtering happens in memory — lat/lon are strings)
                var allLocations = await _dbContext.OcpiPartnerLocations
                    .Join(_dbContext.OcpiPartnerCredentials,
                        l => l.PartnerCredentialId,
                        p => p.Id,
                        (l, p) => new { Location = l, Partner = p })
                    .Where(x => x.Partner.IsActive)
                    .ToListAsync();

                var withDistance = new List<(OcpiPartnerLocation Location, OcpiPartnerCredential Partner, double DistanceKm)>();

                foreach (var x in allLocations)
                {
                    if (!double.TryParse(x.Location.Latitude, out double lat) ||
                        !double.TryParse(x.Location.Longitude, out double lon))
                        continue;

                    double dist = CalculateDistance(request.Latitude, request.Longitude, lat, lon);
                    if (dist <= request.RadiusKm)
                        withDistance.Add((x.Location, x.Partner, dist));
                }

                var maxResults = request.MaxResults > 0 ? request.MaxResults : 50;
                var sorted = withDistance.OrderBy(x => x.DistanceKm).Take(maxResults).ToList();

                var locationIds = sorted.Select(x => x.Location.Id).ToList();
                var evseCounts = await _dbContext.OcpiPartnerEvses
                    .Where(e => locationIds.Contains(e.PartnerLocationId))
                    .GroupBy(e => e.PartnerLocationId)
                    .Select(g => new { LocationId = g.Key, Count = g.Count(), Available = g.Count(e => e.Status == "AVAILABLE") })
                    .ToDictionaryAsync(x => x.LocationId, x => new { x.Count, x.Available });

                var results = sorted.Select(x =>
                {
                    var counts = evseCounts.TryGetValue(x.Location.Id, out var c) ? c : null;
                    return new
                    {
                        hub = MapLocationToHubDto(x.Location, x.Partner, counts?.Count ?? 0, counts?.Available ?? 0),
                        distanceKm = Math.Round(x.DistanceKm, 2)
                    };
                });

                return Ok(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching partner hubs");
                return Ok(new { success = false, message = "Error searching partner hubs" });
            }
        }

        /// <summary>
        /// Comprehensive paginated list: partner hubs with nested stations and connectors.
        /// Mirrors ChargingHubController.GetComprehensiveList but for OCPI partner data.
        /// </summary>
        [HttpPost("comprehensive-list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPartnerComprehensiveList(
            [FromBody] PartnerHubComprehensiveSearchDto request)
        {
            try
            {
                var query = _dbContext.OcpiPartnerLocations
                    .Join(_dbContext.OcpiPartnerCredentials,
                        l => l.PartnerCredentialId,
                        p => p.Id,
                        (l, p) => new { Location = l, Partner = p })
                    .Where(x => x.Partner.IsActive);

                if (!string.IsNullOrWhiteSpace(request.PartnerName))
                    query = query.Where(x => x.Partner.BusinessName.Contains(request.PartnerName));
                if (!string.IsNullOrWhiteSpace(request.City))
                    query = query.Where(x => x.Location.City.Contains(request.City));
                if (!string.IsNullOrWhiteSpace(request.Country))
                    query = query.Where(x => x.Location.Country == request.Country);
                if (request.PartnerId.HasValue)
                    query = query.Where(x => x.Partner.Id == request.PartnerId.Value);

                var totalCount = await query.CountAsync();
                int pageSize = request.PageSize > 0 ? request.PageSize : 10;
                int page = request.Page > 0 ? request.Page : 1;

                var locationData = await query
                    .OrderBy(x => x.Partner.BusinessName).ThenBy(x => x.Location.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var locationIds = locationData.Select(x => x.Location.Id).ToList();

                // Bulk-load EVSEs and connectors
                var evses = await _dbContext.OcpiPartnerEvses
                    .Where(e => locationIds.Contains(e.PartnerLocationId))
                    .ToListAsync();

                var evseIds = evses.Select(e => e.Id).ToList();
                var connectors = await _dbContext.OcpiPartnerConnectors
                    .Where(c => evseIds.Contains(c.PartnerEvseId))
                    .ToListAsync();

                var results = locationData.Select(x =>
                {
                    var locationEvses = evses.Where(e => e.PartnerLocationId == x.Location.Id).ToList();
                    var evseIdSet = locationEvses.Select(e => e.Id).ToHashSet();
                    var locationConnectors = connectors.Where(c => evseIdSet.Contains(c.PartnerEvseId)).ToList();

                    int availableEvses = locationEvses.Count(e =>
                        string.Equals(e.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase));
                    int availableConnectors = locationConnectors.Count(c =>
                    {
                        var parentEvse = locationEvses.FirstOrDefault(e => e.Id == c.PartnerEvseId);
                        return string.Equals(parentEvse?.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase);
                    });

                    double? distKm = null;
                    if (request.Latitude.HasValue && request.Longitude.HasValue
                        && double.TryParse(x.Location.Latitude, out double lat)
                        && double.TryParse(x.Location.Longitude, out double lon))
                    {
                        distKm = Math.Round(CalculateDistance(request.Latitude.Value, request.Longitude.Value, lat, lon), 2);
                    }

                    return new
                    {
                        hub = MapLocationToHubDto(x.Location, x.Partner, locationEvses.Count, availableEvses),
                        distanceKm = distKm,
                        totalStations = locationEvses.Count,
                        availableStations = availableEvses,
                        totalConnectors = locationConnectors.Count,
                        availableConnectors,
                        stations = locationEvses.Select(e =>
                            MapEvseToStationDto(e, x.Location.Id,
                                connectors.Where(c => c.PartnerEvseId == e.Id)))
                    };
                });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalCount,
                        page,
                        pageSize,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                        hubs = results
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner comprehensive list");
                return Ok(new { success = false, message = "Error retrieving partner comprehensive list" });
            }
        }

        // ── Session Management ────────────────────────────────────────────────

        /// <summary>
        /// Start a charging session at a partner CPO station (eMSP role).
        /// Creates an OcpiPartnerSession record with the user's specified limits,
        /// then issues the START_SESSION command via the OCPI roaming service.
        /// Requires OcpiRoamingApiUrl to be configured in appsettings.
        /// </summary>
        [HttpPost("start-session")]
        [Authorize]
        public async Task<IActionResult> StartPartnerSession(
            [FromBody] PartnerSessionStartDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new { success = false, message = "Invalid request" });

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { success = false, message = "User not authenticated" });

                // Validate the EVSE
                var evse = await _dbContext.OcpiPartnerEvses
                    .FirstOrDefaultAsync(e => e.Id == request.EvseDbId);
                if (evse == null)
                    return NotFound(new { success = false, message = "EVSE not found" });

                if (!string.Equals(evse.Status, "AVAILABLE", StringComparison.OrdinalIgnoreCase))
                    return Ok(new { success = false, message = $"EVSE is not available (current status: {evse.Status})" });

                var location = await _dbContext.OcpiPartnerLocations
                    .FirstOrDefaultAsync(l => l.Id == evse.PartnerLocationId);
                if (location == null)
                    return NotFound(new { success = false, message = "Partner location not found" });

                // Resolve token UID: prefer explicit TokenUid; fall back to ChargeTag lookup
                string tokenUid = request.TokenUid ?? string.Empty;
                if (string.IsNullOrEmpty(tokenUid))
                {
                    if (string.IsNullOrEmpty(request.ChargeTagId))
                        return BadRequest(new { success = false, message = "Provide TokenUid or ChargeTagId" });

                    var chargeTag = await _dbContext.ChargeTags
                        .FirstOrDefaultAsync(t => t.TagId == request.ChargeTagId);
                    if (chargeTag == null)
                        return BadRequest(new { success = false, message = "ChargeTag not found" });

                    tokenUid = chargeTag.TagId;
                }

                // Check wallet balance — mirrors ChargingSessionController.StartChargingSession's
                // minimum-balance estimate, adapted for partner EVSEs where we don't have a local
                // tariff to turn EnergyLimit/TimeLimit into a cost estimate, so only CostLimit and
                // BatteryIncreaseLimit (flat ₹100, same as our own chargers) are used.
                var lastWalletTx = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == userId && w.Active == 1)
                    .OrderByDescending(w => w.CreatedOn)
                    .FirstOrDefaultAsync();

                decimal currentBalance = 0;
                if (lastWalletTx != null && decimal.TryParse(lastWalletTx.CurrentCreditBalance, out var balance))
                    currentBalance = balance;

                decimal minBalanceRequired = 20m; // default minimum, same as ChargingSessionController
                string balanceRequirementSource = "default minimum";
                var estimatedCosts = new List<(string source, decimal amount)>();

                if (request.CostLimit.HasValue && request.CostLimit.Value > 0)
                    estimatedCosts.Add(("Cost Limit", (decimal)request.CostLimit.Value));

                if (request.BatteryIncreaseLimit.HasValue && request.BatteryIncreaseLimit.Value > 0)
                    estimatedCosts.Add(("Battery Limit", 100m));

                if (estimatedCosts.Count > 0)
                {
                    var maxCost = estimatedCosts.OrderByDescending(x => x.amount).First();
                    minBalanceRequired = maxCost.amount;
                    balanceRequirementSource = maxCost.source;
                }

                if (currentBalance < 0)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Cannot start session. Your wallet balance is negative (₹{currentBalance:F2}). Please recharge your wallet to continue."
                    });
                }

                if (currentBalance < minBalanceRequired)
                {
                    return Ok(new
                    {
                        success = false,
                        message = $"Insufficient wallet balance. Minimum ₹{minBalanceRequired:F2} required to start charging (based on {balanceRequirementSource}). Your current balance: ₹{currentBalance:F2}. Please recharge your wallet."
                    });
                }

                // Forward to OCPI roaming service
                var roamingApiUrl = _config.GetValue<string>("OcpiRoamingApiUrl");
                if (string.IsNullOrEmpty(roamingApiUrl))
                    return StatusCode(200, new
                    {
                        success = false,
                        message = "OCPI roaming service is not configured (OcpiRoamingApiUrl missing from appsettings)"
                    });

                var http = _httpClientFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(20);

                var body = new
                {
                    partnerId = location.PartnerCredentialId,
                    locationId = location.LocationId,
                    evseUid = evse.EvseUid,
                    connectorId = request.ConnectorId,
                    tokenUid,
                    userId,
                    energyLimit = request.EnergyLimit,
                    costLimit = request.CostLimit,
                    timeLimit = request.TimeLimit,
                    batteryIncreaseLimit = request.BatteryIncreaseLimit
                };

                var resp = await http.PostAsJsonAsync(
                    $"{roamingApiUrl.TrimEnd('/')}/admin/emsp/start-session", body);

                if (!resp.IsSuccessStatusCode)
                {
                    var errBody = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning(
                        "Partner start-session HTTP error: {Status} {Body}",
                        (int)resp.StatusCode, errBody);
                    return Ok(new { success = false, message = $"OCPI roaming service returned HTTP {(int)resp.StatusCode}" });
                }

                var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting partner session");
                return Ok(new { success = false, message = "Error starting session at partner CPO" });
            }
        }

        /// <summary>
        /// Stop an active charging session at a partner CPO station.
        /// Users may only stop their own sessions; Administrators may stop any.
        /// </summary>
        [HttpPost("stop-session")]
        [Authorize]
        public async Task<IActionResult> StopPartnerSession(
            [FromBody] PartnerSessionStopDto request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var session = await _dbContext.OcpiPartnerSessions
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);
                if (session == null)
                    return NotFound(new { success = false, message = "Session not found" });

                if (!User.IsInRole("Administrator") && session.UserId != userId)
                    return Forbid();

                if (session.Status != "ACTIVE")
                    return Ok(new { success = false, message = $"Session is not active (status: {session.Status})" });

                var roamingApiUrl = _config.GetValue<string>("OcpiRoamingApiUrl");
                if (string.IsNullOrEmpty(roamingApiUrl))
                    return StatusCode(200, new { success = false, message = "OCPI roaming service not configured" });

                var http = _httpClientFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(20);

                var resp = await http.PostAsJsonAsync(
                    $"{roamingApiUrl.TrimEnd('/')}/admin/emsp/stop-session",
                    new { sessionId = request.SessionId });

                if (!resp.IsSuccessStatusCode)
                {
                    var errBody = await resp.Content.ReadAsStringAsync();
                    _logger.LogWarning("Partner stop-session HTTP error: {Status} {Body}", (int)resp.StatusCode, errBody);
                    return Ok(new { success = false, message = $"OCPI roaming service returned HTTP {(int)resp.StatusCode}" });
                }

                var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping partner session");
                return Ok(new { success = false, message = "Error stopping session at partner CPO" });
            }
        }

        /// <summary>
        /// Get the authenticated user's charging sessions at partner CPO stations.
        /// Administrators may filter by any userId via query parameter.
        /// </summary>
        [HttpGet("sessions")]
        [Authorize]
        public async Task<IActionResult> GetPartnerSessions(
            [FromQuery] string? status = null,
            [FromQuery] string? userId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var effectiveUserId = User.IsInRole("Administrator") && !string.IsNullOrEmpty(userId)
                    ? userId
                    : callerUserId;

                var query = _dbContext.OcpiPartnerSessions
                    .Where(s => s.UserId == effectiveUserId);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(s => s.Status == status.ToUpperInvariant());

                var totalCount = await query.CountAsync();

                var sessions = await query
                    .OrderByDescending(s => s.StartDateTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Enrich with partner location names
                var locationIds = sessions
                    .Select(s => s.LocationId).Where(x => x != null).Distinct().ToList();
                var locations = await _dbContext.OcpiPartnerLocations
                    .Where(l => locationIds.Contains(l.LocationId))
                    .ToListAsync();

                var partnerIds = locations.Select(l => l.PartnerCredentialId).Distinct().ToList();
                var partners = await _dbContext.OcpiPartnerCredentials
                    .Where(p => partnerIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p);

                var result = sessions.Select(s =>
                {
                    var loc = locations.FirstOrDefault(l => l.LocationId == s.LocationId);
                    var partner = loc != null && partners.TryGetValue(loc.PartnerCredentialId, out var p) ? p : null;
                    var elapsed = (s.EndDateTime ?? DateTime.UtcNow) - s.StartDateTime;

                    return new
                    {
                        sessionId       = s.SessionId,
                        status          = s.Status,
                        startDateTime   = s.StartDateTime,
                        endDateTime     = s.EndDateTime,
                        totalEnergyKwh  = s.TotalEnergy,
                        totalCost       = s.TotalCost,
                        currency        = s.Currency,
                        durationMinutes = (int)Math.Max(0, elapsed.TotalMinutes),
                        // Location
                        ocpiLocationId  = s.LocationId,
                        locationName    = loc?.Name ?? "Partner Station",
                        locationAddress = loc?.Address,
                        locationCity    = loc?.City,
                        partnerName     = partner?.BusinessName,
                        evseUid         = s.EvseUid,
                        connectorId     = s.ConnectorId,
                        // Limits
                        energyLimit           = s.EnergyLimit,
                        costLimit             = s.CostLimit,
                        timeLimit             = s.TimeLimit,
                        batteryIncreaseLimit  = s.BatteryIncreaseLimit,
                        limitViolationHandled = s.LimitViolationHandled,
                        // Live limit progress (for active sessions)
                        limitProgress = s.Status == "ACTIVE" ? (object)new
                        {
                            energyPct  = s.EnergyLimit.HasValue && s.TotalEnergy.HasValue
                                ? Math.Round((double)s.TotalEnergy.Value / s.EnergyLimit.Value * 100, 1)
                                : (double?)null,
                            costPct    = s.CostLimit.HasValue && s.TotalCost.HasValue
                                ? Math.Round((double)s.TotalCost.Value  / s.CostLimit.Value  * 100, 1)
                                : (double?)null,
                            timePct    = s.TimeLimit.HasValue
                                ? Math.Round(elapsed.TotalMinutes / s.TimeLimit.Value * 100, 1)
                                : (double?)null,
                        } : null
                    };
                });

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalCount,
                        page,
                        pageSize,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                        sessions = result
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner sessions");
                return Ok(new { success = false, message = "Error retrieving partner sessions" });
            }
        }

        /// <summary>
        /// Get session limit status for an active partner session.
        /// Shows current progress against each configured limit.
        /// </summary>
        [HttpGet("session-limit-status/{sessionId}")]
        [Authorize]
        public async Task<IActionResult> GetPartnerSessionLimitStatus([FromRoute] string sessionId)
        {
            try
            {
                var callerUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                var session = await _dbContext.OcpiPartnerSessions
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);
                if (session == null)
                    return NotFound(new { success = false, message = "Session not found" });

                if (!User.IsInRole("Administrator") && session.UserId != callerUserId)
                    return Forbid();

                var elapsed = (session.EndDateTime ?? DateTime.UtcNow) - session.StartDateTime;
                var violations = new List<string>();

                if (session.EnergyLimit.HasValue && session.TotalEnergy.HasValue &&
                    (double)session.TotalEnergy.Value >= session.EnergyLimit.Value)
                    violations.Add("EnergyLimit");

                if (session.CostLimit.HasValue && session.TotalCost.HasValue &&
                    (double)session.TotalCost.Value >= session.CostLimit.Value)
                    violations.Add("CostLimit");

                if (session.TimeLimit.HasValue && elapsed.TotalMinutes >= session.TimeLimit.Value)
                    violations.Add("TimeLimit");

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        sessionId        = session.SessionId,
                        status           = session.Status,
                        hasViolations    = violations.Any(),
                        violatedLimits   = violations,
                        limitViolationHandled = session.LimitViolationHandled,
                        limitStatus = new
                        {
                            energy = new
                            {
                                consumed = session.TotalEnergy,
                                limit    = session.EnergyLimit,
                                pct      = session.EnergyLimit.HasValue && session.TotalEnergy.HasValue
                                    ? Math.Round((double)session.TotalEnergy.Value / session.EnergyLimit.Value * 100, 1)
                                    : (double?)null,
                                unit = "kWh"
                            },
                            cost = new
                            {
                                current  = session.TotalCost,
                                limit    = session.CostLimit,
                                pct      = session.CostLimit.HasValue && session.TotalCost.HasValue
                                    ? Math.Round((double)session.TotalCost.Value  / session.CostLimit.Value  * 100, 1)
                                    : (double?)null,
                                currency = session.Currency
                            },
                            time = new
                            {
                                elapsedMinutes = (int)elapsed.TotalMinutes,
                                limit          = session.TimeLimit,
                                pct            = session.TimeLimit.HasValue
                                    ? Math.Round(elapsed.TotalMinutes / session.TimeLimit.Value * 100, 1)
                                    : (double?)null
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving partner session limit status for {SessionId}", sessionId);
                return Ok(new { success = false, message = "Error retrieving session limit status" });
            }
        }

        // ── Mapping helpers ───────────────────────────────────────────────────

        private static object MapLocationToHubDto(
            OcpiPartnerLocation location,
            OcpiPartnerCredential partner,
            int evseCount,
            int availableEvseCount)
        {
            return new
            {
                // Identity — DB id for detailed lookups; recId prefixed to distinguish from native hubs
                id            = location.Id,
                recId         = $"ocpi-{location.Id}",
                // Display
                chargingHubName = location.Name ?? $"{partner.BusinessName} – {location.City}",
                addressLine1    = location.Address,
                city            = location.City,
                state           = location.Country,
                pincode         = location.PostalCode,
                latitude        = location.Latitude,
                longitude       = location.Longitude,
                // Partner metadata
                isOcpiPartner      = true,
                partnerName        = partner.BusinessName,
                partnerCountryCode = partner.CountryCode,
                partnerPartyId     = partner.PartyId,
                partnerId          = partner.Id,
                // Counts
                stationCount          = evseCount,
                availableStationCount = availableEvseCount,
                // Raw OCPI identifiers
                ocpiLocationId = location.LocationId,
                locationType   = location.LocationType,
                lastUpdated    = location.LastUpdated
            };
        }

        private static object MapEvseToStationDto(
            OcpiPartnerEvse evse,
            int locationDbId,
            IEnumerable<OcpiPartnerConnector> connectors)
        {
            var connList = connectors.ToList();
            return new
            {
                id              = evse.Id,
                recId           = $"ocpi-evse-{evse.Id}",
                partnerLocationId = locationDbId,
                // Mirrors ChargingStationDto fields
                chargingPointId  = evse.EvseId ?? evse.EvseUid,
                chargePointName  = evse.PhysicalReference ?? evse.EvseId ?? evse.EvseUid,
                floorLevel       = evse.FloorLevel,
                status           = evse.Status ?? "UNKNOWN",
                isOcpiPartner    = true,
                chargingGunCount = connList.Count,
                lastUpdated      = evse.LastUpdated,
                // Nested guns
                chargers = connList.Select(c => MapConnectorToGunDto(evse, c))
            };
        }

        private static object MapConnectorToGunDto(OcpiPartnerEvse evse, OcpiPartnerConnector connector)
        {
            double? powerKw = connector.MaxElectricPower.HasValue
                ? Math.Round(connector.MaxElectricPower.Value / 1000.0, 2)
                : null;

            return new
            {
                id         = connector.Id,
                recId      = $"ocpi-conn-{connector.Id}",
                evseDbId   = evse.Id,
                // Mirrors ChargerDto fields
                connectorId        = connector.ConnectorId,
                chargerTypeName    = connector.Standard,
                format             = connector.Format,
                powerType          = connector.PowerType,
                powerOutputKw      = powerKw,
                maxVoltage         = connector.MaxVoltage,
                maxAmperage        = connector.MaxAmperage,
                maxElectricPowerW  = connector.MaxElectricPower,
                chargerStatus      = evse.Status ?? "UNKNOWN",
                isOcpiPartner      = true,
                lastUpdated        = connector.LastUpdated
            };
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2))
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

        // ── Request DTOs ──────────────────────────────────────────────────────

        public class PartnerHubSearchDto
        {
            public double Latitude  { get; set; }
            public double Longitude { get; set; }
            public double RadiusKm  { get; set; } = 50;
            public int    MaxResults { get; set; } = 50;
        }

        public class PartnerHubComprehensiveSearchDto
        {
            public int     Page        { get; set; } = 1;
            public int     PageSize    { get; set; } = 10;
            public string? PartnerName { get; set; }
            public string? City        { get; set; }
            public string? Country     { get; set; }
            public int?    PartnerId   { get; set; }
            // Optional geographic filtering for distance calculations
            public double? Latitude    { get; set; }
            public double? Longitude   { get; set; }
        }

        public class PartnerSessionStartDto
        {
            /// <summary>DB id of the OcpiPartnerEvse to charge at.</summary>
            public int EvseDbId { get; set; }
            /// <summary>Connector ID within the EVSE (OCPI string, e.g. "1").</summary>
            public string ConnectorId { get; set; } = "1";
            /// <summary>OCPI token UID for authorisation. Provide this or ChargeTagId.</summary>
            public string? TokenUid { get; set; }
            /// <summary>App ChargeTag.TagId to use when TokenUid is not given.</summary>
            public string? ChargeTagId { get; set; }
            // Session limits (all optional)
            public double? EnergyLimit         { get; set; }
            public double? CostLimit           { get; set; }
            public int?    TimeLimit           { get; set; }
            public double? BatteryIncreaseLimit { get; set; }
        }

        public class PartnerSessionStopDto
        {
            public string SessionId { get; set; } = string.Empty;
        }
    }
}
