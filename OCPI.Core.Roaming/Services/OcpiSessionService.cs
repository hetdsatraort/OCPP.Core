using BitzArt.EnumToMemberValue;
using Microsoft.EntityFrameworkCore;
using OCPI;
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
            // OCPI.Net enums (CountryCode/CurrencyCode/SessionStatus/...) carry the wire value
            // ("IN", "ACTIVE") in an [EnumMember] attribute, not in the C# member name ("India",
            // "Active") — plain .ToString() returns the member name, which both corrupts the
            // stored value and overflows the narrow DB columns. Always go through ToMemberValue().
            var sessionCountryCode = session.CountryCode?.ToMemberValue();

            existing ??= await _dbContext.OcpiPartnerSessions.FirstOrDefaultAsync(s =>
                s.CountryCode == sessionCountryCode
                && s.PartyId == session.PartyId
                && s.SessionId == session.Id);

            if (existing != null)
            {
                // Update existing session. session.EvseId/ConnectorId are the PARTNER's own
                // identifiers (this is an eMSP-role record of a session at a partner CPO's
                // station) — they must not be resolved against our own ChargingStations.
                //
                // Every field falls back to the existing value when the incoming payload omits it.
                // JSON deserialization can't tell "the CPO didn't include this field" from "the CPO
                // sent an explicit null", so treating an absent field as "no change" is the only safe
                // reading — a CPO's periodic/partial push shouldn't be able to erase previously known
                // good data (e.g. a running TotalCost silently reverting to null on the next tick).
                existing.StartDateTime = session.StartDateTime ?? existing.StartDateTime;
                existing.EndDateTime = session.EndDateTime ?? existing.EndDateTime;
                existing.TotalEnergy = session.Kwh ?? existing.TotalEnergy;
                existing.Status = session.Status?.ToMemberValue() ?? existing.Status;
                existing.LocationId = session.LocationId ?? existing.LocationId;
                existing.EvseUid = session.EvseId ?? existing.EvseUid;
                existing.ConnectorId = session.ConnectorId ?? existing.ConnectorId;
                existing.AuthorizationReference = session.AuthorizationReference ?? existing.AuthorizationReference;
                existing.TokenUid = session.CdrToken?.Uid ?? existing.TokenUid;
                existing.Currency = session.Currency?.ToMemberValue() ?? existing.Currency;
                existing.TotalCost = session.TotalCost?.ExclVat ?? existing.TotalCost;
                existing.LastUpdated = session.LastUpdated ?? DateTime.UtcNow;

                var soc = ExtractStateOfCharge(session);
                if (soc.HasValue)
                {
                    existing.CurrentStateOfCharge = soc.Value;
                    existing.StateOfChargeLastUpdate = DateTime.UtcNow;
                }

                _logger.LogInformation($"------Updating Partner Session {session.Id}--------");

                _dbContext.OcpiPartnerSessions.Update(existing);
                _logger.LogInformation("Updated partner session {SessionId}", existing.SessionId);
            }
            else
            {
                // Create new session
                var soc = ExtractStateOfCharge(session);
                var newSession = new OcpiPartnerSession
                {
                    CountryCode = sessionCountryCode,
                    PartyId = session.PartyId,
                    SessionId = session.Id,
                    StartDateTime = session.StartDateTime ?? DateTime.UtcNow,
                    EndDateTime = session.EndDateTime,
                    TotalEnergy = session.Kwh,
                    Status = session.Status?.ToMemberValue(),
                    LocationId = session.LocationId,
                    EvseUid = session.EvseId,
                    ConnectorId = session.ConnectorId,
                    AuthorizationReference = session.AuthorizationReference,
                    TokenUid = session.CdrToken?.Uid,
                    Currency = session.Currency?.ToMemberValue(),
                    TotalCost = session.TotalCost?.ExclVat,
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = session.LastUpdated ?? DateTime.UtcNow,
                    CurrentStateOfCharge = soc,
                    StateOfChargeLastUpdate = soc.HasValue ? DateTime.UtcNow : null
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

            // Update fields that may have changed. PATCH is explicitly a partial update — an
            // omitted field means "unchanged", not "clear it". Falling back to the existing value
            // (matching StorePartnerSessionAsync) is required here: without it, a CPO's routine
            // energy-only PATCH during an active session would null out the previously reported
            // TotalCost/EndDateTime on every tick, which is exactly what was showing "cost: null"
            // in the app despite the CPO having reported a real cost earlier in the session.
            existing.EndDateTime = session.EndDateTime ?? existing.EndDateTime;
            existing.TotalEnergy = session.Kwh ?? existing.TotalEnergy;
            existing.Status = session.Status?.ToMemberValue() ?? existing.Status;
            existing.TotalCost = session.TotalCost?.ExclVat ?? existing.TotalCost;
            existing.LastUpdated = session.LastUpdated ?? DateTime.UtcNow;

            var soc = ExtractStateOfCharge(session);
            if (soc.HasValue)
            {
                existing.CurrentStateOfCharge = soc.Value;
                existing.StateOfChargeLastUpdate = DateTime.UtcNow;
            }

            _dbContext.OcpiPartnerSessions.Update(existing);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated partner session {SessionId}", sessionId);
        }

        public async Task<OcpiPartnerSession> GetPartnerSessionAsync(string sessionId)
        {
            return await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId) ?? new OcpiPartnerSession();
        }

        /// <summary>
        /// Extracts the EV's current state of charge (0–100%) from the session's charging_periods,
        /// per OCPI 2.2.1's STATE_OF_CHARGE CdrDimensionType. Not all CPOs report this — it's
        /// typically DC fast chargers only — so this returns null when absent rather than a default.
        /// Charging periods aren't guaranteed to arrive in chronological order, so this picks the
        /// dimension from whichever period has the latest StartDateTime.
        /// </summary>
        private static decimal? ExtractStateOfCharge(OcpiSession session)
        {
            if (session.ChargingPeriods == null)
                return null;

            return session.ChargingPeriods
                .Where(p => p.Dimensions != null)
                .OrderByDescending(p => p.StartDateTime ?? DateTime.MinValue)
                .SelectMany(p => p.Dimensions!)
                .FirstOrDefault(d => d.Type == CdrDimensionType.StateOfCharge && d.Volume.HasValue)
                ?.Volume;
        }
    }
}
