using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiCommandService : IOcpiCommandService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiCommandService> _logger;
        private readonly IConfiguration _config;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OcpiCommandService(
            OCPPCoreContext dbContext,
            ILogger<OcpiCommandService> logger,
            IConfiguration config,
            IServiceScopeFactory scopeFactory,
            IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        // ───────────────────────────── START SESSION ─────────────────────────────

        public async Task<(CommandResponseType Result, string? SessionId)> HandleStartSessionAsync(OcpiStartSessionCommand command)
        {
            _logger.LogInformation("Handling START_SESSION for location={LocationId} evse={EvseUid} connector={ConnectorId}",
                command.LocationId, command.EvseUid, command.ConnectorId);

            var (chargePointId, connectorNumber, error) =
                await ResolveOcppIdsAsync(command.LocationId, command.EvseUid, command.ConnectorId);

            if (error != null)
            {
                _logger.LogWarning("START_SESSION resolution failed: {Error}", error);
                return (CommandResponseType.Rejected, null);
            }

            var tokenUid   = command.Token?.Uid ?? "REMOTE";
            var sessionId  = Guid.NewGuid().ToString();
            var countryCode = _config.GetValue<string>("OCPI:CountryCode") ?? "IN";
            var partyId     = _config.GetValue<string>("OCPI:PartyId")     ?? "CPO";

            // Capture the requesting eMSP partner now — HttpContext is only valid for the
            // duration of this request, not inside the fire-and-forget background task below.
            // OcpiAuthorizeAttribute stashes the resolved partner here for partner-initiated
            // commands; it stays null for locally-initiated commands (e.g. /admin/commands/start).
            var requestingPartner = _httpContextAccessor.HttpContext?.Items["OcpiPartner"] as OcpiPartnerCredential;
            var authorizationReference = command.AuthorizationReference;

            // Snapshot the highest TransactionId for this chargepoint/connector BEFORE sending the
            // OCPP command — used to identify the new transaction that appears after StartTransaction.
            int baselineTransactionId = await _dbContext.Transactions
                .Where(t => t.ChargePointId == chargePointId && t.ConnectorId == connectorNumber)
                .OrderByDescending(t => t.TransactionId)
                .Select(t => t.TransactionId)
                .FirstOrDefaultAsync();

            _logger.LogInformation(
                "START_SESSION baseline TransactionId for {ChargePointId}/{ConnectorNumber}: {Baseline}",
                chargePointId, connectorNumber, baselineTransactionId);

            // Fire-and-forget: call OCPP charger, create session record, then post result to response_url
            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, msg) = await CallOcppStartTransactionAsync(chargePointId!, connectorNumber, tokenUid);

                    if (success)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();

                        // Poll for the new OCPP transaction row (up to 16 × 1.5 s = 24 s)
                        OCPP.Core.Database.Transaction? ocppTransaction = null;
                        const int maxPollAttempts = 16;
                        const int pollIntervalMs  = 1500;

                        for (int attempt = 1; attempt <= maxPollAttempts; attempt++)
                        {
                            await Task.Delay(pollIntervalMs);

                            ocppTransaction = await db.Transactions
                                .Where(t => t.ChargePointId == chargePointId &&
                                            t.ConnectorId   == connectorNumber &&
                                            t.TransactionId > baselineTransactionId)
                                .OrderByDescending(t => t.TransactionId)
                                .FirstOrDefaultAsync();

                            if (ocppTransaction != null)
                            {
                                _logger.LogInformation(
                                    "OCPI START_SESSION: new transaction found on attempt {Attempt}: TransactionId={TxId}",
                                    attempt, ocppTransaction.TransactionId);
                                break;
                            }

                            _logger.LogDebug(
                                "OCPI START_SESSION poll {Attempt}/{Max}: no new transaction yet for {ChargePointId}/{ConnectorNumber}",
                                attempt, maxPollAttempts, chargePointId, connectorNumber);
                        }

                        if (ocppTransaction == null)
                        {
                            _logger.LogWarning(
                                "OCPI START_SESSION: no transaction appeared after {Max} attempts for {ChargePointId}/{ConnectorNumber} — session {SessionId} created without TransactionId",
                                maxPollAttempts, chargePointId, connectorNumber, sessionId);
                        }

                        // Resolve chargepoint → station → gun so EvseUid/ConnectorId are set correctly.
                        var station = await db.ChargingStations
                            .FirstOrDefaultAsync(s => s.ChargingPointId == chargePointId);

                        var gun = station != null
                            ? await db.ChargingGuns
                                .FirstOrDefaultAsync(g => g.ChargingStationId == station.RecId
                                                       && g.ConnectorId == connectorNumber.ToString())
                            : null;

                        var hostedSession = new OcpiHostedSession
                        {
                            SessionId       = sessionId,
                            TransactionId   = ocppTransaction?.TransactionId,
                            ChargePointId   = chargePointId!,
                            ConnectorNumber = connectorNumber,
                            EvseUid         = station?.RecId ?? command.EvseUid,
                            ConnectorId     = gun?.RecId     ?? command.ConnectorId,
                            TokenUid        = tokenUid,
                            LocationId      = command.LocationId,
                            // StartDateTime   = ocppTransaction?.StartTime ?? DateTime.Now,
                            StartDateTime   = DateTime.Now,
                            Status          = "ACTIVE",
                            PartnerCredentialId    = requestingPartner?.Id,
                            AuthorizationReference = authorizationReference
                        };

                        await db.OcpiHostedSessions.AddAsync(hostedSession);
                        await db.SaveChangesAsync();

                        _logger.LogInformation(
                            "Created OcpiHostedSession {SessionId} with TransactionId={TxId}",
                            sessionId, ocppTransaction?.TransactionId.ToString() ?? "none");

                        // The CPO (us) assigns the session_id — push it to the requesting eMSP
                        // in real time instead of waiting for the next periodic sync round.
                        await PushHostedSessionToPartnerAsync(hostedSession, requestingPartner);
                    }

                    var resultType = success ? CommandResultType.Accepted : CommandResultType.Rejected;
                    await PostCommandResultAsync(command.ResponseUrl, resultType, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in START_SESSION background processing");
                }
            });

            return (CommandResponseType.Accepted, sessionId);
        }

        // ───────────────────────────── STOP SESSION ──────────────────────────────

        public async Task<CommandResponseType> HandleStopSessionAsync(OcpiStopSessionCommand command)
        {
            _logger.LogInformation("Handling STOP_SESSION for session={SessionId}", command.SessionId);

            // Find the hosted session (CPO-role: session at our station)
            var ocpiSession = await _dbContext.OcpiHostedSessions
                .FirstOrDefaultAsync(s => s.SessionId == command.SessionId);

            if (ocpiSession == null)
            {
                _logger.LogWarning("STOP_SESSION: session {SessionId} not found", command.SessionId);
                return CommandResponseType.Unknown_session;
            }

            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == ocpiSession.EvseUid && s.Active == 1);

            if (station == null)
            {
                _logger.LogWarning("STOP_SESSION: station {EvseUid} not found", ocpiSession.EvseUid);
                return CommandResponseType.Rejected;
            }

            var gun = await _dbContext.ChargingGuns
                .FirstOrDefaultAsync(g => g.ChargingStationId == station.RecId
                    && g.RecId == ocpiSession.ConnectorId && g.Active == 1);

            if (gun == null)
            {
                _logger.LogWarning("STOP_SESSION: connector {ConnectorId} not found", ocpiSession.ConnectorId);
                return CommandResponseType.Rejected;
            }

            if (!int.TryParse(gun.ConnectorId, out var connectorNumber))
            {
                _logger.LogWarning("STOP_SESSION: invalid connector number {ConnectorId}", gun.ConnectorId);
                return CommandResponseType.Rejected;
            }

            var chargePointId = station.ChargingPointId;

            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, msg) = await CallOcppStopTransactionAsync(chargePointId, connectorNumber);
                    var resultType = success ? CommandResultType.Accepted : CommandResultType.Rejected;
                    await PostCommandResultAsync(command.ResponseUrl, resultType, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in STOP_SESSION background processing");
                }
            });

            return CommandResponseType.Accepted;
        }

        // ───────────────────────────── RESERVE NOW ───────────────────────────────

        public Task<CommandResponseType> HandleReserveNowAsync(OcpiReserveNowCommand command)
        {
            _logger.LogInformation("RESERVE_NOW not supported, responding NOT_SUPPORTED");
            // ReserveNow requires OCPP 1.6 remote reservation support via /API endpoint — not exposed
            return Task.FromResult(CommandResponseType.Not_supported);
        }

        // ─────────────────────────── CANCEL RESERVATION ──────────────────────────

        public Task<CommandResponseType> HandleCancelReservationAsync(OcpiCancelReservationCommand command)
        {
            _logger.LogInformation("CANCEL_RESERVATION not supported, responding NOT_SUPPORTED");
            return Task.FromResult(CommandResponseType.Not_supported);
        }

        // ─────────────────────────── UNLOCK CONNECTOR ────────────────────────────

        public async Task<CommandResponseType> HandleUnlockConnectorAsync(OcpiUnlockConnectorCommand command)
        {
            _logger.LogInformation("Handling UNLOCK_CONNECTOR for location={LocationId} evse={EvseUid} connector={ConnectorId}",
                command.LocationId, command.EvseUid, command.ConnectorId);

            var (chargePointId, connectorNumber, error) =
                await ResolveOcppIdsAsync(command.LocationId, command.EvseUid, command.ConnectorId);

            if (error != null)
            {
                _logger.LogWarning("UNLOCK_CONNECTOR resolution failed: {Error}", error);
                return CommandResponseType.Rejected;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, msg) = await CallOcppUnlockConnectorAsync(chargePointId!, connectorNumber);
                    var resultType = success ? CommandResultType.Accepted : CommandResultType.Rejected;
                    await PostCommandResultAsync(command.ResponseUrl, resultType, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in UNLOCK_CONNECTOR background processing");
                }
            });

            return CommandResponseType.Accepted;
        }

        // ─────────────────────────── PRIVATE HELPERS ─────────────────────────────

        private async Task<(string? ChargePointId, int ConnectorNumber, string? Error)> ResolveOcppIdsAsync(
            string? locationId, string? evseUid, string? connectorId)
        {
            if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(evseUid))
                return (null, 0, "LocationId and EvseUid are required");

            // evseUid = ChargingStation.RecId
            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == evseUid
                    && s.ChargingHubId == locationId
                    && s.Active == 1);

            if (station == null)
                return (null, 0, $"EVSE {evseUid} not found at location {locationId}");

            int connectorNumber = 1;
            if (!string.IsNullOrEmpty(connectorId))
            {
                // connectorId = ChargingGuns.RecId
                var gun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.RecId == connectorId
                        && g.ChargingStationId == station.RecId
                        && g.Active == 1);

                if (gun == null)
                    return (null, 0, $"Connector {connectorId} not found on EVSE {evseUid}");

                if (!int.TryParse(gun.ConnectorId, out connectorNumber))
                    return (null, 0, $"Invalid OCPP connector number for gun {connectorId}");
            }

            return (station.ChargingPointId, connectorNumber, null);
        }

        private async Task<(bool Success, string Message)> CallOcppStartTransactionAsync(
            string chargePointId, int connectorId, string tokenId)
        {
            return await CallOcppApiAsync(
                $"API/StartTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}/{Uri.EscapeDataString(tokenId)}",
                response =>
                {
                    var status = response.GetProperty("status").GetString();
                    return status switch
                    {
                        "Accepted" => (true, "Transaction accepted by charge point"),
                        "Rejected" => (false, "Transaction rejected by charge point"),
                        "Timeout" => (false, "Charge point did not respond in time"),
                        _ => (false, $"Unknown status: {status}")
                    };
                });
        }

        private async Task<(bool Success, string Message)> CallOcppStopTransactionAsync(
            string chargePointId, int connectorId)
        {
            return await CallOcppApiAsync(
                $"API/StopTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}",
                response =>
                {
                    var status = response.GetProperty("status").GetString();
                    return status switch
                    {
                        "Accepted" => (true, "Transaction stopped"),
                        "Rejected" => (false, "Stop rejected by charge point"),
                        "Timeout" => (false, "Charge point did not respond in time"),
                        _ => (false, $"Unknown status: {status}")
                    };
                });
        }

        private async Task<(bool Success, string Message)> CallOcppUnlockConnectorAsync(
            string chargePointId, int connectorId)
        {
            return await CallOcppApiAsync(
                $"API/UnlockConnector/{Uri.EscapeDataString(chargePointId)}/{connectorId}",
                response =>
                {
                    var status = response.GetProperty("status").GetString();
                    return status switch
                    {
                        "Unlocked" => (true, "Connector unlocked"),
                        "UnlockFailed" => (false, "Unlock failed"),
                        "OngoingAuthorizedTransaction" => (false, "Ongoing authorized transaction"),
                        "NotSupported" => (false, "Not supported by charge point"),
                        "Timeout" => (false, "Charge point did not respond in time"),
                        _ => (false, $"Unknown status: {status}")
                    };
                });
        }

        private async Task<(bool Success, string Message)> CallOcppApiAsync(
            string relativeUrl,
            Func<JsonElement, (bool, string)> parseResponse)
        {
            var serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            var apiKey = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
                return (false, "OCPP server URL not configured");

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                var uri = new Uri(new Uri(serverApiUrl), relativeUrl);

                if (!string.IsNullOrWhiteSpace(apiKey))
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

                var response = await httpClient.GetAsync(uri);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        return parseResponse(doc.RootElement);
                    }
                    return (false, "Empty response from OCPP server");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return (false, "Charge point is offline or not found");

                _logger.LogError("OCPP API {Url} returned {StatusCode}", relativeUrl, response.StatusCode);
                return (false, $"OCPP server error: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OCPP API {Url}", relativeUrl);
                return (false, $"Communication error: {ex.Message}");
            }
        }

        private async Task PostCommandResultAsync(string? responseUrl, CommandResultType result, string? message)
        {
            if (string.IsNullOrEmpty(responseUrl))
                return;

            var platformToken = _config.GetValue<string>("OCPI:Token");
            var payload = new OcpiCommandResult
            {
                Result = result,
                Message = string.IsNullOrEmpty(message) ? null : new[]
                {
                    new OcpiDisplayText { Language = "en", Text = message }
                }
            };

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                if (!string.IsNullOrWhiteSpace(platformToken))
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {platformToken}");

                await httpClient.PostAsJsonAsync(responseUrl, payload);
                _logger.LogInformation("Posted CommandResult {Result} to {Url}", result, responseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to POST CommandResult to {Url}", responseUrl);
            }
        }

        // ─────────────────────── COMMAND RESULT CALLBACK (eMSP role) ─────────────────────
        //
        // When WE act as eMSP and issue a command to a partner CPO (see OcpiAdminController
        // .EmspStartSession/.EmspStopSession and OcpiOrphanSessionService), we hand the CPO a
        // response_url of the form "{ourBaseUrl}/2.2.1/commands/{commandType}/{correlationId}".
        // The CPO POSTs the async CommandResult back there once they've executed the command.
        // This does NOT carry the CPO-assigned session_id (the CommandResult contract has no
        // such field) — that arrives separately via their Session PUT, handled in
        // OcpiSessionService.StorePartnerSessionAsync. This callback only tells us whether the
        // command itself was accepted or rejected.

        public async Task HandleCommandResultAsync(string commandType, string correlationId, OcpiCommandResult result)
        {
            _logger.LogInformation(
                "Received CommandResult for {CommandType} correlation={CorrelationId}: {Result}",
                commandType, correlationId, result.Result);

            if (string.Equals(commandType, "START_SESSION", StringComparison.OrdinalIgnoreCase))
            {
                // correlationId is the authorization_reference we generated when sending
                // START_SESSION; the placeholder row still has SessionId == AuthorizationReference
                // until the CPO's Session push resolves it to the real session_id.
                var pending = await _dbContext.OcpiPartnerSessions.FirstOrDefaultAsync(s =>
                    s.AuthorizationReference == correlationId && s.SessionId == s.AuthorizationReference);

                if (pending == null)
                {
                    _logger.LogWarning("CommandResult for unknown START_SESSION correlation {CorrelationId}", correlationId);
                    return;
                }

                if (result.Result != CommandResultType.Accepted)
                {
                    pending.Status = "REJECTED";
                    pending.LastUpdated = DateTime.Now;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogWarning(
                        "Partner rejected START_SESSION (authRef={AuthRef}): {Result}", correlationId, result.Result);
                }
                // ACCEPTED: leave the PENDING row as-is — the CPO's subsequent Session push
                // carries the real session_id and authoritative session state.
            }
            else if (string.Equals(commandType, "STOP_SESSION", StringComparison.OrdinalIgnoreCase))
            {
                // correlationId here is the CPO-assigned SessionId — by the time we issue a
                // stop, the session has already been resolved via the Session push.
                var session = await _dbContext.OcpiPartnerSessions.FirstOrDefaultAsync(s => s.SessionId == correlationId);
                if (session == null)
                {
                    _logger.LogWarning("CommandResult for unknown STOP_SESSION session {SessionId}", correlationId);
                    return;
                }

                if (result.Result != CommandResultType.Accepted)
                    _logger.LogWarning(
                        "Partner rejected STOP_SESSION for session {SessionId}: {Result}", correlationId, result.Result);
                // Final state (EndDateTime / Status=COMPLETED) arrives via the partner's Session push.
            }
            else
            {
                _logger.LogDebug("CommandResult for {CommandType}/{CorrelationId} not tracked", commandType, correlationId);
            }
        }

        // ─────────────────────── REAL-TIME SESSION PUSH (CPO role) ───────────────────────

        /// <summary>
        /// Immediately pushes a newly-created/updated hosted session to the eMSP partner that
        /// initiated it, instead of waiting for the next periodic <c>OcpiSyncBackgroundService</c>
        /// round. Best-effort: on failure the partner will still pick the session up on the
        /// next sync, so errors are logged but never propagated.
        /// </summary>
        private async Task PushHostedSessionToPartnerAsync(OcpiHostedSession hostedSession, OcpiPartnerCredential? partner)
        {
            if (partner == null || string.IsNullOrEmpty(partner.OutboundToken))
                return;

            try
            {
                var sessionsUrl = await DiscoverPartnerEndpointAsync(partner, "sessions");
                if (sessionsUrl == null)
                {
                    _logger.LogWarning(
                        "Could not discover sessions receiver endpoint for partner {PartnerId} — " +
                        "session {SessionId} will reach them on the next periodic sync instead",
                        partner.Id, hostedSession.SessionId);
                    return;
                }

                var ourCountryCode = _config.GetValue<string>("OCPI:CountryCode") ?? "IN";
                var ourPartyId     = _config.GetValue<string>("OCPI:PartyId")     ?? "CPO";
                var isActive       = hostedSession.EndDateTime == null;

                var wireSession = new OcpiSession
                {
                    CountryCode             = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(ourCountryCode),
                    PartyId                 = ourPartyId,
                    Id                      = hostedSession.SessionId,
                    StartDateTime           = hostedSession.StartDateTime,
                    EndDateTime             = isActive ? null : hostedSession.EndDateTime,
                    Kwh                     = hostedSession.TotalEnergy ?? 0m,
                    AuthMethod              = AuthMethodType.Command,
                    AuthorizationReference  = hostedSession.AuthorizationReference,
                    LocationId              = hostedSession.LocationId,
                    EvseId                  = hostedSession.EvseUid,
                    ConnectorId             = hostedSession.ConnectorId,
                    Status                  = isActive ? SessionStatus.Active : SessionStatus.Completed,
                    Currency                = CurrencyCode.IndianRupee,
                    LastUpdated             = hostedSession.LastUpdated,
                    CdrToken                = string.IsNullOrEmpty(hostedSession.TokenUid)
                                                  ? null
                                                  : new OcpiCdrToken { Uid = hostedSession.TokenUid, Type = TokenType.Rfid }
                };

                var url = $"{sessionsUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{hostedSession.SessionId}";

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");

                var resp = await httpClient.PutAsJsonAsync(url, wireSession);
                if (resp.IsSuccessStatusCode)
                    _logger.LogInformation(
                        "Pushed hosted session {SessionId} to partner {PartnerId} in real time",
                        hostedSession.SessionId, partner.Id);
                else
                    _logger.LogWarning(
                        "Real-time push of session {SessionId} to partner {PartnerId} failed: HTTP {Status}",
                        hostedSession.SessionId, partner.Id, resp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error pushing hosted session {SessionId} to partner {PartnerId} in real time",
                    hostedSession.SessionId, partner.Id);
            }
        }

        /// <summary>
        /// Discovers a specific module endpoint URL for a partner by walking their /versions
        /// and version-details URLs. Returns the first matching receiver/EMSP-role URL, or null.
        /// </summary>
        private async Task<string?> DiscoverPartnerEndpointAsync(OcpiPartnerCredential partner, string moduleIdentifier)
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");

                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var versionsResp = await http.GetAsync(partnerURL);
                if (!versionsResp.IsSuccessStatusCode) return null;

                using var versionsDoc = JsonDocument.Parse(await versionsResp.Content.ReadAsStringAsync());

                string? v221Url = null;
                if (versionsDoc.RootElement.TryGetProperty("data", out var vData) && vData.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in vData.EnumerateArray())
                    {
                        var ver = v.TryGetProperty("version", out var vp) ? vp.GetString() : null;
                        var url = v.TryGetProperty("url", out var up) ? up.GetString() : null;
                        if (ver == "2.2.1") { v221Url = url; break; }
                        if (ver == "2.2") v221Url = url;
                    }
                }
                if (v221Url == null) return null;

                var detailsResp = await http.GetAsync(v221Url);
                if (!detailsResp.IsSuccessStatusCode) return null;

                using var detailsDoc = JsonDocument.Parse(await detailsResp.Content.ReadAsStringAsync());
                if (detailsDoc.RootElement.TryGetProperty("data", out var dData) &&
                    dData.TryGetProperty("endpoints", out var eps) && eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var id   = ep.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var role = ep.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;
                        if (!string.Equals(id, moduleIdentifier, StringComparison.OrdinalIgnoreCase)) continue;

                        bool roleMatch = role == null ||
                            string.Equals(role, "RECEIVER", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(role, "EMSP", StringComparison.OrdinalIgnoreCase);
                        if (roleMatch)
                            return ep.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Endpoint discovery failed for partner {Id} module={Module}", partner.Id, moduleIdentifier);
                return null;
            }
        }
    }
}
