using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;

namespace OCPI.Core.Roaming.Services
{
    public class ChargingSessionService : IChargingSessionService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ChargingSessionService> _logger;
        private readonly IConfiguration _config;

        public ChargingSessionService(
            OCPPCoreContext dbContext,
            ILogger<ChargingSessionService> logger,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
        }

        public async Task<List<OcpiSession>> GetActiveOcpiSessionsAsync()
        {
            var activeSessions = await _dbContext.ChargingSessions
                .Where(s => s.EndTime == DateTime.MinValue && s.Active == 1)
                .ToListAsync();

            var sessions = new List<OcpiSession>();
            foreach (var cs in activeSessions)
            {
                var session = await MapToOcpiSessionAsync(cs);
                if (session != null)
                    sessions.Add(session);
            }

            _logger.LogInformation("Retrieved {Count} active OCPI sessions", sessions.Count);
            return sessions;
        }

        public async Task<OcpiSession?> GetOcpiSessionAsync(string sessionId)
        {
            var cs = await _dbContext.ChargingSessions
                .FirstOrDefaultAsync(s => s.RecId == sessionId && s.Active == 1);

            if (cs == null)
                return null;

            return await MapToOcpiSessionAsync(cs);
        }

        public async Task<bool> PushSessionUpdateToPartnersAsync(string sessionId)
        {
            var session = await GetOcpiSessionAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("PushSessionUpdate: session {SessionId} not found", sessionId);
                return false;
            }

            var partners = await _dbContext.OcpiPartnerCredentials
                .Where(p => p.IsActive)
                .ToListAsync();

            var platformToken = _config.GetValue<string>("OCPI:Token");
            int successCount  = 0;

            foreach (var partner in partners)
            {
                try
                {
                    var countryCode = _config.GetValue<string>("OCPI:CountryCode") ?? "IN";
                    var partyId     = _config.GetValue<string>("OCPI:PartyId")     ?? "CPO";
                    var url         = $"{partner.Url.TrimEnd('/')}/2.2.1/sessions/{countryCode}/{partyId}/{sessionId}";

                    using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {partner.Token}");

                    var response = await httpClient.PutAsJsonAsync(url, session);
                    if (response.IsSuccessStatusCode)
                        successCount++;
                    else
                        _logger.LogWarning("Failed to push session {SessionId} to partner {PartyId}: {Status}",
                            sessionId, partner.PartyId, response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pushing session {SessionId} to partner {PartyId}",
                        sessionId, partner.PartyId);
                }
            }

            _logger.LogInformation("Pushed session {SessionId} to {Count}/{Total} partners",
                sessionId, successCount, partners.Count);

            return successCount > 0 || partners.Count == 0;
        }

        // ─────────────────────────── MAPPING ─────────────────────────────────────

        private async Task<OcpiSession?> MapToOcpiSessionAsync(OCPP.Core.Database.EVCDTO.ChargingSession cs)
        {
            try
            {
                var station = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(s => s.RecId == cs.ChargingStationID);

                var gun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.RecId == cs.ChargingGunId);

                var countryCode = _config.GetValue<string>("OCPI:CountryCode") ?? "IN";
                var partyId     = _config.GetValue<string>("OCPI:PartyId")     ?? "CPO";

                var isActive = cs.EndTime == DateTime.MinValue;

                return new OcpiSession
                {
                    CountryCode           = Enum.Parse<CountryCode>(countryCode),
                    PartyId               = partyId,
                    Id                    = cs.RecId,
                    StartDateTime         = cs.StartTime,
                    EndDateTime           = isActive ? null : cs.EndTime,
                    Kwh                   = Convert.ToDecimal(cs.EnergyTransmitted) > 0 ? Convert.ToDecimal(cs.EnergyTransmitted) : 0m,
                    AuthMethod            = AuthMethodType.Command,
                    LocationId            = station?.ChargingHubId,
                    EvseId                = station?.RecId,
                    ConnectorId           = gun?.ConnectorId,
                    Currency              = CurrencyCode.IndianRupee,
                    Status                = isActive ? SessionStatus.Active : SessionStatus.Completed,
                    LastUpdated           = cs.UpdatedOn == DateTime.MinValue ? cs.CreatedOn : cs.UpdatedOn
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping ChargingSession {SessionId} to OcpiSession", cs.RecId);
                return null;
            }
        }
    }
}
