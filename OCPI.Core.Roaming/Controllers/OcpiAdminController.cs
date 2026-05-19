using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;
using OCPP.Core.Database;
using System.Net.Http.Json;
using System.Text.Json;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// Admin controller for the OCPI Roaming front-end panel.
    /// Provides unauthenticated (internal-network) access for managing our chargers
    /// and viewing partner networks.  Secure this endpoint at the API-gateway level
    /// before exposing it publicly.
    /// </summary>
    [ApiController]
    [Route("admin")]
    public class OcpiAdminController : ControllerBase
    {
        private readonly IOcpiLocationService _locationService;
        private readonly IOcpiCommandService _commandService;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly OCPPCoreContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OcpiAdminController> _logger;

        public OcpiAdminController(
            IOcpiLocationService locationService,
            IOcpiCommandService commandService,
            IOcpiCredentialsService credentialsService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            OCPPCoreContext dbContext,
            ILogger<OcpiAdminController> logger)
        {
            _locationService = locationService;
            _commandService = commandService;
            _credentialsService = credentialsService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = logger;
        }

        // ── Our Locations ──────────────────────────────────────────────────────

        /// <summary>Get all our OCPI locations (hubs → stations → guns)</summary>
        [HttpGet("locations")]
        public async Task<IActionResult> GetOurLocations(
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 100)
        {
            var locations = await _locationService.GetOurLocationsAsync(offset, limit);

            var result = locations.Select(l => new
            {
                id = l.Id,
                name = l.Name,
                address = l.Address,
                city = l.City,
                latitude = l.Coordinates?.Latitude,
                longitude = l.Coordinates?.Longitude,
                evses = (l.Evses ?? Enumerable.Empty<OcpiEvse>()).Select(e => new
                {
                    uid = e.Uid,
                    evseId = e.EvseId,
                    status = e.Status.ToString(),
                    physicalReference = e.PhysicalReference,
                    connectors = (e.Connectors ?? Enumerable.Empty<OcpiConnector>()).Select(c => new
                    {
                        id = c.Id,
                        standard = c.Standard.ToString(),
                        format = c.Format.ToString(),
                        powerType = c.PowerType.ToString(),
                        maxVoltage = c.MaxVoltage,
                        maxAmperage = c.MaxAmperage,
                        maxElectricPower = c.MaxElectricPower
                    })
                })
            });

            return Ok(new { success = true, data = result });
        }

        // ── Partners ───────────────────────────────────────────────────────────

        /// <summary>List all active OCPI partner credentials (tokens are never returned)</summary>
        [HttpGet("partners")]
        public async Task<IActionResult> GetPartners()
        {
            var partners = await _dbContext.OcpiPartnerCredentials
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    id = p.Id,
                    countryCode = p.CountryCode,
                    partyId = p.PartyId,
                    businessName = p.BusinessName,
                    role = p.Role,
                    version = p.Version,
                    url = p.Url,
                    createdOn = p.CreatedOn
                })
                .OrderBy(p => p.businessName)
                .ToListAsync();

            return Ok(new { success = true, data = partners });
        }

        /// <summary>Get a partner's synced locations (from our local DB copy)</summary>
        [HttpGet("partners/{partnerId:int}/locations")]
        public async Task<IActionResult> GetPartnerLocations([FromRoute] int partnerId)
        {
            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == partnerId && p.IsActive);

            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found" });

            var locations = await _dbContext.OcpiPartnerLocations
                .Where(l => l.PartnerCredentialId == partnerId)
                .ToListAsync();

            var locationIds = locations.Select(l => l.Id).ToList();

            var evses = await _dbContext.OcpiPartnerEvses
                .Where(e => locationIds.Contains(e.PartnerLocationId))
                .ToListAsync();

            var result = locations.Select(l => new
            {
                id = l.Id,
                locationId = l.LocationId,
                name = l.Name,
                address = l.Address,
                city = l.City,
                country = l.Country,
                latitude = l.Latitude,
                longitude = l.Longitude,
                evses = evses
                    .Where(e => e.PartnerLocationId == l.Id)
                    .Select(e => new
                    {
                        id = e.Id,
                        evseUid = e.EvseUid,
                        evseId = e.EvseId,
                        status = e.Status ?? "UNKNOWN",
                        lastUpdated = e.LastUpdated
                    })
            });

            return Ok(new { success = true, data = result });
        }

        // ── Commands (Our Chargers) ────────────────────────────────────────────

        /// <summary>Deactivate / remove a registered OCPI partner</summary>
        [HttpDelete("partners/{id:int}")]
        public async Task<IActionResult> RemovePartner([FromRoute] int id)
        {
            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found" });

            partner.IsActive = false;
            partner.LastUpdated = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("[Admin] Deactivated OCPI partner Id={Id} ({BusinessName})", id, partner.BusinessName);
            return Ok(new { success = true, message = $"Partner '{partner.BusinessName}' removed" });
        }

        /// <summary>
        /// Probe a partner's versions URL to verify connectivity.
        /// Uses the stored token to authenticate with the partner.
        /// </summary>
        [HttpPost("partners/{id:int}/probe")]
        public async Task<IActionResult> ProbePartner([FromRoute] int id)
        {
            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found" });

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {partner.Token}");
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var resp = await httpClient.GetAsync(partner.Url);
                sw.Stop();

                return Ok(new
                {
                    success    = resp.IsSuccessStatusCode,
                    statusCode = (int)resp.StatusCode,
                    latencyMs  = sw.ElapsedMilliseconds,
                    message    = resp.IsSuccessStatusCode ? "Reachable" : $"HTTP {(int)resp.StatusCode}"
                });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, statusCode = 0, latencyMs = -1, message = ex.Message });
            }
        }

        /// <summary>
        /// Initiate OCPI handshake: register ourselves with a partner CPO/eMSP.
        /// Caller provides the partner's OCPI versions URL and the A-token (shared out of band).
        /// We discover their endpoints and POST our credentials to complete registration.
        /// </summary>
        [HttpPost("partners/onboard")]
        public async Task<IActionResult> OnboardPartner([FromBody] OnboardPartnerRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.VersionsUrl) || string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { success = false, message = "VersionsUrl and Token are required" });

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {request.Token}");
            httpClient.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                // Step 1: GET partner's versions list
                var versionsJson = await httpClient.GetStringAsync(request.VersionsUrl);
                using var versionsDoc = JsonDocument.Parse(versionsJson);
                var versionsRoot = versionsDoc.RootElement;

                if (!versionsRoot.TryGetProperty("data", out var versionsData) || versionsData.ValueKind != JsonValueKind.Array)
                    return BadRequest(new { success = false, message = "Partner returned no OCPI versions" });

                // Step 2: Find 2.2.1 (fallback to 2.2)
                string? versionUrl = null;
                string? chosenVersion = null;
                foreach (var v in versionsData.EnumerateArray())
                {
                    var ver = v.GetProperty("version").GetString();
                    if (ver == "2.2.1") { versionUrl = v.GetProperty("url").GetString(); chosenVersion = "2.2.1"; break; }
                    if (ver == "2.2"  ) { versionUrl = v.GetProperty("url").GetString(); chosenVersion = "2.2"; }
                }

                if (versionUrl == null)
                    return BadRequest(new { success = false, message = "No compatible OCPI version (2.2 or 2.2.1) found at partner" });

                // Step 3: GET version details to find credentials endpoint
                var detailJson = await httpClient.GetStringAsync(versionUrl);
                using var detailDoc = JsonDocument.Parse(detailJson);
                var detailRoot = detailDoc.RootElement;

                if (!detailRoot.TryGetProperty("data", out var detailData))
                    return BadRequest(new { success = false, message = "Failed to read version details from partner" });

                string? credentialsEndpointUrl = null;
                if (detailData.TryGetProperty("endpoints", out var endpoints))
                {
                    foreach (var ep in endpoints.EnumerateArray())
                    {
                        var identifier = ep.GetProperty("identifier").GetString() ?? "";
                        if (identifier.Equals("credentials", StringComparison.OrdinalIgnoreCase))
                        {
                            credentialsEndpointUrl = ep.GetProperty("url").GetString();
                            break;
                        }
                    }
                }

                if (credentialsEndpointUrl == null)
                    return BadRequest(new { success = false, message = "Partner does not expose a credentials endpoint" });

                // Step 4: Build our credentials object
                var ourToken = Guid.NewGuid().ToString("N")[..32].ToUpperInvariant();
                var ourBaseUrl = _configuration["Ocpi:OurBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var ourVersionsUrl = $"{ourBaseUrl}/2.2.1/versions";
                var ourCountryCode = _configuration["Ocpi:CountryCode"] ?? "IN";
                var ourPartyId = _configuration["Ocpi:PartyId"] ?? "HYC";
                var ourBusinessName = _configuration["Ocpi:BusinessName"] ?? "HyCharge";

                var ourCredentials = new
                {
                    token = ourToken,
                    url = ourVersionsUrl,
                    roles = new[]
                    {
                        new
                        {
                            role = "CPO",
                            business_details = new { name = ourBusinessName },
                            country_code = ourCountryCode,
                            party_id = ourPartyId
                        }
                    }
                };

                // Step 5: POST our credentials to partner's credentials endpoint
                var credResp = await httpClient.PostAsJsonAsync(credentialsEndpointUrl, ourCredentials);
                if (!credResp.IsSuccessStatusCode)
                {
                    var body = await credResp.Content.ReadAsStringAsync();
                    return BadRequest(new { success = false, message = $"Credential POST failed (HTTP {(int)credResp.StatusCode}): {body}" });
                }

                // Step 6: Parse the partner's returned credentials (B-token)
                var credJson = await credResp.Content.ReadAsStringAsync();
                using var credDoc = JsonDocument.Parse(credJson);
                var credData = credDoc.RootElement.GetProperty("data");

                var partnerToken = credData.GetProperty("token").GetString() ?? request.Token;
                var partnerUrl   = credData.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? request.VersionsUrl : request.VersionsUrl;

                string? partnerCountryCode = null;
                string? partnerPartyId = null;
                string? partnerRole = null;
                string? partnerName = request.BusinessName;

                if (credData.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
                {
                    var firstRole = roles.EnumerateArray().FirstOrDefault();
                    if (firstRole.ValueKind != JsonValueKind.Undefined)
                    {
                        partnerCountryCode = firstRole.TryGetProperty("country_code", out var cc) ? cc.GetString() : null;
                        partnerPartyId     = firstRole.TryGetProperty("party_id",     out var pi) ? pi.GetString() : null;
                        partnerRole        = firstRole.TryGetProperty("role",          out var ro) ? ro.GetString() : null;
                        if (firstRole.TryGetProperty("business_details", out var bd))
                            partnerName = bd.TryGetProperty("name", out var nm) ? nm.GetString() : partnerName;
                    }
                }

                // Step 7: Persist partner credentials (use B-token going forward)
                await _credentialsService.CreateOrUpdatePartnerAsync(
                    token:        partnerToken,
                    url:          partnerUrl,
                    countryCode:  partnerCountryCode ?? "??",
                    partyId:      partnerPartyId ?? "???",
                    businessName: partnerName ?? "Unknown",
                    role:         partnerRole ?? "CPO",
                    version:      chosenVersion!
                );

                _logger.LogInformation("[Admin] OCPI handshake complete with {Url} ({BusinessName})", request.VersionsUrl, partnerName);
                return Ok(new { success = true, message = "Partner registered successfully", partnerName });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[Admin] OCPI onboard HTTP error for {Url}", request.VersionsUrl);
                return StatusCode(502, new { success = false, message = $"Could not reach partner: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] OCPI onboard error for {Url}", request.VersionsUrl);
                return StatusCode(500, new { success = false, message = $"Error during handshake: {ex.Message}" });
            }
        }

        // ── Commands (Our Chargers) ────────────────────────────────────────────

        /// <summary>Start a charging session on one of our chargers via OCPI command</summary>
        [HttpPost("commands/start")]
        public async Task<IActionResult> StartSession([FromBody] AdminStartRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LocationId))
                return BadRequest(new { success = false, message = "LocationId is required" });

            var command = new OcpiStartSessionCommand
            {
                ResponseUrl = "https://noop.local/callback",
                Token = new OcpiToken { Uid = request.TagUid },
                LocationId = request.LocationId,
                EvseUid = request.EvseUid,
                ConnectorId = request.ConnectorId
            };

            _logger.LogInformation(
                "[Admin] START_SESSION location={LocationId} evse={EvseUid} connector={ConnectorId} tag={Tag}",
                request.LocationId, request.EvseUid, request.ConnectorId, request.TagUid);

            var (result, sessionId) = await _commandService.HandleStartSessionAsync(command);
            return Ok(new
            {
                success = result == CommandResponseType.Accepted,
                result = result.ToString(),
                sessionId = result == CommandResponseType.Accepted ? sessionId : null
            });
        }

        /// <summary>Stop an active charging session on one of our chargers</summary>
        [HttpPost("commands/stop")]
        public async Task<IActionResult> StopSession([FromBody] AdminStopRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
                return BadRequest(new { success = false, message = "SessionId is required" });

            var command = new OcpiStopSessionCommand
            {
                ResponseUrl = "https://noop.local/callback",
                SessionId = request.SessionId
            };

            _logger.LogInformation("[Admin] STOP_SESSION sessionId={SessionId}", request.SessionId);

            var result = await _commandService.HandleStopSessionAsync(command);
            return Ok(new
            {
                success = result == CommandResponseType.Accepted,
                result = result.ToString()
            });
        }

        /// <summary>Send an unlock connector command to one of our chargers</summary>
        [HttpPost("commands/unlock")]
        public async Task<IActionResult> UnlockConnector([FromBody] AdminUnlockRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LocationId) ||
                string.IsNullOrWhiteSpace(request.EvseUid) ||
                string.IsNullOrWhiteSpace(request.ConnectorId))
                return BadRequest(new { success = false, message = "LocationId, EvseUid and ConnectorId are all required" });

            var command = new OcpiUnlockConnectorCommand
            {
                ResponseUrl = "https://noop.local/callback",
                LocationId = request.LocationId,
                EvseUid = request.EvseUid,
                ConnectorId = request.ConnectorId
            };

            _logger.LogInformation(
                "[Admin] UNLOCK_CONNECTOR location={LocationId} evse={EvseUid} connector={ConnectorId}",
                request.LocationId, request.EvseUid, request.ConnectorId);

            var result = await _commandService.HandleUnlockConnectorAsync(command);
            return Ok(new
            {
                success = result == CommandResponseType.Accepted,
                result = result.ToString()
            });
        }

        // ── Sessions ───────────────────────────────────────────────────────────

        /// <summary>
        /// List active sessions at OUR charging stations (CPO role).
        /// For sessions our users do at partner CPO stations see the partner-sessions endpoint.
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetActiveSessions()
        {
            // CPO role: sessions hosted at our stations
            var sessions = await _dbContext.OcpiHostedSessions
                .Where(s => s.Status == "ACTIVE")
                .ToListAsync();

            var stationIds = sessions.Select(s => s.EvseUid).Where(x => x != null).Distinct().ToList();
            var gunIds     = sessions.Select(s => s.ConnectorId).Where(x => x != null).Distinct().ToList();

            var stations = await _dbContext.ChargingStations
                .Where(st => stationIds.Contains(st.RecId))
                .ToListAsync();

            var hubs = await _dbContext.ChargingHubs
                .Where(h => stations.Select(st => st.ChargingHubId).Contains(h.RecId))
                .ToListAsync();

            var guns = await _dbContext.ChargingGuns
                .Where(g => gunIds.Contains(g.RecId))
                .ToListAsync();

            var result = sessions.Select(s =>
            {
                var station = stations.FirstOrDefault(st => st.RecId == s.EvseUid);
                var hub     = hubs.FirstOrDefault(h => h.RecId == station?.ChargingHubId);
                var gun     = guns.FirstOrDefault(g => g.RecId == s.ConnectorId);

                double kwh  = (double)(s.TotalEnergy ?? 0m);
                double cost = (double)(s.TotalCost   ?? 0m);

                return new
                {
                    sessionId     = s.SessionId,
                    transactionId = s.TransactionId,
                    status        = s.Status,
                    startDateTime = s.StartDateTime,
                    locationId    = station?.ChargingHubId,
                    locationName  = hub?.ChargingHubName,
                    evseId        = station?.RecId,
                    evseName      = station?.ChargingPointId,
                    connectorId   = gun?.ConnectorId,
                    kwh           = Math.Round(kwh, 3),
                    cost          = Math.Round(cost, 2),
                    tariff        = gun?.ChargerTariff ?? "NA",
                    tokenUid      = s.TokenUid,
                    lastUpdated   = s.LastUpdated
                };
            });

            return Ok(new { success = true, data = result });
        }

        /// <summary>List sessions our users did at partner CPO stations (eMSP role).</summary>
        [HttpGet("partner-sessions")]
        public async Task<IActionResult> GetPartnerSessions()
        {
            var sessions = await _dbContext.OcpiPartnerSessions
                .OrderByDescending(s => s.LastUpdated)
                .Take(200)
                .ToListAsync();

            return Ok(new { success = true, data = sessions });
        }

        /// <summary>Get a single hosted session (CPO role) with live meter reading</summary>
        [HttpGet("sessions/{sessionId}")]
        public async Task<IActionResult> GetSession([FromRoute] string sessionId)
        {
            var s = await _dbContext.OcpiHostedSessions
                .FirstOrDefaultAsync(cs => cs.SessionId == sessionId);

            if (s == null)
                return NotFound(new { success = false, message = "Session not found" });

            var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(st => st.RecId == s.EvseUid);
            var hub = station != null ? await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == station.ChargingHubId) : null;
            var gun = await _dbContext.ChargingGuns.FirstOrDefaultAsync(g => g.RecId == s.ConnectorId);

            // Live meter from OCPP connector status — OcpiHostedSession stores ChargePointId/ConnectorNumber directly
            double? liveMeter = null;
            var connStatus = await _dbContext.ConnectorStatuses
                .FirstOrDefaultAsync(c => c.ChargePointId == s.ChargePointId
                                       && c.ConnectorId   == s.ConnectorNumber
                                       && c.Active        == 1);
            liveMeter = connStatus?.LastMeter;

            double kwh  = (double)(s.TotalEnergy ?? 0m);
            double cost = (double)(s.TotalCost   ?? 0m);

            // If we have a live meter and a transaction, calculate live kWh from meter diff
            if (liveMeter.HasValue && s.TransactionId.HasValue)
            {
                var tx = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == s.TransactionId.Value);

                if (tx != null && liveMeter.Value >= tx.MeterStart)
                {
                    kwh = Math.Round(liveMeter.Value - tx.MeterStart, 3);
                    if (gun != null)
                    {
                        double.TryParse(gun.ChargerTariff, out double tariffVal);
                        cost = Math.Round(kwh * tariffVal * 1.18, 2);
                    }
                }
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    sessionId     = s.SessionId,
                    transactionId = s.TransactionId,
                    status        = s.Status,
                    startDateTime = s.StartDateTime,
                    endDateTime   = (s.EndDateTime == null) ? (DateTime?)null : s.EndDateTime,
                    locationId    = station?.ChargingHubId,
                    locationName  = hub?.ChargingHubName,
                    evseId        = station?.RecId,
                    evseName      = station?.ChargingPointId,
                    connectorId   = gun?.ConnectorId,
                    kwh,
                    cost,
                    tariff        = gun?.ChargerTariff ?? "NA",
                    liveMeter,
                    tokenUid      = s.TokenUid,
                    lastUpdated   = s.LastUpdated
                }
            });
        }

        // ── Request DTOs ──────────────────────────────────────────────────────────

        public record AdminStartRequest(
            string LocationId,
            string? EvseUid,
            string? ConnectorId,
            string TagUid);

        public record AdminStopRequest(string SessionId);

        public record AdminUnlockRequest(
            string LocationId,
            string EvseUid,
            string ConnectorId);

        public record OnboardPartnerRequest(
            string VersionsUrl,
            string Token,
            string? BusinessName);
    }
}
