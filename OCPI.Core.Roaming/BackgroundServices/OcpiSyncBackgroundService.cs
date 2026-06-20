using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BitzArt.EnumToMemberValue;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Background service that performs bi-directional OCPI data synchronisation with all
    /// active partner platforms on a configurable interval.
    ///
    /// Direction rules (by partner role):
    ///   EMSP partner  → PULL their tokens;  PUSH our locations + active hosted sessions
    ///   CPO  partner  → PULL their locations, tariffs, sessions and CDRs
    ///   HUB  partner  → both directions
    ///
    /// Endpoint URLs are discovered at runtime by calling the partner's /versions URL and
    /// resolving the 2.2.1 module endpoint list, so no hard-coded URL templates are needed.
    /// </summary>
    /// 
    public interface IOcpiSyncBackgroundService
    {
        public Task PerformSyncRoundAsync(CancellationToken ct);
    }
    public class OcpiSyncBackgroundService : BackgroundService, IOcpiSyncBackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OcpiSyncBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _syncInterval;

        // OCPI standard JSON options — enums as strings, case-insensitive property names.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
            Converters                  = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
        };
        

        public OcpiSyncBackgroundService(
            IServiceProvider services,
            ILogger<OcpiSyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _services      = services;
            _logger        = logger;
            _configuration = configuration;

            var intervalMinutes = configuration.GetValue<int>("OCPI:SyncIntervalMinutes", 5);
            _syncInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        // ── BackgroundService ──────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "OCPI Sync Service started — interval {Interval}", _syncInterval);

            // Delay first run so the host has fully started before we hit the DB.
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformSyncRoundAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in OCPI sync round");
                }

                try
                {
                    await Task.Delay(_syncInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("OCPI Sync Service stopped");
        }

        // ── Sync orchestration ─────────────────────────────────────────────────

        public async Task PerformSyncRoundAsync(CancellationToken ct)
        {
            using var scope     = _services.CreateScope();
            var dbContext       = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();
            var locationService = scope.ServiceProvider.GetRequiredService<IOcpiLocationService>();
            var sessionService  = scope.ServiceProvider.GetRequiredService<IOcpiSessionService>();
            var cdrService      = scope.ServiceProvider.GetRequiredService<IOcpiCdrService>();
            var tariffService   = scope.ServiceProvider.GetRequiredService<IOcpiTariffService>();
            var tokenService    = scope.ServiceProvider.GetRequiredService<IOcpiTokenService>();
            var httpFactory     = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            var partners = await dbContext.OcpiPartnerCredentials
                .Where(p => p.IsActive)
                .ToListAsync(ct);

            if (!partners.Any())
            {
                _logger.LogDebug("No active OCPI partners — skipping sync");
                return;
            }

            _logger.LogInformation("OCPI sync round — {Count} partner(s)", partners.Count);

            // Pre-compute the set of charge points that are currently online (via OCPP server
            // /ConnectionStatus API).  Done once per round so all partner loops share the result.
            var stationChargePointIds = await dbContext.ChargingStations
                .Where(s => s.Active == 1 && !string.IsNullOrEmpty(s.ChargingPointId))
                .Select(s => s.ChargingPointId)
                .Distinct()
                .ToListAsync(ct);

            var onlineChargePoints = await GetOnlineChargePointsAsync(httpFactory, stationChargePointIds, ct);

            _logger.LogDebug(
                "OCPI sync round: {Online}/{Total} charge point(s) online",
                onlineChargePoints.Count, stationChargePointIds.Count);

            foreach (var partner in partners)
            {
                if (ct.IsCancellationRequested) break;

                if (string.IsNullOrWhiteSpace(partner.OutboundToken))
                {
                    _logger.LogWarning(
                        "Partner {CountryCode}-{PartyId} has no OutboundToken — skipping sync until re-handshake",
                        partner.CountryCode, partner.PartyId);
                    continue;
                }

                try
                {
                    var http = CreatePartnerHttpClient(httpFactory, partner);

                    var endpoints = await DiscoverEndpointsAsync(http, partner, ct);
                    if (endpoints == null) continue;   // logged inside

                    var role = partner.Role?.ToUpperInvariant() ?? "CPO";

                    if (role is "CPO" or "HUB")
                        await PullFromCpoPartnerAsync(partner, endpoints, http, locationService, sessionService, cdrService, tariffService, dbContext, ct);

                    if (role is "EMSP" or "HUB")
                    {
                        await PullTokensFromEmspAsync(partner, endpoints, http, tokenService, ct);
                        await PushLocationsToEmspAsync(partner, endpoints, http, locationService, ct);
                        await PushHostedSessionsToEmspAsync(partner, endpoints, http, dbContext, ct);
                        await PushEvseStatusesToEmspAsync(partner, endpoints, http, dbContext, onlineChargePoints, ct);
                    }

                    partner.LastSyncOn = DateTime.UtcNow;
                    dbContext.OcpiPartnerCredentials.Update(partner);
                    await dbContext.SaveChangesAsync(ct);
                    
                    _logger.LogInformation(
                        "Sync complete for partner {CountryCode}-{PartyId}",
                        partner.CountryCode, partner.PartyId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Sync failed for partner {CountryCode}-{PartyId}",
                        partner.CountryCode, partner.PartyId);
                }
            }
        }

        // ── Endpoint discovery ─────────────────────────────────────────────────

        /// <summary>
        /// Calls the partner's /versions URL, then the 2.2.1 details URL, and returns a
        /// lookup keyed by "{module}_{role}" (lower-case), e.g. "locations_sender".
        /// Returns null if discovery fails.
        /// </summary>
        private async Task<Dictionary<string, string>?> DiscoverEndpointsAsync(
            HttpClient http,
            OcpiPartnerCredential partner,
            CancellationToken ct)
        {
            try
            {
                // 1. GET /versions → list of version objects
                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var versionsResp = await http.GetAsync(partnerURL, ct);
                if (!versionsResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Partner {CC}-{Party} versions endpoint returned {Status}",
                        partner.CountryCode, partner.PartyId, versionsResp.StatusCode);
                    return null;
                }

                var versionsEnvelope = await DeserializeAsync<OcpiApiEnvelope<List<Services.OcpiVersionInfo>>>(versionsResp, ct);
                var v221 = versionsEnvelope?.Data?.FirstOrDefault(v =>
                    string.Equals(v.Version, "2.2.1", StringComparison.OrdinalIgnoreCase));

                if (v221 == null)
                {
                    _logger.LogWarning(
                        "Partner {CC}-{Party} does not advertise OCPI 2.2.1",
                        partner.CountryCode, partner.PartyId);
                    return null;
                }

                // 2. GET /versions/2.2.1 → endpoint list
                var detailsResp = await http.GetAsync(v221.Url, ct);
                if (!detailsResp.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Partner {CC}-{Party} version details endpoint returned {Status}",
                        partner.CountryCode, partner.PartyId, detailsResp.StatusCode);
                    return null;
                }

                var detailsEnvelope = await DeserializeAsync<OcpiApiEnvelope<Services.OcpiVersionDetails>>(detailsResp, ct);
                var endpoints = detailsEnvelope?.Data?.Endpoints;

                if (endpoints == null || !endpoints.Any())
                {
                    _logger.LogWarning(
                        "Partner {CC}-{Party} returned empty endpoint list",
                        partner.CountryCode, partner.PartyId);
                    return null;
                }

                var map = endpoints.ToDictionary(
                    e => $"{e.Identifier.ToLowerInvariant()}_{e.Role.ToLowerInvariant()}",
                    e => e.Url,
                    StringComparer.OrdinalIgnoreCase);

                _logger.LogDebug(
                    "Discovered {Count} endpoints for partner {CC}-{Party}",
                    map.Count, partner.CountryCode, partner.PartyId);

                return map;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Endpoint discovery failed for partner {CC}-{Party}",
                    partner.CountryCode, partner.PartyId);
                return null;
            }
        }

        // ── PULL from CPO partner ──────────────────────────────────────────────

        private async Task PullFromCpoPartnerAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiLocationService locationService,
            IOcpiSessionService sessionService,
            IOcpiCdrService cdrService,
            IOcpiTariffService tariffService,
            OCPPCoreContext dbContext,
            CancellationToken ct)
        {
            await PullLocationsFromCpoAsync(partner, endpoints, http, locationService, ct);
            await PullTariffsFromCpoAsync(partner, endpoints, http, tariffService, ct);
            await PullSessionsFromCpoAsync(partner, endpoints, http, sessionService, ct);
            await PullCdrsFromCpoAsync(partner, endpoints, http, cdrService, ct);
        }

        private async Task PullLocationsFromCpoAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiLocationService locationService,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("locations_sender", out var url))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no locations sender endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            var dateFrom = partner.LastSyncOn?.ToString("yyyy-MM-dd");
            var pulled   = 0;

            await foreach (var location in PaginateAsync<OCPI.Core.Roaming.Services.OcpiLocation>(http, url, dateFrom, ct))
            {
                try   { await locationService.StorePartnerLocationAsync(partner.Id, location); pulled++; }
                catch (Exception ex) { _logger.LogError(ex, "Failed to store location {Id}", location.Id); }

                // Pull nested EVSEs / connectors
                if (location.Evses == null) continue;
                var dbLocationId = await locationService.GetPartnerLocationDbIdAsync(
                    partner.CountryCode, partner.PartyId, location.Id!);

                if (dbLocationId == null) continue;

                foreach (var evse in location.Evses)
                {
                    try   { await locationService.StorePartnerEvseAsync(dbLocationId.Value, evse); }
                    catch (Exception ex) { _logger.LogError(ex, "Failed to store EVSE {Uid}", evse.Uid); continue; }

                    if (evse.Connectors == null) continue;
                    var dbEvseId = await locationService.GetPartnerEvseDbIdAsync(dbLocationId.Value, evse.Uid!);
                    if (dbEvseId == null) continue;

                    foreach (var connector in evse.Connectors)
                    {
                        try   { await locationService.StorePartnerConnectorAsync(dbEvseId.Value, connector); }
                        catch (Exception ex) { _logger.LogError(ex, "Failed to store connector {Id}", connector.Id); }
                    }
                }
            }

            _logger.LogInformation("Pulled {Count} locations from CPO partner {CC}-{Party}", pulled, partner.CountryCode, partner.PartyId);
        }

        private async Task PullTariffsFromCpoAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiTariffService tariffService,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("tariffs_sender", out var url))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no tariffs sender endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            var dateFrom = partner.LastSyncOn?.ToString("o");
            var pulled   = 0;

            await foreach (var tariff in PaginateAsync<Contracts.OcpiTariff>(http, url, dateFrom, ct))
            {
                try   { await tariffService.CreateOrUpdateTariffAsync(tariff); pulled++; }
                catch (Exception ex) { _logger.LogError(ex, "Failed to store tariff {Id}", tariff.Id); }
            }

            _logger.LogInformation("Pulled {Count} tariffs from CPO partner {CC}-{Party}", pulled, partner.CountryCode, partner.PartyId);
        }

        private async Task PullSessionsFromCpoAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiSessionService sessionService,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("sessions_sender", out var url))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no sessions sender endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            var dateFrom = partner.LastSyncOn?.ToString("o");
            var pulled   = 0;

            await foreach (var session in PaginateAsync<OcpiSession>(http, url, dateFrom, ct))
            {
                try   { await sessionService.StorePartnerSessionAsync(partner.Id, session); pulled++; }
                catch (Exception ex) { _logger.LogError(ex, "Failed to store session {Id}", session.Id); }
            }

            _logger.LogInformation("Pulled {Count} sessions from CPO partner {CC}-{Party}", pulled, partner.CountryCode, partner.PartyId);
        }

        private async Task PullCdrsFromCpoAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiCdrService cdrService,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("cdrs_sender", out var url))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no CDRs sender endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            // Always pull CDRs from last sync so we don't miss settlement records.
            var dateFrom = partner.LastSyncOn?.ToString("o");
            var pulled   = 0;

            await foreach (var cdr in PaginateAsync<Contracts.OcpiCdr>(http, url, dateFrom, ct))
            {
                try   { await cdrService.CreateCdrAsync(cdr, partner.Id); pulled++; }
                catch (Exception ex) { _logger.LogError(ex, "Failed to store CDR {Id}", cdr.Id); }
            }

            _logger.LogInformation("Pulled {Count} CDRs from CPO partner {CC}-{Party}", pulled, partner.CountryCode, partner.PartyId);
        }

        // ── PULL from EMSP partner ─────────────────────────────────────────────

        private async Task PullTokensFromEmspAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiTokenService tokenService,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("tokens_sender", out var url))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no tokens sender endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            var dateFrom = partner.LastSyncOn?.ToString("o");
            var pulled   = 0;

            await foreach (var token in PaginateAsync<Contracts.OcpiToken>(http, url, dateFrom, ct))
            {
                try   { await tokenService.StorePartnerTokenAsync(partner.Id, token); pulled++; }
                catch (Exception ex) { _logger.LogError(ex, "Failed to store token {Uid}", token.Uid); }
            }

            _logger.LogInformation("Pulled {Count} tokens from EMSP partner {CC}-{Party}", pulled, partner.CountryCode, partner.PartyId);
        }

        // ── PUSH to EMSP partner ───────────────────────────────────────────────

        private async Task PushLocationsToEmspAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            IOcpiLocationService locationService,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("locations_receiver", out var baseUrl))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no locations receiver endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId     = _configuration["OCPI:PartyId"]     ?? "CPO";

            var locations = await locationService.GetOurLocationsAsync(0, 500);
            var pushed    = 0;
            var failed    = 0;

            foreach (var location in locations)
            {
                if (ct.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(location.Id)) continue;

                var url = $"{baseUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{location.Id}";
                try
                {
                    var resp = await http.PutAsJsonAsync(url, location, _jsonOptions, ct);
                    if (resp.IsSuccessStatusCode)
                        pushed++;
                    else
                    {
                        failed++;
                        _logger.LogWarning("PUT location {Id} to {CC}-{Party} → {Status}", location.Id, partner.CountryCode, partner.PartyId, resp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Failed to push location {Id} to partner {CC}-{Party}", location.Id, partner.CountryCode, partner.PartyId);
                }
            }

            _logger.LogInformation(
                "Pushed locations to EMSP {CC}-{Party}: {Pushed} ok, {Failed} failed",
                partner.CountryCode, partner.PartyId, pushed, failed);
        }

        private async Task PushHostedSessionsToEmspAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            OCPPCoreContext dbContext,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("sessions_receiver", out var baseUrl))
            {
                _logger.LogDebug("Partner {CC}-{Party} has no sessions receiver endpoint", partner.CountryCode, partner.PartyId);
                return;
            }

            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId     = _configuration["OCPI:PartyId"]     ?? "CPO";

            // Only push sessions that this partner initiated at our stations.
            var cutoff = partner.LastSyncOn ?? DateTime.UtcNow.AddDays(-1);
            var sessions = await dbContext.OcpiHostedSessions
                .Where(s => s.PartnerCredentialId == partner.Id
                    && (s.Status == "ACTIVE" || s.LastUpdated >= cutoff))
                .ToListAsync(ct);

            var pushed = 0;
            var failed = 0;

            foreach (var hosted in sessions)
            {
                if (ct.IsCancellationRequested) break;

                var session = MapHostedSessionToOcpiSession(hosted, ourCountryCode, ourPartyId);
                var url     = $"{baseUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{hosted.SessionId}";

                try
                {
                    var resp = await http.PutAsJsonAsync(url, session, _jsonOptions, ct);
                    if (resp.IsSuccessStatusCode)
                        pushed++;
                    else
                    {
                        failed++;
                        _logger.LogWarning("PUT session {Id} to {CC}-{Party} → {Status}", hosted.SessionId, partner.CountryCode, partner.PartyId, resp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Failed to push session {Id} to partner {CC}-{Party}", hosted.SessionId, partner.CountryCode, partner.PartyId);
                }
            }

            _logger.LogInformation(
                "Pushed sessions to EMSP {CC}-{Party}: {Pushed} ok, {Failed} failed",
                partner.CountryCode, partner.PartyId, pushed, failed);
        }

        // ── PUSH EVSE status to EMSP partner ──────────────────────────────────

        /// <summary>
        /// Reads real-time connector status from <c>ConnectorStatuses</c> (populated by OCPP
        /// StatusNotification messages) and PATCHes each EVSE status to the EMSP partner so
        /// they always see the live availability of our chargers.
        /// </summary>
        private async Task PushEvseStatusesToEmspAsync(
            OcpiPartnerCredential partner,
            Dictionary<string, string> endpoints,
            HttpClient http,
            OCPPCoreContext dbContext,
            HashSet<string> onlineChargePoints,
            CancellationToken ct)
        {
            if (!endpoints.TryGetValue("locations_receiver", out var baseUrl))
            {
                _logger.LogDebug(
                    "Partner {CC}-{Party} has no locations_receiver endpoint — skipping EVSE status push",
                    partner.CountryCode, partner.PartyId);
                return;
            }

            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId     = _configuration["OCPI:PartyId"]     ?? "CPO";

            var stations = await dbContext.ChargingStations
                .Where(s => s.Active == 1 && !string.IsNullOrEmpty(s.ChargingPointId))
                .ToListAsync(ct);

            if (stations.Count == 0) return;

            var chargePointIds = stations.Select(s => s.ChargingPointId).Distinct().ToList();

            // Real-time status from OCPP StatusNotification messages
            var connectorStatuses = await dbContext.ConnectorStatuses
                .Where(cs => chargePointIds.Contains(cs.ChargePointId) && cs.Active == 1)
                .ToListAsync(ct);

            var statusByChargePoint = connectorStatuses
                .GroupBy(cs => cs.ChargePointId)
                .ToDictionary(g => g.Key, g => g.Select(cs => cs.LastStatus).ToList());

            var hubIds = stations.Select(s => s.ChargingHubId).Distinct().ToList();
            var hubs   = await dbContext.ChargingHubs
                .Where(h => hubIds.Contains(h.RecId) && h.Active == 1)
                .ToDictionaryAsync(h => h.RecId, ct);

            // Chargers with an active OCPI hosted session are provably in use.  Override any
            // stale ConnectorStatus or failed online-check so partners see CHARGING, not OUTOFORDER.
            var activeSessionChargePointIds = await dbContext.OcpiHostedSessions
                .Where(s => s.Status == "ACTIVE" && !string.IsNullOrEmpty(s.ChargePointId))
                .Select(s => s.ChargePointId!)
                .Distinct()
                .ToListAsync(ct);
            var activeSessionSet = new HashSet<string>(activeSessionChargePointIds, StringComparer.OrdinalIgnoreCase);

            int pushed = 0, failed = 0;

            foreach (var station in stations)
            {
                if (ct.IsCancellationRequested) break;
                if (!hubs.TryGetValue(station.ChargingHubId, out var hub)) continue;

                string ocpiStatus;
                if (activeSessionSet.Contains(station.ChargingPointId))
                {
                    // Active OCPI session — charger is definitely in use; report CHARGING
                    // regardless of the online check or stale ConnectorStatuses value.
                    ocpiStatus = "CHARGING";
                }
                else if (!onlineChargePoints.Contains(station.ChargingPointId))
                {
                    // Offline charge points must be reported as OUTOFORDER regardless of the
                    // last status stored in ConnectorStatuses (which could be stale).
                    ocpiStatus = "OUTOFORDER";
                }
                else if (statusByChargePoint.TryGetValue(station.ChargingPointId, out var rawStatuses))
                {
                    ocpiStatus = DeriveEvseOcpiStatus(rawStatuses);
                }
                else
                {
                    ocpiStatus = "UNKNOWN";
                }

                // OCPI PUT URL: /{cc}/{partyId}/{locationId}/{evseUid}
                var url = $"{baseUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{hub.RecId}/{station.RecId}";

                try
                {
                    var patch = new { status = ocpiStatus, last_updated = DateTime.UtcNow };
                    var resp  = await http.PatchAsJsonAsync(url, patch, _jsonOptions, ct);

                    if (resp.IsSuccessStatusCode)
                        pushed++;
                    else
                    {
                        failed++;
                        _logger.LogWarning(
                            "PATCH EVSE {EvseUid}={Status} to partner {CC}-{Party} → HTTP {Code}",
                            station.RecId, ocpiStatus, partner.CountryCode, partner.PartyId, resp.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex,
                        "Failed to PATCH EVSE status {EvseUid} to partner {CC}-{Party}",
                        station.RecId, partner.CountryCode, partner.PartyId);
                }
            }

            _logger.LogInformation(
                "EVSE status push to EMSP {CC}-{Party}: {Pushed} ok, {Failed} failed",
                partner.CountryCode, partner.PartyId, pushed, failed);
        }

        /// <summary>
        /// Calls the OCPP server's /ConnectionStatus API for each charge-point ID and returns
        /// the subset that are currently online.  Mirrors the same check in
        /// <c>GunStatusSyncService.GetChargePointOnlineAsync</c> so EVSE status is never
        /// based solely on (potentially stale) <c>ConnectorStatuses</c> rows.
        /// When <c>ServerApiUrl</c> is not configured every ID is treated as online so
        /// real connector statuses are still pushed.
        /// </summary>
        private async Task<HashSet<string>> GetOnlineChargePointsAsync(
            IHttpClientFactory httpFactory,
            IEnumerable<string?> chargePointIds,
            CancellationToken ct)
        {
            var serverApiUrl = _configuration["ServerApiUrl"];
            var ids = chargePointIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

            if (string.IsNullOrEmpty(serverApiUrl))
            {
                _logger.LogDebug(
                    "ServerApiUrl not configured — treating all charge points as online for EVSE status push");
                return new HashSet<string>(ids!, StringComparer.OrdinalIgnoreCase);
            }

            var apiKey  = _configuration["ApiKey"] ?? string.Empty;
            var baseUrl = serverApiUrl.TrimEnd('/');
            var online  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in ids)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var client = httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

                    var resp = await client.GetAsync(
                        $"{baseUrl}/API/ConnectionStatus/{Uri.EscapeDataString(id!)}", ct);

                    if (!resp.IsSuccessStatusCode) continue;

                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("isOnline", out var prop) && prop.GetBoolean())
                        online.Add(id!);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "GetOnlineChargePointsAsync: status check failed for {Id} — treating as offline", id);
                }
            }

            return online;
        }

        /// <summary>
        /// Aggregates OCPP connector statuses for one EVSE into a single OCPI status string.
        /// Covers all OCPP 1.6 connector status values that indicate an active or transitional
        /// charging session (Preparing, SuspendedEV, SuspendedEVSE, Finishing) so they are
        /// never reported as OUTOFORDER while a partner's user is connected.
        /// Priority: CHARGING > BLOCKED > RESERVED > OUTOFORDER > AVAILABLE > UNKNOWN.
        /// </summary>
        private static string DeriveEvseOcpiStatus(IEnumerable<string?> ocppStatuses)
        {
            var statuses = ocppStatuses.Select(s => s?.ToUpperInvariant()).ToList();
            if (statuses.Any(s => s is "OCCUPIED" or "CHARGING"
                                     or "PREPARING" or "SUSPENDEDEV"
                                     or "SUSPENDEDEVSE" or "FINISHING"))
                return "CHARGING";
            if (statuses.Any(s => s is "UNAVAILABLE" or "FAULTED")) return "BLOCKED";
            if (statuses.Any(s => s == "RESERVED"))                  return "RESERVED";
            if (statuses.Any(s => s == "OFFLINE"))                   return "OUTOFORDER";
            if (statuses.All(s => s == "AVAILABLE"))                 return "AVAILABLE";
            return "UNKNOWN";
        }

        // ── Pagination helper ──────────────────────────────────────────────────

        /// <summary>
        /// Iterates all pages of an OCPI sender endpoint, yielding items one by one.
        /// Adds <c>date_from</c> and <c>limit</c> query parameters; follows the OCPI
        /// <c>Link</c> header for subsequent pages.
        /// </summary>
        private async IAsyncEnumerable<T> PaginateAsync<T>(
            HttpClient http,
            string url,
            string? dateFrom,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            const int pageSize = 100;

            // Build initial URL with query params
            var sep = url.Contains('?') ? "&" : "?";
            var firstUrl = $"{url}{sep}limit={pageSize}";
            if (!string.IsNullOrEmpty(dateFrom))
                firstUrl += $"&date_from={dateFrom}";

            string? nextUrl = firstUrl;

            while (nextUrl != null)
            {
                if (ct.IsCancellationRequested) yield break;

                HttpResponseMessage resp;
                try
                {
                    resp = await http.GetAsync(nextUrl, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "HTTP GET failed: {Url}", nextUrl);
                    yield break;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GET {Url} returned {Status}", nextUrl, resp.StatusCode);
                    yield break;
                }

                OcpiApiEnvelope<List<T>>? envelope;
                try
                {
                    envelope = await DeserializeAsync<OcpiApiEnvelope<List<T>>>(resp, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize OCPI response from {Url}", nextUrl);
                    yield break;
                }

                if (envelope?.Data == null) yield break;

                foreach (var item in envelope.Data)
                    yield return item;

                // Follow OCPI Link header for next page (format: <url>; rel="next")
                nextUrl = ParseLinkNextHeader(resp);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private HttpClient CreatePartnerHttpClient(IHttpClientFactory factory, OcpiPartnerCredential partner)
        {
            var http = factory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            var tokenStr = string.IsNullOrEmpty(partner.OutboundToken) ? partner.Token : partner.OutboundToken;
            http.DefaultRequestHeaders.Add("Authorization", $"Token  {Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenStr))}");
            http.Timeout = TimeSpan.FromSeconds(30);
            return http;
        }

        private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage resp, CancellationToken ct)
        {
            var content = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }

        /// <summary>Parses the OCPI Link header and returns the "next" URL, or null.</summary>
        private static string? ParseLinkNextHeader(HttpResponseMessage resp)
        {
            if (!resp.Headers.TryGetValues("Link", out var linkValues))
                return null;

            foreach (var value in linkValues)
            {
                // Format: <https://...>; rel="next"
                var parts = value.Split(';');
                if (parts.Length < 2) continue;
                if (!parts[1].Trim().Equals("rel=\"next\"", StringComparison.OrdinalIgnoreCase)) continue;

                var urlPart = parts[0].Trim();
                return urlPart.TrimStart('<').TrimEnd('>');
            }

            return null;
        }

        private OcpiSession MapHostedSessionToOcpiSession(
            OcpiHostedSession s,
            string countryCode,
            string partyId)
        {
            var isActive = s.EndDateTime == null || s.EndDateTime == DateTime.MinValue;
            return new OcpiSession
            {
                CountryCode   = Enum.Parse<CountryCode>(countryCode),
                PartyId       = partyId,
                Id            = s.SessionId,
                StartDateTime = s.StartDateTime,
                EndDateTime   = isActive ? null : s.EndDateTime,
                Kwh           = Convert.ToDecimal(s.TotalEnergy ?? 0),
                AuthMethod    = AuthMethodType.Command,
                LocationId    = s.LocationId,
                EvseId        = s.EvseUid,
                ConnectorId   = s.ConnectorId,
                Status        = isActive ? SessionStatus.Active : SessionStatus.Completed,
                Currency      = CurrencyCode.IndianRupee,
                LastUpdated   = s.LastUpdated == DateTime.MinValue ? DateTime.UtcNow : s.LastUpdated,
                CdrToken      = string.IsNullOrEmpty(s.TokenUid)
                                    ? null
                                    : new OcpiCdrToken { Uid = s.TokenUid, Type = TokenType.Rfid }
            };
        }
    }

    // ── Response envelope ──────────────────────────────────────────────────────

    /// <summary>Standard OCPI response wrapper used for deserialising partner API responses.</summary>
    internal sealed class OcpiApiEnvelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }

        [JsonPropertyName("status_message")]
        public string? StatusMessage { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime? Timestamp { get; set; }
    }
}
