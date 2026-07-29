using BitzArt.EnumToMemberValue;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPI.Core.Roaming.BackgroundServices;
using OCPI.Core.Roaming.Services;
using OCPP.Core.Database;
using System.Net.Http.Json;
using System.Text;
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
        private readonly IOcpiSyncBackgroundService _syncService;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly IOcpiTariffService _tariffService;
        private readonly OCPPCoreContext _dbContext;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OcpiAdminController> _logger;

        public OcpiAdminController(
            IOcpiLocationService locationService,
            IOcpiCommandService commandService,
            IOcpiCredentialsService credentialsService,
            IOcpiTariffService tariffService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            OCPPCoreContext dbContext,
            ILogger<OcpiAdminController> logger,
            IOcpiSyncBackgroundService syncService)
        {
            _locationService = locationService;
            _commandService = commandService;
            _credentialsService = credentialsService;
            _tariffService = tariffService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _dbContext = dbContext;
            _logger = logger;
            _syncService = syncService;
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
                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var resp = await httpClient.GetAsync(partnerURL);
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
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(request.Token))}");
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
                var ourBaseUrl = _configuration["Ocpi:BaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
                var ourVersionsUrl = $"{ourBaseUrl}/versions";
                var ourCountryCode = _configuration["Ocpi:CountryCode"] ?? "IN";
                var ourPartyId = _configuration["Ocpi:PartyId"] ?? "HYC";
                var ourBusinessName = _configuration["Ocpi:BusinessName"] ?? "HyCharge";

                var ourCredentials = new OcpiCredentials
                {
                    Token = ourToken,
                    Url = ourVersionsUrl,
                    Roles = new List<OcpiCredentialsRole>
                    {
                        new OcpiCredentialsRole
                        {
                            Role = "EMSP",
                            BusinessDetails = new OcpiBusinessDetails { Name = ourBusinessName },
                            CountryCode = ourCountryCode,
                            PartyId = ourPartyId
                        }
                    }
                };

                // Step 5: POST our credentials to partner's credentials endpoint
                var credResp = await httpClient.PostAsJsonAsync<OcpiCredentials>(credentialsEndpointUrl, ourCredentials);
                if (!credResp.IsSuccessStatusCode)
                {
                    var body = await credResp.Content.ReadAsStringAsync();
                    return Ok(new { success = false, message = $"Credential POST failed (HTTP {(int)credResp.StatusCode}): {body}" });
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
                    token:        ourToken,
                    url:          partnerUrl,
                    countryCode:  partnerCountryCode ?? "??",
                    partyId:      partnerPartyId ?? "???",
                    businessName: partnerName ?? "Unknown",
                    role:         partnerRole ?? "CPO",
                    version:      chosenVersion!,
                    outboundToken: partnerToken
                );

                _logger.LogInformation("[Admin] OCPI handshake complete with {Url} ({BusinessName})", request.VersionsUrl, partnerName);
                return Ok(new { success = true, message = "Partner registered successfully", partnerName });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "[Admin] OCPI onboard HTTP error for {Url}", request.VersionsUrl);
                return StatusCode(200, new { success = false, message = $"Could not reach partner: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin] OCPI onboard error for {Url}", request.VersionsUrl);
                return StatusCode(200, new { success = false, message = $"Error during handshake: {ex.Message}. Inner: {ex.InnerException?.Message}" });
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
                    connectorId   = gun?.RecId,
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
                    connectorId   = gun?.RecId,
                    kwh,
                    cost,
                    tariff        = gun?.ChargerTariff ?? "NA",
                    liveMeter,
                    tokenUid      = s.TokenUid,
                    lastUpdated   = s.LastUpdated
                }
            });
        }

        // ── A-Token Issuance ──────────────────────────────────────────────────────

        /// <summary>
        /// Generate an A-token for a new partner.  Share this token with the partner
        /// out-of-band; they use it to authenticate the initial POST to /2.2.1/credentials
        /// and receive a permanent B-token in exchange.
        /// </summary>
        [HttpPost("partners/issue-token")]
        public async Task<IActionResult> IssueAToken([FromBody] IssueATokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Label))
                return BadRequest(new { success = false, message = "Label is required" });

            var pending = await _credentialsService.IssueATokenAsync(
                request.Label.Trim(),
                request.ExpiryHours > 0 ? request.ExpiryHours : 72);

            _logger.LogInformation("[Admin] Issued A-token id={Id} label='{Label}'", pending.Id, pending.Label);

            return Ok(new
            {
                success = true,
                data = new
                {
                    id        = pending.Id,
                    aToken    = pending.AToken,
                    label     = pending.Label,
                    expiresAt = pending.ExpiresAt,
                    createdOn = pending.CreatedOn
                }
            });
        }

        /// <summary>List all issued A-tokens (used and pending).</summary>
        [HttpGet("partners/issued-tokens")]
        public async Task<IActionResult> GetIssuedTokens()
        {
            var tokens = await _credentialsService.GetAllPendingRegistrationsAsync();
            var result = tokens.Select(t => new
            {
                id                  = t.Id,
                label               = t.Label,
                aToken              = t.AToken,
                expiresAt           = t.ExpiresAt,
                createdOn           = t.CreatedOn,
                isUsed              = t.IsUsed,
                usedOn              = t.UsedOn,
                partnerCredentialId = t.PartnerCredentialId,
                isExpired           = !t.IsUsed && t.ExpiresAt <= DateTime.UtcNow
            });
            return Ok(new { success = true, data = result });
        }

        // ── eMSP Outbound Commands (Our users at Partner CPO stations) ───────────

        /// <summary>
        /// Send a START_SESSION command to a partner CPO for one of our users (eMSP role).
        /// Discovers the partner's commands endpoint at runtime, POSTs the OCPI command, and
        /// creates a PENDING <see cref="OCPP.Core.Database.OCPIDTO.OcpiPartnerSession"/> record
        /// with the user's optional limits so the orphan-session service can enforce them.
        ///
        /// Per the OCPI spec the CPO — not us — assigns the session_id, so we don't know it yet.
        /// We instead send an <c>authorization_reference</c> and key the PENDING row on it
        /// (SessionId is temporarily set to the same value as a placeholder); once the CPO PUTs
        /// the real session back to us via the Sessions receiver, <see cref="OcpiSessionService"/>
        /// resolves the placeholder to the CPO-assigned session_id.
        /// </summary>
        [HttpPost("emsp/start-session")]
        public async Task<IActionResult> EmspStartSession(
            [FromBody] EmspStartSessionRequest request)
        {
            if (request.PartnerId <= 0)
                return BadRequest(new { success = false, message = "PartnerId is required" });

            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == request.PartnerId && p.IsActive);
            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found or inactive" });

            if (string.IsNullOrEmpty(partner.OutboundToken))
                return BadRequest(new { success = false, message = "Partner has no outbound token configured" });

            // Discover the partner's commands endpoint
            var commandsUrl = await DiscoverPartnerEndpointAsync(partner, "commands");
            if (commandsUrl == null)
                return StatusCode(200, new { success = false, message = "Could not discover partner commands endpoint" });

            // We do NOT assign the session_id — the CPO does. We send authorization_reference
            // so the CPO can echo it back on the Session push and we can correlate it.
            var authorizationReference = Guid.NewGuid().ToString();
            var ourBaseUrl  = _configuration["Ocpi:OurBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var responseUrl = $"{ourBaseUrl}/2.2.1/commands/START_SESSION/{authorizationReference}";

            var commandBody = new
            {
                response_url            = responseUrl,
                token                   = new
                {
                    country_code       = _configuration["Ocpi:CountryCode"] ?? "IN",
                    party_id           = _configuration["Ocpi:PartyId"]     ?? "CPO",
                    uid                = request.TokenUid,
                    type               = "APP_USER",
                    contract_id        = request.TokenUid,
                    issuer             = _configuration["Ocpi:BusinessName"] ?? "HyCharge",
                    valid              = true,
                    whitelist          = "NEVER",
                    last_updated       = DateTime.UtcNow.ToString("o")
                },
                location_id             = request.LocationId,
                evse_uid                = request.EvseUid,
                connector_id            = request.ConnectorId,
                authorization_reference = authorizationReference
            };

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
            http.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                var resp = await http.PostAsJsonAsync(
                    $"{commandsUrl.TrimEnd('/')}/START_SESSION", commandBody);
                var body = await resp.Content.ReadAsStringAsync();

                string cmdResult = "UNKNOWN";
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("result", out var resultProp))
                        cmdResult = resultProp.GetString() ?? "UNKNOWN";
                }
                catch { /* ignore parse errors */ }

                bool accepted = string.Equals(cmdResult, "ACCEPTED", StringComparison.OrdinalIgnoreCase);

                // Persist a PENDING placeholder so limits/UserId travel with the session once the
                // CPO resolves it to a real session_id. SessionId == AuthorizationReference marks
                // it unresolved; OcpiSessionService promotes it on the CPO's Session push.
                var session = new OCPP.Core.Database.OCPIDTO.OcpiPartnerSession
                {
                    CountryCode          = partner.CountryCode,
                    PartyId              = partner.PartyId,
                    SessionId            = authorizationReference,
                    AuthorizationReference = authorizationReference,
                    StartDateTime        = DateTime.UtcNow,
                    Status               = accepted ? "PENDING" : "INVALID",
                    LocationId           = request.LocationId,
                    EvseUid              = request.EvseUid,
                    ConnectorId          = request.ConnectorId,
                    TokenUid             = request.TokenUid,
                    Currency             = "INR",
                    PartnerCredentialId  = partner.Id,
                    UserId               = request.UserId,
                    EnergyLimit          = request.EnergyLimit,
                    CostLimit            = request.CostLimit,
                    TimeLimit            = request.TimeLimit,
                    BatteryIncreaseLimit = request.BatteryIncreaseLimit,
                    CreatedOn            = DateTime.UtcNow,
                    LastUpdated          = DateTime.UtcNow
                };

                _dbContext.OcpiPartnerSessions.Add(session);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "[Admin/eMSP] START_SESSION → partner {PartnerId} ({BusinessName}): result={Result}, authRef={AuthRef}",
                    request.PartnerId, partner.BusinessName, cmdResult, authorizationReference);

                return Ok(new
                {
                    success                 = accepted,
                    result                  = cmdResult,
                    sessionId               = (string?)null,
                    authorizationReference  = accepted ? authorizationReference : null,
                    status                  = accepted ? "PENDING" : "REJECTED",
                    message                 = accepted
                        ? "Command accepted by partner CPO; awaiting session confirmation. " +
                          "Poll GET admin/partner-sessions/by-reference/{authorizationReference} for the resolved sessionId."
                        : $"Partner rejected the command: {cmdResult}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Admin/eMSP] Error sending START_SESSION to partner {PartnerId}", request.PartnerId);
                return StatusCode(200,
                    new { success = false, message = $"Error communicating with partner: {ex.Message}" });
            }
        }

        /// <summary>
        /// Poll the resolution status of a session started via <see cref="EmspStartSession"/>.
        /// While the CPO hasn't yet pushed the real session, <c>sessionId</c> is null and
        /// <c>status</c> is "PENDING". Once resolved, <c>sessionId</c> holds the CPO-assigned id.
        /// </summary>
        [HttpGet("partner-sessions/by-reference/{authorizationReference}")]
        public async Task<IActionResult> GetPartnerSessionByReference([FromRoute] string authorizationReference)
        {
            var session = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.AuthorizationReference == authorizationReference);

            if (session == null)
                return NotFound(new { success = false, message = "No session found for this authorization reference" });

            bool resolved = session.SessionId != session.AuthorizationReference;

            return Ok(new
            {
                success   = true,
                data      = new
                {
                    authorizationReference = session.AuthorizationReference,
                    sessionId              = resolved ? session.SessionId : null,
                    status                 = session.Status,
                    resolved
                }
            });
        }

        /// <summary>
        /// Send a STOP_SESSION command to the partner CPO for an active eMSP session.
        /// Looks up the session by <paramref name="request"/>.<c>SessionId</c> — accepting either
        /// the CPO-assigned session_id or the original authorization_reference, in case the CPO
        /// hasn't resolved the session yet — discovers the partner's commands endpoint, and POSTs
        /// the OCPI STOP_SESSION body.
        /// </summary>
        [HttpPost("emsp/stop-session")]
        public async Task<IActionResult> EmspStopSession(
            [FromBody] EmspStopSessionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
                return BadRequest(new { success = false, message = "SessionId is required" });

            var session = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId
                    || s.AuthorizationReference == request.SessionId);
            if (session == null)
                return NotFound(new { success = false, message = "Partner session not found" });

            // The CPO hasn't yet told us the real session_id — we have nothing to send them.
            if (session.SessionId == session.AuthorizationReference)
                return StatusCode(200, new
                {
                    success = false,
                    message = "Partner CPO has not yet confirmed this session — it has no session_id to stop yet. Try again shortly."
                });

            if (!session.PartnerCredentialId.HasValue)
                return BadRequest(new { success = false, message = "Session has no associated partner credential" });

            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == session.PartnerCredentialId.Value && p.IsActive);
            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found or inactive" });

            if (string.IsNullOrEmpty(partner.OutboundToken))
                return BadRequest(new { success = false, message = "Partner has no outbound token configured" });

            var commandsUrl = await DiscoverPartnerEndpointAsync(partner, "commands");
            if (commandsUrl == null)
                return StatusCode(200, new { success = false, message = "Could not discover partner commands endpoint" });

            var ourBaseUrl  = _configuration["Ocpi:OurBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var responseUrl = $"{ourBaseUrl}/2.2.1/commands/STOP_SESSION/{session.SessionId}";

            var commandBody = new
            {
                response_url = responseUrl,
                session_id   = session.SessionId
            };

            var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
            http.Timeout = TimeSpan.FromSeconds(15);

            try
            {
                var resp = await http.PostAsJsonAsync(
                    $"{commandsUrl.TrimEnd('/')}/STOP_SESSION", commandBody);
                var body = await resp.Content.ReadAsStringAsync();

                string cmdResult = "UNKNOWN";
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("result", out var resultProp))
                        cmdResult = resultProp.GetString() ?? "UNKNOWN";
                }
                catch { /* ignore parse errors */ }

                _logger.LogInformation(
                    "[Admin/eMSP] STOP_SESSION → partner {PartnerId}: result={Result}, sessionId={SessionId}",
                    session.PartnerCredentialId, cmdResult, session.SessionId);

                return Ok(new
                {
                    success   = string.Equals(cmdResult, "ACCEPTED", StringComparison.OrdinalIgnoreCase),
                    result    = cmdResult,
                    sessionId = session.SessionId,
                    message   = string.Equals(cmdResult, "ACCEPTED", StringComparison.OrdinalIgnoreCase)
                        ? "Stop command accepted by partner CPO"
                        : $"Partner response: {cmdResult}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Admin/eMSP] Error sending STOP_SESSION for session {SessionId}", session.SessionId);
                return StatusCode(200,
                    new { success = false, message = $"Error communicating with partner: {ex.Message}" });
            }
        }

        /// <summary>Role names accepted when discovering a module URL that a partner CPO publishes for us to pull from (as opposed to one they push to).</summary>
        private static readonly string[] PullSourceRoles = { "SENDER", "CPO" };

        /// <summary>
        /// Resolve a partner CPO's own per-kWh energy price for a specific connector (eMSP role) —
        /// used by OCPP.Core.Management's charging-estimate endpoint to quote a real total cost
        /// (partner price + HyCharge's platform fee + tax) instead of the platform fee alone.
        ///
        /// <see cref="OcpiSyncBackgroundService"/> already periodically pulls every CPO partner's
        /// locations/EVSEs/connectors AND their full Tariffs module into our local
        /// <see cref="OcpiPartnerConnector.TariffIds"/> / <see cref="OcpiTariff"/> tables, so the
        /// fast path here is a pure local-DB read — no network call. Live pulls (both for a
        /// connector's tariff_ids and for a specific tariff) are only a fallback for a connector
        /// the periodic sync hasn't reached yet, or a partner that only pushes and omitted
        /// tariff_ids on that push.
        /// </summary>
        [HttpGet("emsp/partner-connector-tariff")]
        public async Task<IActionResult> GetPartnerConnectorTariff(
            [FromQuery] int partnerId,
            [FromQuery] string locationId,
            [FromQuery] string evseUid,
            [FromQuery] string connectorId)
        {
            if (partnerId <= 0 || string.IsNullOrWhiteSpace(locationId) ||
                string.IsNullOrWhiteSpace(evseUid) || string.IsNullOrWhiteSpace(connectorId))
                return BadRequest(new { success = false, message = "partnerId, locationId, evseUid and connectorId are required" });

            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == partnerId && p.IsActive);
            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found or inactive" });

            // 1) Fast path — tariff_ids already captured for this connector by the periodic sync
            //    (or a partner push), no network call.
            var storedTariffIds = await _dbContext.OcpiPartnerLocations
                .Where(l => l.PartnerCredentialId == partnerId && l.LocationId == locationId)
                .Join(_dbContext.OcpiPartnerEvses, l => l.Id, e => e.PartnerLocationId, (l, e) => e)
                .Where(e => e.EvseUid == evseUid)
                .Join(_dbContext.OcpiPartnerConnectors, e => e.Id, c => c.PartnerEvseId, (e, c) => c)
                .Where(c => c.ConnectorId == connectorId)
                .Select(c => c.TariffIds)
                .FirstOrDefaultAsync();

            var tariffIds = ParseTariffIds(storedTariffIds);
            var tariffIdSource = "locally synced connector";

            // 2) Fall back to a live pull from the partner's Locations module — covers a connector
            //    the periodic sync hasn't reached yet, or a partner that never included tariff_ids
            //    on push.
            if (tariffIds.Count == 0)
            {
                if (string.IsNullOrEmpty(partner.OutboundToken))
                    return Ok(UnavailableTariffResult(null,
                        "No locally synced tariff_ids for this connector, and partner has no outbound token configured for a live lookup"));

                tariffIds = await PullConnectorTariffIdsLiveAsync(partner, locationId, evseUid, connectorId);
                tariffIdSource = "live pull from partner";

                if (tariffIds.Count == 0)
                    return Ok(UnavailableTariffResult(null, "Partner connector has no tariff_ids (checked local sync and a live pull)"));
            }

            var tariffId = tariffIds[0];

            // 3) Resolve the tariff itself — local cache first (already populated by
            //    OcpiSyncBackgroundService.PullTariffsFromCpoAsync, or a partner push to
            //    OcpiTariffsController.PutTariff, or a previous live resolution below).
            var cached = await _tariffService.GetTariffAsync(partner.CountryCode, partner.PartyId, tariffId);
            var cachedEnergyPrice = cached?.Elements?
                .SelectMany(e => e.PriceComponents ?? Enumerable.Empty<OcpiPriceComponent>())
                .FirstOrDefault(c => c.Type == TariffDimensionType.Energy)?.Price;

            if (cachedEnergyPrice.HasValue)
            {
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        energyPricePerKwh = cachedEnergyPrice.Value,
                        currency = cached!.Currency?.ToMemberValue(),
                        tariffId,
                        source = "Cached"
                    },
                    message = $"Resolved from locally cached partner tariff (tariff_ids from {tariffIdSource})"
                });
            }

            // 4) Live fetch from the partner's own Tariffs module.
            if (string.IsNullOrEmpty(partner.OutboundToken))
                return Ok(UnavailableTariffResult(tariffId, "Tariff not cached locally, and partner has no outbound token configured for a live lookup"));

            var tariffsUrl = await DiscoverPartnerEndpointAsync(partner, "tariffs", PullSourceRoles);
            if (tariffsUrl == null)
                return Ok(UnavailableTariffResult(tariffId, "Could not discover partner's tariffs endpoint"));

            try
            {
                var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(15);

                var tariffUrl = $"{tariffsUrl.TrimEnd('/')}/{partner.CountryCode}/{partner.PartyId}/{Uri.EscapeDataString(tariffId)}";
                var tariffResp = await http.GetAsync(tariffUrl);
                if (!tariffResp.IsSuccessStatusCode)
                    return Ok(UnavailableTariffResult(tariffId, $"Partner tariffs endpoint returned HTTP {(int)tariffResp.StatusCode}"));

                using var tariffDoc = JsonDocument.Parse(await tariffResp.Content.ReadAsStringAsync());
                if (!tariffDoc.RootElement.TryGetProperty("data", out var tariffData))
                    return Ok(UnavailableTariffResult(tariffId, "Partner tariff response had no data"));

                string? currency = tariffData.TryGetProperty("currency", out var curEl) ? curEl.GetString() : null;

                var elements = new List<OcpiTariffElement>();
                decimal? energyPrice = null;
                if (tariffData.TryGetProperty("elements", out var elementsEl) && elementsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in elementsEl.EnumerateArray())
                    {
                        var components = new List<OcpiPriceComponent>();
                        if (element.TryGetProperty("price_components", out var pcsEl) && pcsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var pc in pcsEl.EnumerateArray())
                            {
                                var typeStr = pc.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
                                decimal? price = pc.TryGetProperty("price", out var priceEl) && priceEl.TryGetDecimal(out var priceVal)
                                    ? priceVal
                                    : null;

                                var component = new OcpiPriceComponent
                                {
                                    Type = OcpiEnumMemberHelper.ParseMemberValue<TariffDimensionType>(typeStr),
                                    Price = price
                                };
                                components.Add(component);

                                if (energyPrice == null && component.Type == TariffDimensionType.Energy)
                                    energyPrice = price;
                            }
                        }
                        elements.Add(new OcpiTariffElement { PriceComponents = components });
                    }
                }

                // Cache it locally for next time — best-effort, a failure here shouldn't fail the estimate.
                try
                {
                    var contractTariff = new OcpiTariff
                    {
                        CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(partner.CountryCode),
                        PartyId = partner.PartyId,
                        Id = tariffId,
                        Currency = OcpiEnumMemberHelper.ParseMemberValue<CurrencyCode>(currency),
                        Elements = elements,
                        LastUpdated = DateTime.UtcNow
                    };
                    await _tariffService.CreateOrUpdateTariffAsync(contractTariff);
                }
                catch (Exception cacheEx)
                {
                    _logger.LogWarning(cacheEx,
                        "[Admin/eMSP] Failed to cache live-fetched tariff {TariffId} for partner {PartnerId}",
                        tariffId, partnerId);
                }

                if (energyPrice == null)
                    return Ok(UnavailableTariffResult(tariffId, "Partner tariff has no ENERGY price component"));

                return Ok(new
                {
                    success = true,
                    data = new { energyPricePerKwh = energyPrice.Value, currency, tariffId, source = "Live" },
                    message = $"Resolved live from partner's tariffs endpoint (tariff_ids from {tariffIdSource})"
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Admin/eMSP] Failed to fetch tariff {TariffId} from partner {PartnerId}", tariffId, partnerId);
                return Ok(UnavailableTariffResult(tariffId, $"Error fetching partner tariff: {ex.Message}"));
            }
        }

        /// <summary>Live fallback for GetPartnerConnectorTariff step 2 — see its doc comment.</summary>
        private async Task<List<string>> PullConnectorTariffIdsLiveAsync(
            OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential partner, string locationId, string evseUid, string connectorId)
        {
            var tariffIds = new List<string>();

            var locationsUrl = await DiscoverPartnerEndpointAsync(partner, "locations", PullSourceRoles);
            if (locationsUrl == null)
                return tariffIds;

            try
            {
                var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(15);

                var connectorUrl = $"{locationsUrl.TrimEnd('/')}/{partner.CountryCode}/{partner.PartyId}/" +
                    $"{Uri.EscapeDataString(locationId)}/{Uri.EscapeDataString(evseUid)}/{Uri.EscapeDataString(connectorId)}";
                var connResp = await http.GetAsync(connectorUrl);
                if (connResp.IsSuccessStatusCode)
                {
                    using var connDoc = JsonDocument.Parse(await connResp.Content.ReadAsStringAsync());
                    if (connDoc.RootElement.TryGetProperty("data", out var connData) &&
                        connData.TryGetProperty("tariff_ids", out var tidsEl) &&
                        tidsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in tidsEl.EnumerateArray())
                        {
                            var s = t.GetString();
                            if (!string.IsNullOrEmpty(s)) tariffIds.Add(s);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "[Admin/eMSP] Partner connector pull returned HTTP {Status} for {Url}",
                        connResp.StatusCode, connectorUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Admin/eMSP] Failed to pull connector {ConnectorId} from partner {PartnerId} for tariff resolution",
                    connectorId, partner.Id);
            }

            return tariffIds;
        }

        private static List<string> ParseTariffIds(string? stored) =>
            string.IsNullOrWhiteSpace(stored)
                ? new List<string>()
                : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        private static object UnavailableTariffResult(string? tariffId, string message) => new
        {
            success = true,
            data = new { energyPricePerKwh = (decimal?)null, currency = (string?)null, tariffId, source = "Unavailable" },
            message
        };

        [HttpGet("emsp/perform-sync")]
        public async Task<IActionResult> PerformSync()
        {
            try
            {
                var ct = new CancellationToken();
                await _syncService.PerformSyncRoundAsync(ct);
                return Ok(new { success = true, message = "Full sync completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Admin/eMSP] Error performing full sync");
                return StatusCode(500, new { success = false, message = $"Error during sync: {ex.Message}" });
            }
        }

        // ── eMSP helper ───────────────────────────────────────────────────────────

        /// <summary>
        /// Discovers a specific module endpoint URL for a partner by walking their /versions
        /// and version-details URLs. Returns the first matching URL or null on failure.
        /// <paramref name="acceptableRoles"/> defaults to RECEIVER/CPO (the role we need for
        /// modules where WE send data to THEM, e.g. Commands); pass SENDER/CPO for modules where
        /// we instead pull data THEY publish (e.g. Locations, Tariffs).
        /// </summary>
        private async Task<string?> DiscoverPartnerEndpointAsync(
            OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential partner,
            string moduleIdentifier,
            string[]? acceptableRoles = null)
        {
            acceptableRoles ??= new[] { "RECEIVER", "CPO" };

            try
            {
                var http = _httpClientFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(10);

                // Step 1: GET /versions
                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var versionsResp = await http.GetAsync(partnerURL);
                if (!versionsResp.IsSuccessStatusCode) return null;

                using var versionsDoc = JsonDocument.Parse(
                    await versionsResp.Content.ReadAsStringAsync());

                string? v221Url = null;
                if (versionsDoc.RootElement.TryGetProperty("data", out var vData) &&
                    vData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in vData.EnumerateArray())
                    {
                        var ver = v.TryGetProperty("version", out var vp) ? vp.GetString() : null;
                        var url = v.TryGetProperty("url",     out var up) ? up.GetString() : null;
                        if (ver == "2.2.1") { v221Url = url; break; }
                        if (ver == "2.2")     v221Url = url;   // keep searching for 2.2.1
                    }
                }

                if (v221Url == null) return null;

                // Step 2: GET version details
                var detailsResp = await http.GetAsync(v221Url);
                if (!detailsResp.IsSuccessStatusCode) return null;

                using var detailsDoc = JsonDocument.Parse(
                    await detailsResp.Content.ReadAsStringAsync());

                if (detailsDoc.RootElement.TryGetProperty("data", out var dData) &&
                    dData.TryGetProperty("endpoints", out var eps) &&
                    eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var id   = ep.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var role = ep.TryGetProperty("role",       out var roleProp) ? roleProp.GetString() : null;
                        if (!string.Equals(id, moduleIdentifier, StringComparison.OrdinalIgnoreCase))
                            continue;

                        bool roleMatch = role == null ||
                            acceptableRoles.Any(r => string.Equals(role, r, StringComparison.OrdinalIgnoreCase));
                        if (roleMatch)
                            return ep.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[Admin] Endpoint discovery failed for partner {Id} module={Module}",
                    partner.Id, moduleIdentifier);
                return null;
            }
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

        public record IssueATokenRequest(string Label, int ExpiryHours = 72);

        public record EmspStartSessionRequest(
            int     PartnerId,
            string  LocationId,
            string? EvseUid,
            string? ConnectorId,
            string  TokenUid,
            string? UserId               = null,
            double? EnergyLimit          = null,
            double? CostLimit            = null,
            int?    TimeLimit            = null,
            double? BatteryIncreaseLimit = null);

        public record EmspStopSessionRequest(string SessionId);
    }
}
