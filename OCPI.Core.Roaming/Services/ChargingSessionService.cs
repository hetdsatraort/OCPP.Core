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
            // CPO role: sessions at OUR stations. Active = no StopTime yet.
            var activeSessions = await _dbContext.OcpiHostedSessions
                .Where(s => s.Status == "ACTIVE")
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
            // CPO role: look up our hosted session table only
            var cs = await _dbContext.OcpiHostedSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

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
            int successCount = 0;

            foreach (var partner in partners)
            {
                try
                {
                    var countryCode = _config.GetValue<string>("OCPI:CountryCode") ?? "IN";
                    var partyId = _config.GetValue<string>("OCPI:PartyId") ?? "CPO";
                    var partnerBaseURL = partner.Url.TrimEnd('/').EndsWith("versions") ? partner.Url.TrimEnd('/').Replace("/versions", "") : $"{partner.Url.TrimEnd('/')}";

                    var url = $"{partnerBaseURL.TrimEnd('/')}/2.2.1/sessions/{countryCode}/{partyId}/{sessionId}";

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

        private async Task<OcpiSession?> MapToOcpiSessionAsync(OCPP.Core.Database.OCPIDTO.OcpiHostedSession ops)
        {
            try
            {
                var station = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(s => s.RecId == ops.EvseUid);

                var gun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.RecId == ops.ConnectorId);

                var countryCode = _config.GetValue<string>("OCPI:CountryCode") ?? "IN";
                var partyId = _config.GetValue<string>("OCPI:PartyId") ?? "CPO";

                var isActive = ops.EndDateTime == DateTime.MinValue || ops.EndDateTime == null;

                return new OcpiSession
                {
                    CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(countryCode),
                    PartyId = partyId,
                    Id = ops.SessionId,
                    StartDateTime = ops.StartDateTime,
                    EndDateTime = isActive ? null : ops.EndDateTime,
                    Kwh = Convert.ToDecimal(ops.TotalEnergy) > 0 ? Convert.ToDecimal(ops.TotalEnergy) : 0m,
                    AuthMethod = AuthMethodType.Command,
                    LocationId = station?.ChargingHubId,
                    EvseId = station?.RecId,
                    ConnectorId = gun?.RecId,
                    Currency = CurrencyCode.IndianRupee,
                    TotalCost = new OcpiPrice() { ExclVat = ops.TotalCost, InclVat = ops.TotalCost * 1.18m },
                    Status = isActive ? SessionStatus.Active : SessionStatus.Completed,
                    LastUpdated = ops.LastUpdated == DateTime.MinValue ? ops.CreatedOn : ops.LastUpdated
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping ChargingSession {SessionId} to OcpiSession", ops.SessionId);
                return null;
            }
        }
    }
}
