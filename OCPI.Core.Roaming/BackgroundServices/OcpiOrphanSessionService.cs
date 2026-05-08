using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Background service that runs on every check-interval tick and does two things:
    ///
    ///   A) LIVE UPDATE (active sessions with a mapped transaction):
    ///      Reads the latest ConnectorStatus meter value and updates TotalEnergy on
    ///      the OcpiPartnerSession so the admin panel always shows fresh energy data.
    ///
    ///   B) CLEANUP (orphaned sessions):
    ///      1. Session has a TransactionId and the Transaction.StopTime is set
    ///         → mark COMPLETED, fill EndDateTime / TotalEnergy from the transaction.
    ///      2. Session has no TransactionId and has been ACTIVE longer than
    ///         OrphanTimeoutMinutes (default 30) without being updated
    ///         → mark INVALID (charger never confirmed the start).
    /// </summary>
    public class OcpiOrphanSessionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OcpiOrphanSessionService> _logger;
        private readonly TimeSpan _checkInterval;
        private readonly TimeSpan _orphanTimeout;

        public OcpiOrphanSessionService(
            IServiceScopeFactory scopeFactory,
            ILogger<OcpiOrphanSessionService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;

            var intervalSeconds = configuration.GetValue<int>("OCPI:OrphanCheckIntervalSeconds", 30);
            var timeouSeconds  = configuration.GetValue<int>("OCPI:OrphanTimeoutSeconds", 60);

            _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
            _orphanTimeout = TimeSpan.FromSeconds(timeouSeconds);
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
                    await ProcessSessionsAsync(stoppingToken);
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

            var activeSessions = await db.OcpiPartnerSessions
                .Where(s => s.Status == "ACTIVE")
                .ToListAsync(ct);

            if (activeSessions.Count == 0)
            {
                _logger.LogDebug("OcpiOrphanSessionService: no ACTIVE sessions found");
                return;
            }

            _logger.LogInformation(
                "OcpiOrphanSessionService: processing {Count} ACTIVE session(s)", activeSessions.Count);

            // ── Bulk-load all referenced lookup data (avoid N+1) ─────────────────

            var txIds      = activeSessions.Where(s => s.TransactionId.HasValue)
                                           .Select(s => s.TransactionId!.Value)
                                           .Distinct()
                                           .ToList();

            var evseUids   = activeSessions.Select(s => s.EvseUid).Where(x => x != null).Distinct().ToList();
            var connIds    = activeSessions.Select(s => s.ConnectorId).Where(x => x != null).Distinct().ToList();

            var transactions = txIds.Any()
                ? await db.Transactions.Where(t => txIds.Contains(t.TransactionId)).ToListAsync(ct)
                : new List<Transaction>();

            var stations = evseUids.Any()
                ? await db.ChargingStations.Where(s => evseUids.Contains(s.RecId)).ToListAsync(ct)
                : new List<OCPP.Core.Database.EVCDTO.ChargingStation>();

            var guns = connIds.Any()
                ? await db.ChargingGuns.Where(g => connIds.Contains(g.RecId)).ToListAsync(ct)
                : new List<OCPP.Core.Database.EVCDTO.ChargingGuns>();

            // Build a map of (ChargePointId, ConnectorNumber) → ConnectorStatus for bulk lookup
            var connectorKeys = new List<(string cpId, int connNum)>();
            foreach (var session in activeSessions)
            {
                if (!session.TransactionId.HasValue) continue;
                var station = stations.FirstOrDefault(st => st.RecId == session.EvseUid);
                var gun     = guns.FirstOrDefault(g  => g.RecId  == session.ConnectorId);
                if (station != null && gun != null && int.TryParse(gun.ConnectorId, out var cn))
                    connectorKeys.Add((station.ChargingPointId, cn));
            }

            var chargePointIds  = connectorKeys.Select(k => k.cpId).Distinct().ToList();
            var allConnStatuses = chargePointIds.Any()
                ? await db.ConnectorStatuses
                      .Where(cs => chargePointIds.Contains(cs.ChargePointId) && cs.Active == 1)
                      .ToListAsync(ct)
                : new List<ConnectorStatus>();

            // ── Process each session ──────────────────────────────────────────────

            int liveUpdated     = 0;
            int closedByTx      = 0;
            int closedAsInvalid = 0;

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
                        session.Status      = "COMPLETED";
                        session.EndDateTime = tx.StopTime.Value;

                        if (tx.MeterStop.HasValue && tx.MeterStop.Value >= tx.MeterStart)
                            session.TotalEnergy = (decimal)Math.Round(tx.MeterStop.Value - tx.MeterStart, 4);

                        session.LastUpdated = DateTime.UtcNow;
                        closedByTx++;

                        _logger.LogInformation(
                            "OcpiOrphanSessionService: COMPLETED session {SessionId} (TxId={TxId}) " +
                            "stopped at {StopTime}, energy={Energy} kWh",
                            session.SessionId, tx.TransactionId, tx.StopTime.Value, session.TotalEnergy);
                    }
                    else
                    {
                        // ── Transaction still running → sync live energy from ConnectorStatus ──
                        var station = stations.FirstOrDefault(st => st.RecId == session.EvseUid);
                        var gun     = guns.FirstOrDefault(g  => g.RecId  == session.ConnectorId);

                        if (station != null && gun != null &&
                            int.TryParse(gun.ConnectorId, out var connNum))
                        {
                            var connStatus = allConnStatuses
                                .FirstOrDefault(cs => cs.ChargePointId == station.ChargingPointId
                                                   && cs.ConnectorId   == connNum);

                            if (connStatus?.LastMeter.HasValue == true &&
                                connStatus.LastMeter.Value >= tx.MeterStart)
                            {
                                var liveEnergy = (decimal)Math.Round(
                                    connStatus.LastMeter.Value - tx.MeterStart, 4);

                                session.TotalEnergy = liveEnergy;
                                session.LastUpdated = DateTime.UtcNow;
                                liveUpdated++;

                                _logger.LogDebug(
                                    "OcpiOrphanSessionService: live update session {SessionId} — " +
                                    "meter={Meter} start={Start} energy={Energy} kWh",
                                    session.SessionId, connStatus.LastMeter.Value,
                                    tx.MeterStart, liveEnergy);
                            }
                        }
                    }

                    continue;
                }

                // ── No TransactionId → orphan timeout check ───────────────────────
                var age = DateTime.UtcNow - session.LastUpdated;
                if (age >= _orphanTimeout)
                {
                    session.Status      = "INVALID";
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

            _logger.LogInformation(
                "OcpiOrphanSessionService: cycle done — live-updated: {Live}, completed: {Tx}, invalidated: {Inv}",
                liveUpdated, closedByTx, closedAsInvalid);
        }
    }
}
