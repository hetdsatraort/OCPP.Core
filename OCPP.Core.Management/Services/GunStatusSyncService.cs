using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.ChargingSession;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// Scoped service that syncs each charging gun's status from the OCPP server
    /// into the local DB.  Used by both the controller and the GunStatusService
    /// background task so the logic lives in one place.
    /// </summary>
    public class GunStatusSyncService : IGunStatusSyncService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly IConfiguration _config;
        private readonly ILogger<GunStatusSyncService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public GunStatusSyncService(
            OCPPCoreContext dbContext,
            IConfiguration config,
            ILogger<GunStatusSyncService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        // ── Public API ──────────────────────────────────────────────────────────

        public async Task<ChargingGunStatusDto> SyncGunStatusAsync(string chargingGunId)
        {
            var chargingGun = await _dbContext.ChargingGuns
                .FirstOrDefaultAsync(g => g.RecId == chargingGunId && g.Active == 1);

            if (chargingGun == null)
                return null;

            var activeSession = await _dbContext.ChargingSessions
                .FirstOrDefaultAsync(s => s.ChargingGunId == chargingGun.RecId
                                       && s.Active == 1
                                       && s.EndTime == DateTime.MinValue);

            var chargingStation = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(cs => cs.RecId == chargingGun.ChargingStationId);

            // ── Online check ──────────────────────────────────────────────────
            bool isOnline = false;
            if (chargingStation != null && !string.IsNullOrEmpty(chargingStation.ChargingPointId))
                isOnline = await GetChargePointOnlineAsync(chargingStation.ChargingPointId);

            // ── Offline path ──────────────────────────────────────────────────
            if (!isOnline)
            {
                if (chargingGun.ChargerStatus != "Offline")
                {
                    chargingGun.ChargerStatus = "Offline";
                    chargingGun.UpdatedOn = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }

                return new ChargingGunStatusDto
                {
                    ChargingGunId    = chargingGunId,
                    ChargingStationId   = chargingStation?.RecId,
                    ChargingStationName = chargingStation?.ChargingPointId,
                    ConnectorId      = chargingGun.ConnectorId,
                    Status           = "Offline",
                    CurrentSessionId = activeSession?.RecId,
                    LastStatusUpdate = DateTime.UtcNow,
                    IsAvailable      = false,
                    IsOnline         = false,
                    OcppStatus       = chargingGun.ChargerStatus,
                    LastOcppStatusTime = null,
                    LastMeter        = null,
                    LastMeterTime    = null
                };
            }

            // ── Online path — sync connector status ───────────────────────────
            Database.ConnectorStatus connectorStatus = null;
            if (chargingStation != null && int.TryParse(chargingGun.ConnectorId, out int connectorIdInt))
            {
                connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargingStation.ChargingPointId
                                            && cs.ConnectorId   == connectorIdInt
                                            && cs.Active        == 1);

                if (connectorStatus != null && !string.IsNullOrEmpty(connectorStatus.LastStatus))
                {
                    if (chargingGun.ChargerStatus != connectorStatus.LastStatus)
                    {
                        chargingGun.ChargerStatus = connectorStatus.LastStatus;
                        chargingGun.UpdatedOn     = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync();
                        _logger.LogInformation(
                            "GunStatusSyncService: gun {GunId} status → '{Status}'",
                            chargingGunId, connectorStatus.LastStatus);
                    }
                }
            }

            string effectiveStatus = connectorStatus?.LastStatus;
            if (string.IsNullOrEmpty(effectiveStatus))
                effectiveStatus = activeSession != null ? "In Use" : "Available";

            bool isAvailable = connectorStatus != null
                ? connectorStatus.LastStatus == "Available"
                : activeSession == null;

            return new ChargingGunStatusDto
            {
                ChargingGunId    = chargingGunId,
                ChargingStationId   = chargingStation?.RecId,
                ChargingStationName = chargingStation?.ChargingPointId,
                ConnectorId      = chargingGun.ConnectorId,
                Status           = effectiveStatus,
                CurrentSessionId = activeSession?.RecId,
                LastStatusUpdate = connectorStatus?.LastStatusTime ?? activeSession?.UpdatedOn ?? DateTime.UtcNow,
                IsAvailable      = isAvailable,
                IsOnline         = true,
                OcppStatus          = connectorStatus?.LastStatus,
                LastOcppStatusTime  = connectorStatus?.LastStatusTime,
                LastMeter           = connectorStatus?.LastMeter,
                LastMeterTime       = connectorStatus?.LastMeterTime
            };
        }

        public async Task SyncAllGunsAsync(CancellationToken ct = default)
        {
            var gunIds = await _dbContext.ChargingGuns
                .Where(g => g.Active == 1)
                .Select(g => g.RecId)
                .ToListAsync(ct);

            int synced = 0, failed = 0;
            foreach (var gunId in gunIds)
            {
                if (ct.IsCancellationRequested)
                    break;
                try
                {
                    await SyncGunStatusAsync(gunId);
                    synced++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "GunStatusSyncService: error syncing gun {GunId}", gunId);
                }
            }

            _logger.LogInformation(
                "GunStatusSyncService: sync complete — {Synced} succeeded, {Failed} failed",
                synced, failed);
        }

        // ── Private helpers ─────────────────────────────────────────────────────

        private async Task<bool> GetChargePointOnlineAsync(string chargePointId)
        {
            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            string apiKey       = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
                return false;

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                var uri = new Uri(new Uri(serverApiUrl),
                    $"ConnectionStatus/{Uri.EscapeDataString(chargePointId)}");

                using var client = _httpClientFactory.CreateClient("GunStatus");
                client.Timeout = TimeSpan.FromSeconds(5);

                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

                var response = await client.GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                    return false;

                var json = await response.Content.ReadAsStringAsync();
                dynamic result = JsonConvert.DeserializeObject(json);
                return result?.isOnline == true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "GunStatusSyncService: connection status check failed for {ChargePointId}", chargePointId);
                return false;
            }
        }
    }
}
