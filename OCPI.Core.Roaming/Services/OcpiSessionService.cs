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
            OcpiPartnerSession? existing = null;

            // The CPO assigns the session_id, not us. When we initiate a session via
            // /admin/emsp/start-session we don't know it yet, so we create a PENDING placeholder
            // row keyed by the authorization_reference we sent them (SessionId == AuthorizationReference
            // until resolved). When the CPO now pushes the real session, match on that reference first
            // and promote the placeholder to the CPO-assigned SessionId.
            if (!string.IsNullOrEmpty(session.AuthorizationReference))
            {
                existing = await _dbContext.OcpiPartnerSessions.FirstOrDefaultAsync(s =>
                    s.PartnerCredentialId == partnerCredentialId
                    && s.AuthorizationReference == session.AuthorizationReference
                    && s.SessionId == s.AuthorizationReference);

                if (existing != null && !string.IsNullOrEmpty(session.Id) && existing.SessionId != session.Id)
                {
                    _logger.LogInformation(
                        "Resolved pending partner session (authRef={AuthRef}) to CPO-assigned SessionId={SessionId}",
                        session.AuthorizationReference, session.Id);
                    existing.SessionId = session.Id;
                }
            }

            // Fall back to matching on the CPO-assigned session id directly (sessions we didn't
            // initiate ourselves, or a second update for an already-resolved session).
            existing ??= await _dbContext.OcpiPartnerSessions.FirstOrDefaultAsync(s =>
                s.CountryCode == session.CountryCode.ToString()
                && s.PartyId == session.PartyId
                && s.SessionId == session.Id);

            if (existing != null)
            {
                // Update existing session. session.EvseId/ConnectorId are the PARTNER's own
                // identifiers (this is an eMSP-role record of a session at a partner CPO's
                // station) — they must not be resolved against our own ChargingStations.
                existing.StartDateTime = session.StartDateTime ?? existing.StartDateTime;
                existing.EndDateTime = session.EndDateTime;
                existing.TotalEnergy = session.Kwh;
                existing.Status = session.Status?.ToString() ?? existing.Status;
                existing.LocationId = session.LocationId ?? existing.LocationId;
                existing.EvseUid = session.EvseId ?? existing.EvseUid;
                existing.ConnectorId = session.ConnectorId ?? existing.ConnectorId;
                existing.AuthorizationReference = session.AuthorizationReference ?? existing.AuthorizationReference;
                existing.TokenUid = session.CdrToken?.Uid ?? existing.TokenUid;
                existing.Currency = session.Currency?.ToString() ?? existing.Currency;
                existing.TotalCost = session.TotalCost?.ExclVat ?? existing.TotalCost;
                existing.LastUpdated = session.LastUpdated ?? DateTime.UtcNow;

                _logger.LogInformation($"------Updating Partner Session {session.Id}--------");

                _dbContext.OcpiPartnerSessions.Update(existing);
                _logger.LogInformation("Updated partner session {SessionId}", existing.SessionId);
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
                    EvseUid = session.EvseId,
                    ConnectorId = session.ConnectorId,
                    AuthorizationReference = session.AuthorizationReference,
                    TokenUid = session.CdrToken?.Uid,
                    Currency = session.Currency?.ToString(),
                    TotalCost = session.TotalCost?.ExclVat,
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = session.LastUpdated ?? DateTime.UtcNow
                };

                _logger.LogInformation($"--------Adding Partner Session {session.Id}--------");

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
