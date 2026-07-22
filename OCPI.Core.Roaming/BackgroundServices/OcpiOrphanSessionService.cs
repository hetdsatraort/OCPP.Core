using BitzArt.EnumToMemberValue;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Background service that runs on every check-interval tick and manages sessions hosted
    /// at OUR charging stations (CPO role, <see cref="OCPP.Core.Database.OCPIDTO.OcpiHostedSession"/>).
    ///
    ///   A) LIVE UPDATE (active sessions with a mapped transaction):
    ///      Reads the latest ConnectorStatus meter value and updates TotalEnergy on
    ///      the OcpiHostedSession so the admin panel always shows fresh energy data.
    ///
    ///   B) CLEANUP (orphaned sessions):
    ///      1. Session has a TransactionId and the Transaction.StopTime is set
    ///         → mark COMPLETED, fill EndDateTime / TotalEnergy from the transaction.
    ///      2. Session has no TransactionId and has been ACTIVE longer than
    ///         OrphanTimeoutSeconds (default 60) without being updated
    ///         → mark INVALID (charger never confirmed the start).
    ///      3. Session has a TransactionId that never gets a StopTime because the charge point
    ///         disconnects mid-charge (reboot, WebSocket drop, power loss, etc.) and never sends
    ///         StopTransaction. Once the OCPP server confirms it's offline via ConnectionStatus
    ///         and the last live meter update is older than DisconnectedSessionTimeoutSeconds
    ///         (default 120), force-close it as COMPLETED using the last known energy reading
    ///         (not INVALID — real energy was very likely delivered). Without this, neither of
    ///         the above two cases ever fires and the session stays ACTIVE forever. The mirrored
    ///         eMSP-side OcpiPartnerSession (same SessionId — only possible when the "partner" is
    ///         actually us, i.e. a self-partner OCPI test setup) is force-closed in the same pass
    ///         rather than relying on the completion push reaching it, so both sides flip
    ///         together even if that push fails.
    /// </summary>
    public class OcpiOrphanSessionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OcpiOrphanSessionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _orphanTimeout;
        private readonly TimeSpan _disconnectedSessionTimeout;

        // Mirrors OcpiSyncBackgroundService._jsonOptions — JsonStringEnumMemberConverterV2 must be
        // the only enum converter registered so it isn't shadowed for OCPI.Net enums like
        // SessionStatus/CountryCode whose wire value comes from [EnumMember], not the member name.
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
            Converters                  = { new JsonStringEnumMemberConverterV2() }
        };

        public OcpiOrphanSessionService(
            IServiceScopeFactory scopeFactory,
            ILogger<OcpiOrphanSessionService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;

            var intervalSeconds = configuration.GetValue<int>("OCPI:OrphanCheckIntervalSeconds", 10);
            var timeouSeconds = configuration.GetValue<int>("OCPI:OrphanTimeoutSeconds", 60);
            var disconnectedSeconds = configuration.GetValue<int>("OCPI:DisconnectedSessionTimeoutSeconds", 120);

            _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
            _orphanTimeout = TimeSpan.FromSeconds(timeouSeconds);
            _disconnectedSessionTimeout = TimeSpan.FromSeconds(disconnectedSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "OcpiOrphanSessionService started. Check interval: {Interval}, No-transaction timeout: {Timeout}",
                _checkInterval, _orphanTimeout);

            // Wait one interval before the first check so we don't run immediately on startup.
            await Task.Delay(_checkInterval, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // CPO role: manage sessions hosted at OUR chargers
                    await ProcessSessionsAsync(stoppingToken);
                    // eMSP role: manage sessions our users have at partner CPO stations
                    await ProcessPartnerSessionsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OcpiOrphanSessionService: unhandled error during processing");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("OcpiOrphanSessionService stopped");
        }

        // ─────────────────────────────────────────────────────────────────────────

        private async Task ProcessSessionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();

            // CPO role: only process sessions at OUR stations (OcpiHostedSession).
            // eMSP-role sessions (OcpiPartnerSession) are updated directly by partner CPOs via PUT/PATCH.
            var activeSessions = await db.OcpiHostedSessions
                .Where(s => s.Status == "ACTIVE")
                .ToListAsync(ct);

            if (activeSessions.Count == 0)
            {
                _logger.LogDebug("OcpiOrphanSessionService: no ACTIVE hosted sessions found");
                return;
            }

            _logger.LogInformation(
                "OcpiOrphanSessionService: processing {Count} ACTIVE session(s)", activeSessions.Count);

            // ── Bulk-load all referenced lookup data (avoid N+1) ─────────────────
            // OcpiHostedSession stores ChargePointId and ConnectorNumber directly, so we
            // no longer need to join through ChargingStations / ChargingGuns for the
            // ConnectorStatus lookup.

            var txIds = activeSessions.Where(s => s.TransactionId.HasValue)
                                      .Select(s => s.TransactionId!.Value)
                                      .Distinct()
                                      .ToList();

            var transactions = txIds.Any()
                ? await db.Transactions.Where(t => txIds.Contains(t.TransactionId)).ToListAsync(ct)
                : new List<Transaction>();

            // Build a map of (ChargePointId, ConnectorNumber) → ConnectorStatus for bulk lookup
            var connectorKeys = activeSessions
                .Where(s => s.TransactionId.HasValue)
                .Select(s => (s.ChargePointId, s.ConnectorNumber))
                .Distinct()
                .ToList();

            var chargePointIds = connectorKeys.Select(k => k.ChargePointId).Distinct().ToList();
            var allConnStatuses = chargePointIds.Any()
                ? await db.ConnectorStatuses
                      .Where(cs => chargePointIds.Contains(cs.ChargePointId) && cs.Active == 1)
                      .ToListAsync(ct)
                : new List<ConnectorStatus>();

            // Gun tariffs — needed to compute a live running TotalCost for ACTIVE sessions rather
            // than leaving it null until CDR generation at completion (see PushCompletedSessionsAndCdrsAsync).
            var connectorIds = activeSessions.Select(s => s.ConnectorId).Where(x => x != null).Distinct().ToList();
            var guns = connectorIds.Count > 0
                ? await db.ChargingGuns.Where(g => connectorIds.Contains(g.RecId)).ToListAsync(ct)
                : new List<OCPP.Core.Database.EVCDTO.ChargingGuns>();

            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

            // Confirmed via the OCPP server's own ConnectionStatus — used below to force-close a
            // session whose transaction never gets a StopTime because the charge point dropped
            // off mid-charge. A stale ConnectorStatuses reading alone isn't a safe enough signal
            // (a still-connected charger can just be slow to report), so this requires the
            // stronger, independent "actually offline" confirmation.
            var onlineChargePoints = chargePointIds.Count > 0
                ? await GetOnlineChargePointsForNotifyAsync(httpFactory, chargePointIds, ct)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── Process each session ──────────────────────────────────────────────

            int liveUpdated = 0;
            int closedByTx = 0;
            int closedAsDisconnected = 0;
            int closedAsInvalid = 0;
            var completedChargePointIds = new List<string>();
            var completedPartnerSessions = new List<OcpiHostedSession>();
            var liveUpdatedPartnerSessions = new List<OcpiHostedSession>();

            foreach (var session in activeSessions)
            {
                if (session.TransactionId.HasValue)
                {
                    var tx = transactions.FirstOrDefault(t => t.TransactionId == session.TransactionId.Value);
                    if (tx == null)
                    {
                        _logger.LogWarning(
                            "OcpiOrphanSessionService: session {SessionId} references missing transaction {TxId} — skipping",
                            session.SessionId, session.TransactionId.Value);
                        continue;
                    }

                    if (tx.StopTime.HasValue)
                    {
                        // ── Transaction stopped → close the OCPI session ──────────
                        session.Status = "COMPLETED";
                        session.EndDateTime = tx.StopTime.Value;

                        if (tx.MeterStop.HasValue && tx.MeterStop.Value >= tx.MeterStart)
                            session.TotalEnergy = (decimal)Math.Round(tx.MeterStop.Value - tx.MeterStart, 4);

                        // Final cost (excl. VAT) — PushCompletedSessionsAndCdrsAsync recomputes this
                        // independently for the CDR, but keeping the session's own TotalCost accurate
                        // too means anything reading OcpiHostedSession directly (admin views, the
                        // periodic bulk sync if it races ahead of CDR generation) sees a real number
                        // instead of null.
                        var gunForCost = guns.FirstOrDefault(g => g.RecId == session.ConnectorId);
                        if (gunForCost != null && double.TryParse(gunForCost.ChargerTariff, out var finalTariff))
                            session.TotalCost = Math.Round((session.TotalEnergy ?? 0m) * (decimal)finalTariff, 2);

                        session.LastUpdated = DateTime.UtcNow;
                        closedByTx++;

                        if (!string.IsNullOrEmpty(session.ChargePointId))
                            completedChargePointIds.Add(session.ChargePointId);

                        if (session.PartnerCredentialId.HasValue)
                            completedPartnerSessions.Add(session);

                        _logger.LogInformation(
                            "OcpiOrphanSessionService: COMPLETED session {SessionId} (TxId={TxId}) " +
                            "stopped at {StopTime}, energy={Energy} kWh",
                            session.SessionId, tx.TransactionId, tx.StopTime.Value, session.TotalEnergy);
                    }
                    else
                    {
                        // ── Transaction still running → sync live energy from ConnectorStatus ──
                        // OcpiHostedSession stores ChargePointId and ConnectorNumber directly.
                        var connStatus = allConnStatuses
                            .FirstOrDefault(cs => cs.ChargePointId == session.ChargePointId
                                               && cs.ConnectorId == session.ConnectorNumber);

                        if (connStatus?.LastMeter.HasValue == true &&
                            connStatus.LastMeter.Value >= tx.MeterStart)
                        {
                            var liveEnergy = (decimal)Math.Round(
                                connStatus.LastMeter.Value - tx.MeterStart, 4);

                            // Only treat this as a genuine live update — and only bump LastUpdated —
                            // when the meter reading actually advanced since the last tick. Once a
                            // charge point disconnects, its ConnectorStatuses row just sits there
                            // forever still satisfying the condition above, so unconditionally
                            // refreshing LastUpdated on every ~10s tick would keep resetting the
                            // staleness clock and the disconnected-session force-close check below
                            // could never accumulate enough age to fire — the session would stay
                            // ACTIVE forever even though nothing new is actually happening.
                            bool energyAdvanced = session.TotalEnergy != liveEnergy;

                            session.TotalEnergy = liveEnergy;

                            // Running cost (excl. VAT) — previously never computed for ACTIVE
                            // sessions, so eMSP partners always saw TotalCost as null until the
                            // session completed and a CDR was generated.
                            var gun = guns.FirstOrDefault(g => g.RecId == session.ConnectorId);
                            if (gun != null && double.TryParse(gun.ChargerTariff, out var tariffRate))
                                session.TotalCost = Math.Round(liveEnergy * (decimal)tariffRate, 2);

                            if (energyAdvanced)
                            {
                                session.LastUpdated = DateTime.UtcNow;
                                liveUpdated++;

                                if (session.PartnerCredentialId.HasValue)
                                    liveUpdatedPartnerSessions.Add(session);

                                _logger.LogDebug(
                                    "OcpiOrphanSessionService: live update session {SessionId} — " +
                                    "meter={Meter} start={Start} energy={Energy} kWh, cost={Cost}",
                                    session.SessionId, connStatus.LastMeter.Value,
                                    tx.MeterStart, liveEnergy, session.TotalCost);
                            }
                        }

                        // ── Charge point confirmed offline mid-session → force-close ──────────
                        // The charger dropped off (reboot, WebSocket drop, power loss) without ever
                        // sending StopTransaction, so tx.StopTime will never arrive on its own and
                        // this session would otherwise stay ACTIVE forever — neither the "closedByTx"
                        // branch above (needs tx.StopTime) nor the no-TransactionId orphan check below
                        // (only applies before a transaction exists) ever catches it. Close it using
                        // the last known-good energy reading rather than discarding it as INVALID —
                        // real energy was very likely delivered before the disconnect.
                        bool chargePointOffline = !onlineChargePoints.Contains(session.ChargePointId);
                        if (chargePointOffline && DateTime.UtcNow - session.LastUpdated >= _disconnectedSessionTimeout)
                        {
                            session.Status = "COMPLETED";
                            session.EndDateTime = session.LastUpdated;

                            var gunForCost = guns.FirstOrDefault(g => g.RecId == session.ConnectorId);
                            if (gunForCost != null && double.TryParse(gunForCost.ChargerTariff, out var offlineTariff))
                                session.TotalCost = Math.Round((session.TotalEnergy ?? 0m) * (decimal)offlineTariff, 2);

                            session.LastUpdated = DateTime.UtcNow;
                            closedAsDisconnected++;

                            tx.StopTime = session.EndDateTime;
                            tx.MeterStop = tx.MeterStart + (double)(session.TotalEnergy ?? 0m);
                            tx.StopReason = "ChargePointDisconnected";

                            if (!string.IsNullOrEmpty(session.ChargePointId))
                                completedChargePointIds.Add(session.ChargePointId);

                            if (session.PartnerCredentialId.HasValue)
                                completedPartnerSessions.Add(session);

                            _logger.LogWarning(
                                "OcpiOrphanSessionService: force-closed session {SessionId} (TxId={TxId}) — " +
                                "charge point {ChargePointId} confirmed offline, no live update since {LastUpdated} " +
                                "(energy={Energy} kWh)",
                                session.SessionId, tx.TransactionId, session.ChargePointId,
                                session.EndDateTime, session.TotalEnergy);

                            continue;
                        }

                        // SoC (0–100%) from the OCPP server's cached MeterValues, when the charger
                        // reports it (typically DC fast chargers). Not gated on connStatus/meter
                        // above — the charger may report SoC in a MeterValues sample even between
                        // energy ticks, and we don't want to skip an SoC-only update.
                        var soc = await GetLiveSoCAsync(httpFactory, session.ChargePointId, session.ConnectorNumber, ct);
                        if (soc.HasValue)
                        {
                            session.CurrentStateOfCharge = (decimal)soc.Value;
                            session.StateOfChargeLastUpdate = DateTime.UtcNow;
                            if (session.PartnerCredentialId.HasValue && !liveUpdatedPartnerSessions.Contains(session))
                                liveUpdatedPartnerSessions.Add(session);
                        }
                    }

                    continue;
                }

                // ── No TransactionId → orphan timeout check ───────────────────────
                var age = DateTime.UtcNow - session.LastUpdated;
                if (age >= _orphanTimeout)
                {
                    session.Status = "INVALID";
                    session.EndDateTime = DateTime.UtcNow;
                    session.LastUpdated = DateTime.UtcNow;
                    closedAsInvalid++;

                    _logger.LogWarning(
                        "OcpiOrphanSessionService: session {SessionId} has no TransactionId and has been " +
                        "ACTIVE for {Age:F1} min (threshold {Threshold} min) — marked INVALID",
                        session.SessionId, age.TotalMinutes, _orphanTimeout.TotalMinutes);
                }
            }

            await db.SaveChangesAsync(ct);

            // Immediately notify EMSP/HUB partners of status change for any EVSEs whose
            // sessions just completed (so partners see "AVAILABLE" without waiting for the
            // next full sync round).
            if (completedChargePointIds.Count > 0)
            {
                await NotifyEmspPartnersOfEvseStatusAsync(
                    db, httpFactory, completedChargePointIds.Distinct().ToList(), ct);
            }

            // Generate the CDR for each completed partner-initiated session and push the final
            // session + CDR to the originating eMSP partner in real time, instead of waiting for
            // them to pull it on the next periodic sync round.
            if (completedPartnerSessions.Count > 0)
            {
                await PushCompletedSessionsAndCdrsAsync(db, httpFactory, completedPartnerSessions, ct);
            }

            // Push energy/cost/SoC updates for still-ACTIVE partner-initiated sessions in real time
            // too — without this, an eMSP partner only sees fresh numbers once per
            // OCPI:SyncIntervalMinutes bulk sync (5 min default) instead of on this loop's ~10s
            // cadence, which is what made TotalCost/SoC look permanently stuck/null while charging.
            if (liveUpdatedPartnerSessions.Count > 0)
            {
                await PushActiveSessionUpdatesToEmspAsync(db, httpFactory, liveUpdatedPartnerSessions, ct);
            }

            _logger.LogInformation(
                "OcpiOrphanSessionService: cycle done — live-updated: {Live}, completed: {Tx}, " +
                "force-closed (disconnected): {Disc}, invalidated: {Inv}",
                liveUpdated, closedByTx, closedAsDisconnected, closedAsInvalid);
        }

        // ── Real-time CDR generation + push to EMSP partners (CPO role) ──────────

        /// <summary>
        /// For each just-completed <see cref="OcpiHostedSession"/> that was initiated by an
        /// eMSP partner, generates the corresponding <see cref="OCPP.Core.Database.OCPIDTO.OcpiCdr"/>
        /// (idempotent on <c>LocalSessionId</c>) and pushes both the final session state and the
        /// CDR to that partner in real time, rather than relying solely on their periodic pull of
        /// our /sessions/sender and /cdrs/sender endpoints.
        /// </summary>
        private async Task PushCompletedSessionsAndCdrsAsync(
            OCPPCoreContext db,
            IHttpClientFactory httpFactory,
            List<OcpiHostedSession> sessions,
            CancellationToken ct)
        {
            var partnerIds = sessions
                .Where(s => s.PartnerCredentialId.HasValue)
                .Select(s => s.PartnerCredentialId!.Value)
                .Distinct()
                .ToList();
            if (partnerIds.Count == 0) return;

            var partners = await db.OcpiPartnerCredentials
                .Where(p => partnerIds.Contains(p.Id) && p.IsActive)
                .ToDictionaryAsync(p => p.Id, ct);

            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var connectorIds = sessions.Select(s => s.ConnectorId).Where(x => x != null).Distinct().ToList();
            var guns = connectorIds.Count > 0
                ? await db.ChargingGuns.Where(g => connectorIds.Contains(g.RecId)).ToListAsync(ct)
                : new List<OCPP.Core.Database.EVCDTO.ChargingGuns>();

            foreach (var session in sessions)
            {
                if (!session.PartnerCredentialId.HasValue) continue;
                if (!partners.TryGetValue(session.PartnerCredentialId.Value, out var partner)) continue;

                // Idempotent: skip if we already generated a CDR for this hosted session.
                // Generation must happen regardless of OutboundToken — the CDR is the billing
                // record of truth and must exist (and be pull-able via /2.2.1/cdrs/sender) even
                // when we can't push it right now. Gating creation on OutboundToken here used to
                // mean a partner with an incomplete credentials handshake (no OutboundToken) would
                // never get a CDR at all for their completed sessions — not even on request.
                var cdr = await db.OcpiCdrs.FirstOrDefaultAsync(
                    c => c.LocalSessionId == session.SessionId, ct);

                if (cdr == null)
                {
                    var gun = guns.FirstOrDefault(g => g.RecId == session.ConnectorId);
                    double.TryParse(gun?.ChargerTariff, out var tariffRate);

                    var kwh = session.TotalEnergy ?? 0m;
                    var costExclVat = Math.Round(kwh * (decimal)tariffRate, 2);
                    var costInclVat = Math.Round(costExclVat * 1.18m, 2);
                    var endDateTime = session.EndDateTime ?? DateTime.UtcNow;

                    cdr = new OCPP.Core.Database.OCPIDTO.OcpiCdr
                    {
                        CountryCode = ourCountryCode,
                        PartyId = ourPartyId,
                        CdrId = Guid.NewGuid().ToString(),
                        StartDateTime = session.StartDateTime,
                        EndDateTime = endDateTime,
                        SessionId = session.SessionId,
                        AuthorizationReference = session.AuthorizationReference,
                        AuthMethod = "COMMAND",
                        LocationId = session.LocationId,
                        EvseUid = session.EvseUid,
                        ConnectorId = session.ConnectorId,
                        Currency = "INR",
                        TotalEnergy = kwh,
                        TotalTime = (decimal)(endDateTime - session.StartDateTime).TotalHours,
                        TotalCostExclVat = costExclVat,
                        TotalCostInclVat = costInclVat,
                        TokenUid = session.TokenUid,
                        PartnerCredentialId = partner.Id,
                        LocalSessionId = session.SessionId,
                        CreatedOn = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    // Persist before pushing: if the service restarts between here and the
                    // network call, the next cycle will find this record and retry the push
                    // without creating a duplicate.
                    db.OcpiCdrs.Add(cdr);
                    await db.SaveChangesAsync(ct);
                }

                if (string.IsNullOrEmpty(partner.OutboundToken))
                {
                    // Previously a silent `continue` — made this loud because a missing
                    // OutboundToken (incomplete/never-finished credentials handshake) permanently
                    // blocks every real-time push to this partner (CDRs, sessions, EVSE status)
                    // with no other trace in the logs.
                    _logger.LogWarning(
                        "OcpiOrphanSessionService: partner {CountryCode}-{PartyId} has no OutboundToken — " +
                        "CDR {CdrId} for session {SessionId} was generated but NOT pushed. " +
                        "The partner must pull it via /2.2.1/cdrs/sender until the credentials handshake is completed.",
                        partner.CountryCode, partner.PartyId, cdr.CdrId, session.SessionId);
                    continue;
                }

                await PushSessionAndCdrToPartnerAsync(partner, session, cdr, httpFactory, ct);
            }
        }

        /// <summary>Pushes the final (COMPLETED) session state and its CDR to one eMSP partner.</summary>
        private async Task PushSessionAndCdrToPartnerAsync(
            OcpiPartnerCredential partner,
            OcpiHostedSession session,
            OCPP.Core.Database.OCPIDTO.OcpiCdr cdr,
            IHttpClientFactory httpFactory,
            CancellationToken ct)
        {
            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var http = httpFactory.CreateClient();
            http.DefaultRequestHeaders.TryAddWithoutValidation(
                "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
            http.Timeout = TimeSpan.FromSeconds(15);

            // ── Push final session state ──────────────────────────────
            var sessionsUrl = await DiscoverPartnerModuleEndpointAsync(partner, "sessions", httpFactory, ct);
            if (sessionsUrl != null)
            {
                var wireSession = new OcpiSession
                {
                    CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(ourCountryCode),
                    PartyId = ourPartyId,
                    Id = session.SessionId,
                    StartDateTime = session.StartDateTime,
                    EndDateTime = session.EndDateTime,
                    Kwh = session.TotalEnergy ?? 0m,
                    AuthMethod = AuthMethodType.Command,
                    AuthorizationReference = session.AuthorizationReference,
                    LocationId = session.LocationId,
                    EvseId = session.EvseUid,
                    ConnectorId = session.ConnectorId,
                    Status = SessionStatus.Completed,
                    Currency = CurrencyCode.IndianRupee,
                    TotalCost = cdr.TotalCostExclVat > 0
                                                 ? new OcpiPrice { ExclVat = cdr.TotalCostExclVat, InclVat = cdr.TotalCostInclVat }
                                                 : null,
                    LastUpdated = session.LastUpdated,
                    CdrToken = string.IsNullOrEmpty(session.TokenUid)
                                                  ? null
                                                  : new OcpiCdrToken { Uid = session.TokenUid, Type = TokenType.Rfid }
                };

                try
                {
                    var url = $"{sessionsUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{session.SessionId}";
                    var resp = await http.PutAsJsonAsync(url, wireSession, ct);
                    if (!resp.IsSuccessStatusCode)
                        _logger.LogWarning(
                            "Real-time completed-session push for {SessionId} to partner {PartnerId} failed: HTTP {Status}",
                            session.SessionId, partner.Id, resp.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error pushing completed session {SessionId} to partner {PartnerId}",
                        session.SessionId, partner.Id);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Could not discover sessions receiver endpoint for partner {PartnerId} — " +
                    "completed session {SessionId} will reach them on the next periodic sync instead",
                    partner.Id, session.SessionId);
            }

            // ── Push CDR ───────────────────────────────────────────────
            var cdrsUrl = await DiscoverPartnerModuleEndpointAsync(partner, "cdrs", httpFactory, ct);
            if (cdrsUrl != null)
            {
                var wireCdr = new OCPI.Contracts.OcpiCdr
                {
                    CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(ourCountryCode),
                    PartyId = ourPartyId,
                    Id = cdr.CdrId,
                    StartDateTime = cdr.StartDateTime,
                    EndDateTime = cdr.EndDateTime,
                    SessionId = cdr.SessionId,
                    AuthorizationReference = cdr.AuthorizationReference,
                    AuthMethod = AuthMethodType.Command,
                    CdrLocation = new OcpiCdrLocation
                    {
                        Id = cdr.LocationId,
                        EvseUid = cdr.EvseUid,
                        ConnectorId = cdr.ConnectorId
                    },
                    Currency = CurrencyCode.IndianRupee,
                    TotalEnergy = cdr.TotalEnergy,
                    TotalTime = cdr.TotalTime,
                    TotalCost = new OcpiPrice { ExclVat = cdr.TotalCostExclVat, InclVat = cdr.TotalCostInclVat },
                    CdrToken = string.IsNullOrEmpty(cdr.TokenUid)
                                                  ? null
                                                  : new OcpiCdrToken { Uid = cdr.TokenUid, Type = TokenType.Rfid },
                    LastUpdated = cdr.LastUpdated
                };

                try
                {
                    var resp = await http.PostAsJsonAsync(cdrsUrl, wireCdr, ct);
                    if (resp.IsSuccessStatusCode)
                        _logger.LogInformation(
                            "Pushed CDR {CdrId} for session {SessionId} to partner {PartnerId} in real time",
                            cdr.CdrId, session.SessionId, partner.Id);
                    else
                        _logger.LogWarning(
                            "Real-time CDR push for {CdrId} to partner {PartnerId} failed: HTTP {Status}",
                            cdr.CdrId, partner.Id, resp.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pushing CDR {CdrId} to partner {PartnerId}", cdr.CdrId, partner.Id);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Could not discover CDRs receiver endpoint for partner {PartnerId} — " +
                    "CDR {CdrId} will reach them on the next periodic sync instead",
                    partner.Id, cdr.CdrId);
            }
        }

        /// <summary>
        /// Pushes an in-progress (still ACTIVE) hosted session's energy/cost/SoC to its originating
        /// eMSP partner. Grouped by partner so a partner with several active sessions this cycle
        /// gets one endpoint discovery instead of one per session.
        /// </summary>
        private async Task PushActiveSessionUpdatesToEmspAsync(
            OCPPCoreContext db,
            IHttpClientFactory httpFactory,
            List<OcpiHostedSession> sessions,
            CancellationToken ct)
        {
            var partnerIds = sessions
                .Where(s => s.PartnerCredentialId.HasValue)
                .Select(s => s.PartnerCredentialId!.Value)
                .Distinct()
                .ToList();
            if (partnerIds.Count == 0) return;

            var partners = await db.OcpiPartnerCredentials
                .Where(p => partnerIds.Contains(p.Id) && p.IsActive)
                .ToDictionaryAsync(p => p.Id, ct);

            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId = _configuration["OCPI:PartyId"] ?? "CPO";

            foreach (var group in sessions.GroupBy(s => s.PartnerCredentialId!.Value))
            {
                if (ct.IsCancellationRequested) break;
                if (!partners.TryGetValue(group.Key, out var partner)) continue;
                if (string.IsNullOrEmpty(partner.OutboundToken)) continue; // no log here — same partner already warned in PushCompletedSessionsAndCdrsAsync if genuinely unconfigured

                var sessionsUrl = await DiscoverPartnerModuleEndpointAsync(partner, "sessions", httpFactory, ct);
                if (sessionsUrl == null) continue;

                var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(15);

                foreach (var session in group)
                {
                    var chargingPeriods = session.CurrentStateOfCharge.HasValue
                        ? new List<OcpiChargingPeriod>
                          {
                              new OcpiChargingPeriod
                              {
                                  StartDateTime = session.StateOfChargeLastUpdate ?? DateTime.UtcNow,
                                  Dimensions = new List<OcpiCdrDimension>
                                  {
                                      new OcpiCdrDimension
                                      {
                                          Type = CdrDimensionType.StateOfCharge,
                                          Volume = session.CurrentStateOfCharge.Value
                                      }
                                  }
                              }
                          }
                        : null;

                    var wireSession = new OcpiSession
                    {
                        CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(ourCountryCode),
                        PartyId = ourPartyId,
                        Id = session.SessionId,
                        StartDateTime = session.StartDateTime,
                        EndDateTime = null,
                        Kwh = session.TotalEnergy ?? 0m,
                        AuthMethod = AuthMethodType.Command,
                        AuthorizationReference = session.AuthorizationReference,
                        LocationId = session.LocationId,
                        EvseId = session.EvseUid,
                        ConnectorId = session.ConnectorId,
                        Status = SessionStatus.Active,
                        Currency = CurrencyCode.IndianRupee,
                        TotalCost = session.TotalCost.HasValue
                            ? new OcpiPrice { ExclVat = session.TotalCost.Value, InclVat = Math.Round(session.TotalCost.Value * 1.18m, 2) }
                            : null,
                        ChargingPeriods = chargingPeriods,
                        LastUpdated = session.LastUpdated,
                        CdrToken = string.IsNullOrEmpty(session.TokenUid)
                            ? null
                            : new OcpiCdrToken { Uid = session.TokenUid, Type = TokenType.Rfid }
                    };

                    try
                    {
                        var url = $"{sessionsUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{session.SessionId}";
                        var resp = await http.PutAsJsonAsync(url, wireSession, ct);
                        if (!resp.IsSuccessStatusCode)
                            _logger.LogDebug(
                                "Live session push for {SessionId} to partner {PartnerId} → HTTP {Status}",
                                session.SessionId, partner.Id, resp.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex,
                            "Live session push failed for {SessionId} to partner {PartnerId}",
                            session.SessionId, partner.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Walks the partner's /versions and version-details URLs to find the receiver endpoint
        /// URL for the given OCPI module (e.g. "sessions", "cdrs", "commands"). Returns null if
        /// discovery fails or the partner doesn't expose that module.
        /// </summary>
        private async Task<string?> DiscoverPartnerModuleEndpointAsync(
            OcpiPartnerCredential partner,
            string moduleIdentifier,
            IHttpClientFactory httpFactory,
            CancellationToken ct,
            string[]? acceptableRoles = null)
        {
            acceptableRoles ??= new[] { "RECEIVER", "EMSP" };
            try
            {
                var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(10);

                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var vResp = await http.GetAsync(partnerURL, ct);
                if (!vResp.IsSuccessStatusCode) return null;

                using var vDoc = JsonDocument.Parse(await vResp.Content.ReadAsStringAsync(ct));
                string? v221Url = null;
                if (vDoc.RootElement.TryGetProperty("data", out var vData) &&
                    vData.ValueKind == JsonValueKind.Array)
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

                var dResp = await http.GetAsync(v221Url, ct);
                if (!dResp.IsSuccessStatusCode) return null;

                using var dDoc = JsonDocument.Parse(await dResp.Content.ReadAsStringAsync(ct));
                if (dDoc.RootElement.TryGetProperty("data", out var dData) &&
                    dData.TryGetProperty("endpoints", out var eps) &&
                    eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var id = ep.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var role = ep.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;

                        if (!string.Equals(id, moduleIdentifier, StringComparison.OrdinalIgnoreCase))
                            continue;

                        bool roleOk = role == null
                            || acceptableRoles.Any(r => string.Equals(role, r, StringComparison.OrdinalIgnoreCase));

                        if (roleOk)
                            return ep.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OcpiOrphanSessionService: endpoint discovery failed for partner {PartnerId} module={Module}",
                    partner.Id, moduleIdentifier);
                return null;
            }
        }

        /// <summary>
        /// GETs a fresh copy of each stale ACTIVE partner session directly from the partner CPO's
        /// sessions SENDER endpoint and upserts it via <see cref="IOcpiSessionService.StorePartnerSessionAsync"/>,
        /// so TotalEnergy/TotalCost/Status reflect the partner's latest state rather than waiting
        /// on their next proactive push or the 5-minute bulk sync.
        /// </summary>
        private async Task RefreshStaleSessionsFromPartnerAsync(
            List<OcpiPartnerSession> staleSessions,
            Dictionary<int, OcpiPartnerCredential> partners,
            IOcpiSessionService sessionService,
            IHttpClientFactory httpFactory,
            CancellationToken ct)
        {
            foreach (var session in staleSessions)
            {
                if (ct.IsCancellationRequested) break;
                if (!session.PartnerCredentialId.HasValue) continue;
                if (!partners.TryGetValue(session.PartnerCredentialId.Value, out var partner)) continue;
                if (string.IsNullOrEmpty(partner.OutboundToken)) continue;

                try
                {
                    var sessionsUrl = await DiscoverPartnerModuleEndpointAsync(
                        partner, "sessions", httpFactory, ct, acceptableRoles: new[] { "SENDER" });
                    if (sessionsUrl == null) continue;

                    var http = httpFactory.CreateClient();
                    http.DefaultRequestHeaders.TryAddWithoutValidation(
                        "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                    http.Timeout = TimeSpan.FromSeconds(10);

                    var url = $"{sessionsUrl.TrimEnd('/')}/{session.CountryCode}/{session.PartyId}/{session.SessionId}";
                    var resp = await http.GetAsync(url, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogDebug(
                            "OcpiOrphanSessionService: live session refresh for {SessionId} → HTTP {Status}",
                            session.SessionId, resp.StatusCode);
                        continue;
                    }

                    var body = await resp.Content.ReadAsStringAsync(ct);
                    var envelope = JsonSerializer.Deserialize<OcpiApiEnvelope<OcpiSession>>(body, _jsonOptions);
                    if (envelope?.Data == null) continue;

                    await sessionService.StorePartnerSessionAsync(partner.Id, envelope.Data);

                    _logger.LogDebug(
                        "OcpiOrphanSessionService: live-refreshed partner session {SessionId} — {Kwh} kWh",
                        session.SessionId, envelope.Data.Kwh);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "OcpiOrphanSessionService: live refresh failed for partner session {SessionId}",
                        session.SessionId);
                }
            }
        }

        // ── Real-time EVSE status push to EMSP partners ──────────────────────────

        /// <summary>
        /// For each EMSP/HUB partner, PATCHes the OCPI EVSE status for the stations whose
        /// charge-point IDs are in <paramref name="chargePointIds"/> using the live
        /// <c>ConnectorStatuses</c> table values.  Called immediately after hosted sessions
        /// complete so partners see updated availability without waiting for the next full sync.
        /// </summary>
        private async Task NotifyEmspPartnersOfEvseStatusAsync(
            OCPPCoreContext db,
            IHttpClientFactory httpFactory,
            IReadOnlyList<string> chargePointIds,
            CancellationToken ct)
        {
            var partners = await db.OcpiPartnerCredentials
                .Where(p => p.IsActive && (p.Role == "EMSP" || p.Role == "HUB"))
                .ToListAsync(ct);

            if (partners.Count == 0) return;

            var ourCountryCode = _configuration["OCPI:CountryCode"] ?? "IN";
            var ourPartyId = _configuration["OCPI:PartyId"] ?? "CPO";

            var stations = await db.ChargingStations
                .Where(s => chargePointIds.Contains(s.ChargingPointId) && s.Active == 1)
                .ToListAsync(ct);

            if (stations.Count == 0) return;

            var hubIds = stations.Select(s => s.ChargingHubId).Distinct().ToList();
            var hubs = await db.ChargingHubs
                .Where(h => hubIds.Contains(h.RecId) && h.Active == 1)
                .ToDictionaryAsync(h => h.RecId, ct);

            // Real-time OCPP connector statuses
            var connStatuses = await db.ConnectorStatuses
                .Where(cs => chargePointIds.Contains(cs.ChargePointId) && cs.Active == 1)
                .ToListAsync(ct);

            var statusByChargePoint = connStatuses
                .GroupBy(cs => cs.ChargePointId)
                .ToDictionary(g => g.Key, g => g.Select(cs => cs.LastStatus).ToList());

            // Check which charge points are currently online via the OCPP server so we never
            // push a stale ConnectorStatuses value for an offline charger.
            var onlineChargePoints = await GetOnlineChargePointsForNotifyAsync(
                httpFactory, chargePointIds, ct);

            foreach (var partner in partners)
            {
                if (string.IsNullOrWhiteSpace(partner.OutboundToken)) continue;
                if (ct.IsCancellationRequested) break;

                var locationsUrl = await DiscoverLocationsReceiverAsync(partner, httpFactory, ct);
                if (locationsUrl == null) continue;

                var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(10);

                foreach (var station in stations)
                {
                    if (!hubs.TryGetValue(station.ChargingHubId, out var hub)) continue;

                    string ocpiStatus;
                    if (!onlineChargePoints.Contains(station.ChargingPointId))
                    {
                        ocpiStatus = "OUTOFORDER";
                    }
                    else if (statusByChargePoint.TryGetValue(station.ChargingPointId, out var raw))
                    {
                        ocpiStatus = DeriveEvseOcpiStatus(raw);
                    }
                    else
                    {
                        ocpiStatus = "AVAILABLE"; // session just completed — charger should be free
                    }

                    var url = $"{locationsUrl.TrimEnd('/')}/{ourCountryCode}/{ourPartyId}/{hub.RecId}/{station.RecId}";

                    try
                    {
                        var resp = await http.PatchAsJsonAsync(
                            url,
                            new { status = ocpiStatus, last_updated = DateTime.UtcNow },
                            ct);

                        if (!resp.IsSuccessStatusCode)
                            _logger.LogWarning(
                                "OcpiOrphanSessionService: PATCH EVSE {Uid}={Status} to {CC}-{Party} → HTTP {Code}",
                                station.RecId, ocpiStatus, partner.CountryCode, partner.PartyId, resp.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "OcpiOrphanSessionService: failed to PATCH EVSE {Uid} status to {CC}-{Party}",
                            station.RecId, partner.CountryCode, partner.PartyId);
                    }
                }
            }
        }

        /// <summary>
        /// Walks the partner's /versions → 2.2.1 details to find their locations RECEIVER
        /// endpoint URL.  Returns null if discovery fails.
        /// </summary>
        private async Task<string?> DiscoverLocationsReceiverAsync(
            OcpiPartnerCredential partner,
            IHttpClientFactory httpFactory,
            CancellationToken ct)
        {
            try
            {
                var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(10);

                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var vResp = await http.GetAsync(partnerURL, ct);
                if (!vResp.IsSuccessStatusCode) return null;

                using var vDoc = JsonDocument.Parse(await vResp.Content.ReadAsStringAsync(ct));
                string? v221Url = null;
                if (vDoc.RootElement.TryGetProperty("data", out var vData) &&
                    vData.ValueKind == JsonValueKind.Array)
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

                var dResp = await http.GetAsync(v221Url, ct);
                if (!dResp.IsSuccessStatusCode) return null;

                using var dDoc = JsonDocument.Parse(await dResp.Content.ReadAsStringAsync(ct));
                if (dDoc.RootElement.TryGetProperty("data", out var dData) &&
                    dData.TryGetProperty("endpoints", out var eps) &&
                    eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var id = ep.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var role = ep.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;

                        if (!string.Equals(id, "locations", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Accept RECEIVER or EMSP role (some implementations omit role)
                        bool roleOk = role == null
                            || string.Equals(role, "RECEIVER", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(role, "EMSP", StringComparison.OrdinalIgnoreCase);

                        if (roleOk)
                            return ep.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OcpiOrphanSessionService: endpoint discovery failed for partner {CC}-{Party}",
                    partner.CountryCode, partner.PartyId);
                return null;
            }
        }

        /// <summary>
        /// Calls the OCPP server's /ConnectionStatus API for each charge-point ID and returns
        /// the subset that are currently online — mirrors <c>GunStatusSyncService</c>.
        /// Falls back to treating every ID as online when <c>ServerApiUrl</c> is not set.
        /// </summary>
        private async Task<HashSet<string>> GetOnlineChargePointsForNotifyAsync(
            IHttpClientFactory httpFactory,
            IReadOnlyList<string> chargePointIds,
            CancellationToken ct)
        {
            var serverApiUrl = _configuration["ServerApiUrl"];
            if (string.IsNullOrEmpty(serverApiUrl))
                return new HashSet<string>(chargePointIds, StringComparer.OrdinalIgnoreCase);

            var apiKey = _configuration["ApiKey"] ?? string.Empty;
            var baseUrl = serverApiUrl.TrimEnd('/');
            var online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var id in chargePointIds)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var client = httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    if (!string.IsNullOrWhiteSpace(apiKey))
                        client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

                    var resp = await client.GetAsync(
                        $"{baseUrl}/API/ConnectionStatus/{Uri.EscapeDataString(id)}", ct);

                    if (!resp.IsSuccessStatusCode) continue;

                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("isOnline", out var prop) && prop.GetBoolean())
                        online.Add(id);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex,
                        "OcpiOrphanSessionService: online check failed for {Id} — treating as offline", id);
                }
            }

            return online;
        }

        /// <summary>
        /// Reads the OCPP server's cached MeterValues StateOfCharge measurand for one charge
        /// point/connector — the same <c>SoC/GetSoC</c> endpoint OCPP.Core.Management's
        /// ChargingSessionController uses for our own app's session details. Not all chargers
        /// report SoC (typically DC fast chargers only), so a null return is the normal case,
        /// not an error. Returns null (rather than throwing) on any failure so a slow/unreachable
        /// SoC lookup never blocks the rest of the session-processing cycle.
        /// </summary>
        private async Task<double?> GetLiveSoCAsync(
            IHttpClientFactory httpFactory,
            string chargePointId,
            int connectorNumber,
            CancellationToken ct,
            int maxAgeMinutes = 5)
        {
            var serverApiUrl = _configuration["ServerApiUrl"];
            if (string.IsNullOrEmpty(serverApiUrl))
                return null;

            try
            {
                var apiKey = _configuration["ApiKey"] ?? string.Empty;
                var baseUrl = serverApiUrl.TrimEnd('/');

                var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

                var url = $"{baseUrl}/SoC/GetSoC?chargePointId={Uri.EscapeDataString(chargePointId)}" +
                          $"&connectorId={connectorNumber}&maxAgeMinutes={maxAgeMinutes}";

                var resp = await client.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) return null;

                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);

                if (doc.RootElement.TryGetProperty("success", out var successProp) && successProp.GetBoolean() &&
                    doc.RootElement.TryGetProperty("data", out var dataProp) &&
                    dataProp.ValueKind != JsonValueKind.Null &&
                    dataProp.TryGetProperty("soC", out var socProp) &&
                    socProp.ValueKind == JsonValueKind.Number)
                {
                    return socProp.GetDouble();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "OcpiOrphanSessionService: SoC lookup failed for {ChargePointId}/{Connector}",
                    chargePointId, connectorNumber);
                return null;
            }
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
            if (statuses.Any(s => s == "RESERVED")) return "RESERVED";
            if (statuses.Any(s => s == "OFFLINE")) return "OUTOFORDER";
            if (statuses.All(s => s == "AVAILABLE")) return "AVAILABLE";
            return "UNKNOWN";
        }

        // ── eMSP role: partner session limit checking ─────────────────────────────
        //
        // OcpiPartnerSessions are sessions our users do at partner CPO stations.
        // The partner CPO owns the charging hardware; we can only stop the session by
        // issuing an OCPI STOP_SESSION command.  Energy / cost data comes from whichever
        // arrives first: the partner's own PUT/PATCH pushes to our sessions receiver, the
        // live-refresh pull at the top of ProcessPartnerSessionsAsync (RefreshStaleSessionsFromPartnerAsync),
        // or — as a slow fallback — OcpiSyncBackgroundService's 5-minute bulk sync.
        //
        // This method:
        //   A) Marks sessions as COMPLETED when the partner has set EndDateTime.
        //      Bills the user's wallet for the final TotalCost.
        //   B) Checks configured limits (Energy / Cost / Time) and issues a STOP_SESSION
        //      command to the partner CPO when a limit is exceeded, then bills the wallet
        //      for the current TotalCost.  LimitViolationHandled is set to prevent double-billing.
        //   C) Marks sessions without UserId as INVALID when they are stale (eMSP sessions
        //      that were never formally started from our app and have timed out).

        private async Task ProcessPartnerSessionsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();
            var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var invoiceClient = scope.ServiceProvider.GetRequiredService<IPartnerInvoiceClient>();

            var activeSessions = await db.OcpiPartnerSessions
                .Where(s => s.Status == "ACTIVE")
                .ToListAsync(ct);

            if (activeSessions.Count == 0)
            {
                _logger.LogDebug("OcpiOrphanSessionService: no ACTIVE partner sessions to process");
                return;
            }

            _logger.LogInformation(
                "OcpiOrphanSessionService: processing {Count} ACTIVE partner session(s)", activeSessions.Count);

            // ── Bulk-load ancillary data ──────────────────────────────────────────

            var partnerIds = activeSessions
                .Where(s => s.PartnerCredentialId.HasValue)
                .Select(s => s.PartnerCredentialId!.Value)
                .Distinct().ToList();

            var partners = partnerIds.Any()
                ? await db.OcpiPartnerCredentials
                      .Where(p => partnerIds.Contains(p.Id) && p.IsActive)
                      .ToDictionaryAsync(p => p.Id, ct)
                : new Dictionary<int, OcpiPartnerCredential>();

            // ── Live-refresh sessions the partner hasn't pushed an update for recently ──
            // Some CPOs only PUT/PATCH our sessions receiver at start/stop, not incrementally
            // while charging — without this, TotalEnergy/TotalCost would only advance on the
            // 5-minute OcpiSyncBackgroundService bulk pull (OCPI:SyncIntervalMinutes), making the
            // "Energy Consumed" figure in the app appear frozen for minutes at a time. Pulling
            // just the stale ACTIVE sessions here piggybacks on this loop's existing cadence
            // (OCPI:OrphanCheckIntervalSeconds, default 10s) for a much fresher reading.
            var staleCutoff = DateTime.UtcNow.AddSeconds(-Math.Max(15, _checkInterval.TotalSeconds));
            var staleSessions = activeSessions
                .Where(s => s.PartnerCredentialId.HasValue && s.LastUpdated < staleCutoff)
                .ToList();
            if (staleSessions.Count > 0)
            {
                var sessionService = scope.ServiceProvider.GetRequiredService<IOcpiSessionService>();
                await RefreshStaleSessionsFromPartnerAsync(staleSessions, partners, sessionService, httpFactory, ct);
            }

            // Only need wallet/user data for app-initiated sessions with limits
            var limitedUserIds = activeSessions
                .Where(s => s.UserId != null && !s.LimitViolationHandled
                            && (s.EnergyLimit.HasValue || s.CostLimit.HasValue || s.TimeLimit.HasValue))
                .Select(s => s.UserId!)
                .Distinct().ToList();

            // Also include sessions that completed (EndDateTime set) to finalise billing
            var completedUserIds = activeSessions
                .Where(s => s.EndDateTime.HasValue && s.UserId != null && !s.LimitViolationHandled)
                .Select(s => s.UserId!)
                .Distinct().ToList();

            var allBillingUserIds = limitedUserIds.Union(completedUserIds).Distinct().ToList();

            var walletBalancesDict = new Dictionary<string, string>();
            if (allBillingUserIds.Any())
            {
                var lastTxns = await db.WalletTransactionLogs
                    .Where(w => allBillingUserIds.Contains(w.UserId) && w.Active == 1)
                    .GroupBy(w => w.UserId)
                    .Select(g => g.OrderByDescending(w => w.CreatedOn).First())
                    .ToListAsync(ct);

                walletBalancesDict = lastTxns.ToDictionary(w => w.UserId, w => w.CurrentCreditBalance);
            }

            var usersDict = allBillingUserIds.Any()
                ? await db.Users
                      .Where(u => allBillingUserIds.Contains(u.RecId))
                      .ToDictionaryAsync(u => u.RecId, ct)
                : new Dictionary<string, OCPP.Core.Database.EVCDTO.Users>();

            var walletsToAdd = new List<OCPP.Core.Database.EVCDTO.WalletTransactionLog>();
            var usersToUpdate = new List<OCPP.Core.Database.EVCDTO.Users>();

            int completedCount = 0;
            int limitStoppedCount = 0;
            int orphanInvalidated = 0;

            foreach (var session in activeSessions)
            {
                // ── A) Partner has set EndDateTime → mark COMPLETED and finalise billing ──
                if (session.EndDateTime.HasValue)
                {
                    session.Status = "COMPLETED";
                    session.LastUpdated = DateTime.UtcNow;
                    completedCount++;

                    if (!session.LimitViolationHandled
                        && session.UserId != null
                        && session.TotalCost.HasValue
                        && session.TotalCost > 0)
                    {
                        bool billed = await ApplyPartnerSessionBillingAsync(
                            db, invoiceClient, session, walletBalancesDict, usersDict,
                            walletsToAdd, usersToUpdate,
                            violationReason: null, ct);
                        if (billed)
                        {
                            session.LimitViolationHandled = true;
                        }
                    }

                    _logger.LogInformation(
                        "OcpiOrphanSessionService: partner session {SessionId} marked COMPLETED " +
                        "(partner sent EndDateTime={End}, energy={Energy} kWh, cost={Cost} {Ccy})",
                        session.SessionId, session.EndDateTime, session.TotalEnergy,
                        session.TotalCost, session.Currency);
                    continue;
                }

                // ── B) Stale orphan with no app user → invalidate after timeout ────────
                var age = DateTime.UtcNow - session.LastUpdated;
                if (session.UserId == null && age >= _orphanTimeout)
                {
                    session.Status = "INVALID";
                    session.EndDateTime = DateTime.UtcNow;
                    session.LastUpdated = DateTime.UtcNow;
                    orphanInvalidated++;

                    _logger.LogWarning(
                        "OcpiOrphanSessionService: partner session {SessionId} invalidated — " +
                        "no app user and no update for {Age:F1} min (threshold {Threshold:F1} min)",
                        session.SessionId, age.TotalMinutes, _orphanTimeout.TotalMinutes);
                    continue;
                }

                // ── C) Check user-configured limits ───────────────────────────────────
                if (session.LimitViolationHandled) continue;
                if (session.UserId == null) continue; // no limits to enforce
                if (!session.EnergyLimit.HasValue && !session.CostLimit.HasValue
                    && !session.TimeLimit.HasValue && !session.BatteryIncreaseLimit.HasValue)
                    continue;

                var violations = new List<string>();
                var elapsed = DateTime.UtcNow - session.StartDateTime;

                if (session.EnergyLimit.HasValue && session.TotalEnergy.HasValue
                    && (double)session.TotalEnergy.Value >= session.EnergyLimit.Value)
                {
                    violations.Add(
                        $"Energy {session.TotalEnergy:F3} kWh ≥ limit {session.EnergyLimit:F3} kWh");
                }

                if (session.CostLimit.HasValue && session.TotalCost.HasValue
                    && (double)session.TotalCost.Value >= session.CostLimit.Value)
                {
                    violations.Add(
                        $"Cost {session.TotalCost:F2} {session.Currency} ≥ limit {session.CostLimit:F2}");
                }

                if (session.TimeLimit.HasValue
                    && elapsed.TotalMinutes >= session.TimeLimit.Value)
                {
                    violations.Add(
                        $"Time {elapsed.TotalMinutes:F1} min ≥ limit {session.TimeLimit} min");
                }

                if (violations.Count == 0) continue;

                _logger.LogWarning(
                    "OcpiOrphanSessionService: partner session {SessionId} — limit(s) exceeded: {Violations}",
                    session.SessionId, string.Join("; ", violations));

                // Issue STOP_SESSION to the partner CPO (best-effort; session will be reconciled on next sync)
                if (session.PartnerCredentialId.HasValue &&
                    partners.TryGetValue(session.PartnerCredentialId.Value, out var partner) &&
                    !string.IsNullOrEmpty(partner.OutboundToken))
                {
                    bool stopped = await IssueStopSessionToPartnerAsync(
                        session, partner, httpFactory, ct);

                    _logger.LogInformation(
                        "OcpiOrphanSessionService: STOP_SESSION to partner {PartnerId} for session {SessionId}: {Result}",
                        partner.Id, session.SessionId, stopped ? "ACCEPTED" : "not accepted / failed");
                }
                else
                {
                    _logger.LogWarning(
                        "OcpiOrphanSessionService: cannot send STOP for session {SessionId} — " +
                        "partner credential missing or has no outbound token", session.SessionId);
                }

                // Bill the user for current consumption if we already have a cost figure. If the
                // partner hasn't reported TotalCost yet, leave LimitViolationHandled false (do NOT
                // set it here) so the "Partner has set EndDateTime" branch above can still bill the
                // session once the partner reports the final cost on a later sync — otherwise this
                // session would be marked handled with zero billed and never charged at all.
                // Likewise, if billing was attempted but the Management API call failed (transient
                // network issue), leave it unhandled so this cycle's attempt is retried rather than
                // silently dropping the charge.
                bool billingAttemptFailed = false;
                if (session.TotalCost.HasValue && session.TotalCost > 0)
                {
                    bool billed = await ApplyPartnerSessionBillingAsync(
                        db, invoiceClient, session, walletBalancesDict, usersDict,
                        walletsToAdd, usersToUpdate,
                        violationReason: string.Join("; ", violations), ct);
                    billingAttemptFailed = !billed;
                }

                if (!billingAttemptFailed)
                {
                    session.LimitViolationHandled = true;
                }
                session.LastUpdated = DateTime.UtcNow;
                limitStoppedCount++;
            }

            // ── Batch save ────────────────────────────────────────────────────────
            if (walletsToAdd.Any()) db.WalletTransactionLogs.AddRange(walletsToAdd);
            if (usersToUpdate.Any()) db.Users.UpdateRange(usersToUpdate);

            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "OcpiOrphanSessionService: partner session cycle done — " +
                "completed: {Completed}, limit-stopped: {LimitStopped}, invalidated: {Invalidated}",
                completedCount, limitStoppedCount, orphanInvalidated);
        }

        // ── OCPI outbound STOP_SESSION ─────────────────────────────────────────

        /// <summary>
        /// Discovers the partner CPO's commands endpoint and POSTs a STOP_SESSION body.
        /// Returns true if the partner responded ACCEPTED; false on any failure.
        /// </summary>
        private async Task<bool> IssueStopSessionToPartnerAsync(
            OcpiPartnerSession session,
            OcpiPartnerCredential partner,
            IHttpClientFactory httpFactory,
            CancellationToken ct)
        {
            try
            {
                var commandsUrl = await DiscoverPartnerCommandsEndpointAsync(
                    partner, httpFactory, ct);

                if (commandsUrl == null)
                {
                    _logger.LogWarning(
                        "OcpiOrphanSessionService: commands endpoint not found for partner {PartnerId} — " +
                        "cannot stop session {SessionId}", partner.Id, session.SessionId);
                    return false;
                }

                var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(10);

                var ourBaseUrl = _configuration.GetValue<string>("Ocpi:OurBaseUrl") ?? "https://localhost";
                var commandBody = new
                {
                    response_url = $"{ourBaseUrl}/2.2.1/commands/STOP_SESSION/{session.SessionId}",
                    session_id = session.SessionId
                };

                var resp = await http.PostAsJsonAsync(
                    $"{commandsUrl.TrimEnd('/')}/STOP_SESSION", commandBody, ct);

                var body = await resp.Content.ReadAsStringAsync(ct);
                string cmdResult = "UNKNOWN";
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("data", out var data) &&
                        data.TryGetProperty("result", out var r))
                        cmdResult = r.GetString() ?? "UNKNOWN";
                }
                catch { /* ignore JSON parse errors */ }

                return string.Equals(cmdResult, "ACCEPTED", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "OcpiOrphanSessionService: error issuing STOP_SESSION to partner {PartnerId} " +
                    "for session {SessionId}", partner.Id, session.SessionId);
                return false;
            }
        }

        /// <summary>
        /// Walks the partner's /versions and version-details URLs to find the
        /// commands receiver endpoint URL.  Returns null if discovery fails.
        /// </summary>
        private async Task<string?> DiscoverPartnerCommandsEndpointAsync(
            OcpiPartnerCredential partner,
            IHttpClientFactory httpFactory,
            CancellationToken ct)
        {
            try
            {
                var http = httpFactory.CreateClient();
                http.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization", $"Token {Convert.ToBase64String(Encoding.UTF8.GetBytes(partner.OutboundToken))}");
                http.Timeout = TimeSpan.FromSeconds(10);

                // Step 1: GET /versions
                var partnerURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/') : $"{partner.Url.TrimEnd('/')}/versions";
                var vResp = await http.GetAsync(partnerURL, ct);
                if (!vResp.IsSuccessStatusCode) return null;

                using var vDoc = JsonDocument.Parse(await vResp.Content.ReadAsStringAsync(ct));

                string? v221Url = null;
                if (vDoc.RootElement.TryGetProperty("data", out var vData) &&
                    vData.ValueKind == JsonValueKind.Array)
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

                // Step 2: GET version details
                var dResp = await http.GetAsync(v221Url, ct);
                if (!dResp.IsSuccessStatusCode) return null;

                using var dDoc = JsonDocument.Parse(await dResp.Content.ReadAsStringAsync(ct));

                if (dDoc.RootElement.TryGetProperty("data", out var dData) &&
                    dData.TryGetProperty("endpoints", out var eps) &&
                    eps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ep in eps.EnumerateArray())
                    {
                        var id = ep.TryGetProperty("identifier", out var idProp) ? idProp.GetString() : null;
                        var role = ep.TryGetProperty("role", out var roleProp) ? roleProp.GetString() : null;

                        if (!string.Equals(id, "commands", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Accept RECEIVER or CPO role (some implementations omit role)
                        bool roleOk = role == null
                            || string.Equals(role, "RECEIVER", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(role, "CPO", StringComparison.OrdinalIgnoreCase);

                        if (roleOk)
                            return ep.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "OcpiOrphanSessionService: endpoint discovery failed for partner {PartnerId}",
                    partner.Id);
                return null;
            }
        }

        // ── Wallet billing helper ─────────────────────────────────────────────

        /// <summary>
        /// Debits <c>session.TotalCost</c> plus HyCharge's own platform fee (+ 9% CGST + 9% SGST
        /// on the fee) from the user's wallet and records a
        /// <see cref="OCPP.Core.Database.EVCDTO.WalletTransactionLog"/> entry. The fee/GST math
        /// itself is computed by OCPP.Core.Management (which owns PartnerInvoiceService) — this
        /// service doesn't reference Management, so it calls back into it via
        /// <see cref="IPartnerInvoiceClient"/> rather than duplicating that logic locally.
        /// Mutates <paramref name="walletBalancesDict"/> so subsequent sessions in the same cycle
        /// see the updated balance.
        /// Returns false (without billing) if the invoice couldn't be obtained — e.g. the
        /// Management API is unreachable — so the caller can leave the session unhandled and
        /// retry on the next cycle instead of losing the charge.
        /// </summary>
        private async Task<bool> ApplyPartnerSessionBillingAsync(
            OCPPCoreContext db,
            IPartnerInvoiceClient invoiceClient,
            OcpiPartnerSession session,
            Dictionary<string, string> walletBalancesDict,
            Dictionary<string, OCPP.Core.Database.EVCDTO.Users> usersDict,
            List<OCPP.Core.Database.EVCDTO.WalletTransactionLog> walletsToAdd,
            List<OCPP.Core.Database.EVCDTO.Users> usersToUpdate,
            string? violationReason,
            CancellationToken ct)
        {
            if (session.UserId == null || !session.TotalCost.HasValue || session.TotalCost <= 0)
                return false;

            var invoice = await invoiceClient.GetOrCreateInvoiceAsync(session.SessionId, ct);
            if (invoice == null)
            {
                _logger.LogWarning(
                    "OcpiOrphanSessionService: could not obtain platform-fee invoice for partner " +
                    "session {SessionId} from Management API — skipping wallet debit this cycle",
                    session.SessionId);
                return false;
            }

            decimal totalFee = invoice.TotalPayable;

            decimal previousBalance = 0;
            if (walletBalancesDict.TryGetValue(session.UserId, out var balStr) &&
                decimal.TryParse(balStr, out var lb))
                previousBalance = lb;

            decimal newBalance = previousBalance - totalFee;

            if (newBalance < 0)
                _logger.LogWarning(
                    "OcpiOrphanSessionService: billing user {UserId} for partner session {SessionId} — " +
                    "balance went negative. Previous: {Prev:F2}, Fee: {Fee:F2} {Ccy}, New: {New:F2}",
                    session.UserId, session.SessionId, previousBalance, totalFee, session.Currency, newBalance);

            var info1 = violationReason != null
                ? $"OCPI partner session auto-stopped — limit exceeded: {session.SessionId} | Invoice: {invoice.InvoiceNumber}"
                : $"OCPI partner session completed — {session.SessionId} | Invoice: {invoice.InvoiceNumber}";
            var info2 = $"Energy: {session.TotalEnergy:F3} kWh | Partner cost: {invoice.PartnerCost:F2} {session.Currency} | Platform fee+GST: {invoice.GrandTotal:F2}";
            var info3 = violationReason ?? $"Partner: {session.CountryCode}-{session.PartyId} | EVSE: {session.EvseUid}";

            var walletTx = new OCPP.Core.Database.EVCDTO.WalletTransactionLog
            {
                RecId = Guid.NewGuid().ToString(),
                UserId = session.UserId,
                PreviousCreditBalance = previousBalance.ToString("F2"),
                CurrentCreditBalance = newBalance.ToString("F2"),
                TransactionType = "Debit",
                // ChargingSessionId is a FK into EVCDTO.ChargingSession (our own-charger session
                // table) — OCPI partner sessions live in OcpiPartnerSessions instead and have no
                // row there, so setting this to session.SessionId violates
                // FK_WalletTransactionLog_ChargingSession. The OCPI session id is already in info1.
                ChargingSessionId = null,
                AdditionalInfo1 = info1,
                AdditionalInfo2 = info2,
                AdditionalInfo3 = info3,
                Active = 1,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow
            };

            walletsToAdd.Add(walletTx);

            if (usersDict.TryGetValue(session.UserId, out var user))
            {
                user.CreditBalance = newBalance.ToString("F2");
                user.UpdatedOn = DateTime.UtcNow;
                if (!usersToUpdate.Contains(user))
                    usersToUpdate.Add(user);
            }

            // Update in-memory cache for subsequent sessions in the same cycle
            walletBalancesDict[session.UserId] = newBalance.ToString("F2");

            _logger.LogInformation(
                "OcpiOrphanSessionService: debited {Fee:F2} {Ccy} from user {UserId} " +
                "for partner session {SessionId}; new balance: {Balance:F2}",
                totalFee, session.Currency, session.UserId, session.SessionId, newBalance);

            return true;
        }
    }
}
