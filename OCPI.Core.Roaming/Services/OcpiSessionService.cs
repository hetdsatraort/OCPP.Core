using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiSessionService : IOcpiSessionService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiSessionService> _logger;

        public OcpiSessionService(OCPPCoreContext dbContext, ILogger<OcpiSessionService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task StorePartnerSessionAsync(int partnerCredentialId, OcpiSession session)
        {
            var existing = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.CountryCode == session.CountryCode.ToString()
                    && s.PartyId == session.PartyId 
                    && s.SessionId == session.Id);

            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.ChargingHubId == session.LocationId);
            if (existing != null && station != null)
            {
                // Update existing session
                existing.StartDateTime = session.StartDateTime ?? DateTime.MinValue;
                existing.EndDateTime = session.EndDateTime;
                existing.TotalEnergy = session.Kwh;
                existing.Status = session.Status?.ToString();
                existing.LocationId = session.LocationId;
                existing.EvseUid = station.RecId;
                existing.ConnectorId = session.ConnectorId;
                existing.AuthorizationReference = session.AuthorizationReference;
                existing.TokenUid = session.CdrToken?.Uid;
                existing.Currency = session.Currency?.ToString();
                existing.TotalCost = session.TotalCost?.ExclVat;
                existing.LastUpdated = session.LastUpdated ?? DateTime.UtcNow;
                
                _dbContext.OcpiPartnerSessions.Update(existing);
                _logger.LogInformation("Updated partner session {SessionId}", session.Id);
            }
            else
            {
                // Create new session
                var newSession = new OcpiPartnerSession
                {
                    CountryCode = session.CountryCode?.ToString(),
                    PartyId = session.PartyId,
                    SessionId = session.Id,
                    StartDateTime = session.StartDateTime ?? DateTime.UtcNow,
                    EndDateTime = session.EndDateTime,
                    TotalEnergy = session.Kwh,
                    Status = session.Status?.ToString(),
                    LocationId = session.LocationId,
                    EvseUid = station?.RecId,
                    ConnectorId = session.ConnectorId,
                    AuthorizationReference = session.AuthorizationReference,
                    TokenUid = session.CdrToken?.Uid,
                    Currency = session.Currency?.ToString(),
                    TotalCost = session.TotalCost?.ExclVat,
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = session.LastUpdated ?? DateTime.UtcNow
                };
                
                await _dbContext.OcpiPartnerSessions.AddAsync(newSession);
                _logger.LogInformation("Created new partner session {SessionId}", session.Id);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdatePartnerSessionAsync(string sessionId, OcpiSession session)
        {
            var existing = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (existing == null)
            {
                _logger.LogWarning("Attempted to update non-existent session {SessionId}", sessionId);
                return;
            }

            // Update fields that may have changed
            existing.EndDateTime = session.EndDateTime;
            existing.TotalEnergy = session.Kwh;
            existing.Status = session.Status?.ToString();
            existing.TotalCost = session.TotalCost?.ExclVat;
            existing.LastUpdated = session.LastUpdated ?? DateTime.UtcNow;

            _dbContext.OcpiPartnerSessions.Update(existing);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Updated partner session {SessionId}", sessionId);
        }

        public async Task<OcpiPartnerSession> GetPartnerSessionAsync(string sessionId)
        {
            return await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId) ?? new OcpiPartnerSession();
        }
    }
}
