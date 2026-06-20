using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
using System.Text;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiCredentialsService : IOcpiCredentialsService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiCredentialsService> _logger;

        public OcpiCredentialsService(OCPPCoreContext dbContext, ILogger<OcpiCredentialsService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<OcpiPartnerCredential?> GetPartnerByTokenAsync(string token)
        {
            string decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(token.Trim()));
            return await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => ( !string.IsNullOrEmpty(p.Token) ? p.Token : p.Token) == decodedToken && p.IsActive);
        }

        public async Task<OcpiPartnerCredential?> GetPartnerByCountryAndPartyAsync(string countryCode, string partyId, string roleId)
        {
            return await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.CountryCode == countryCode && p.PartyId == partyId && p.Role == roleId && p.IsActive);
        }

        public async Task<OcpiPartnerCredential> CreateOrUpdatePartnerAsync(
            string token, 
            string url, 
            string countryCode, 
            string partyId, 
            string businessName, 
            string role, 
            string version,
            string? outboundToken = null)
        {
            var existing = await GetPartnerByCountryAndPartyAsync(countryCode, partyId, role);

            if (existing != null)
            {
                // Update existing partner
                existing.Token = token;
                existing.Url = url;
                existing.BusinessName = businessName;
                existing.Role = role;
                existing.Version = version;
                if (outboundToken != null)
                    existing.OutboundToken = outboundToken;
                existing.LastUpdated = DateTime.UtcNow;
                
                _dbContext.OcpiPartnerCredentials.Update(existing);
                _logger.LogInformation("Updated OCPI partner credentials for {CountryCode}-{PartyId}", countryCode, partyId);
            }
            else
            {
                // Create new partner
                existing = new OcpiPartnerCredential
                {
                    Token = token,
                    Url = url,
                    CountryCode = countryCode,
                    PartyId = partyId,
                    BusinessName = businessName,
                    Role = role,
                    Version = version,
                    OutboundToken = outboundToken,
                    IsActive = true,
                    CreatedOn = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };
                
                await _dbContext.OcpiPartnerCredentials.AddAsync(existing);
                _logger.LogInformation("Created new OCPI partner credentials for {CountryCode}-{PartyId}", countryCode, partyId);
            }

            await _dbContext.SaveChangesAsync();
            return existing;
        }

        public async Task DeletePartnerAsync(string token)
        {
            var partner = await GetPartnerByTokenAsync(token);
            if (partner != null)
            {
                partner.IsActive = false;
                partner.LastUpdated = DateTime.UtcNow;
                _dbContext.OcpiPartnerCredentials.Update(partner);
                await _dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Deactivated OCPI partner with token {Token}", token);
            }
        }

        // ── A-token (pending registration) ─────────────────────────────────────

        public async Task<OcpiPendingRegistration> IssueATokenAsync(string label, int expiryHours = 72)
        {
            var pending = new OcpiPendingRegistration
            {
                AToken = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                Label = label,
                ExpiresAt = DateTime.UtcNow.AddHours(expiryHours),
                CreatedOn = DateTime.UtcNow,
                IsUsed = false
            };

            await _dbContext.OcpiPendingRegistrations.AddAsync(pending);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Issued A-token for label '{Label}', expires {Expiry}", label, pending.ExpiresAt);
            return pending;
        }

        public async Task<OcpiPendingRegistration?> GetPendingRegistrationByTokenAsync(string aToken)
        {
            var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(aToken.Trim()));
            return await _dbContext.OcpiPendingRegistrations
                .FirstOrDefaultAsync(p => p.AToken == decodedToken && !p.IsUsed && p.ExpiresAt > DateTime.UtcNow);
        }

        public async Task<List<OcpiPendingRegistration>> GetAllPendingRegistrationsAsync()
        {
            return await _dbContext.OcpiPendingRegistrations
                .OrderByDescending(p => p.CreatedOn)
                .ToListAsync();
        }

        public async Task MarkATokenUsedAsync(int pendingId, int partnerCredentialId)
        {
            var pending = await _dbContext.OcpiPendingRegistrations.FindAsync(pendingId);
            if (pending == null) return;

            pending.IsUsed = true;
            pending.UsedOn = DateTime.UtcNow;
            pending.PartnerCredentialId = partnerCredentialId;
            _dbContext.OcpiPendingRegistrations.Update(pending);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("A-token id={Id} marked used, linked to partner credential id={PartnerId}", pendingId, partnerCredentialId);
        }
    }
}
