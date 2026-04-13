using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

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

        public async Task<OcpiPartnerCredential> GetPartnerByTokenAsync(string token)
        {
            return await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Token == token && p.IsActive);
        }

        public async Task<OcpiPartnerCredential> GetPartnerByCountryAndPartyAsync(string countryCode, string partyId)
        {
            return await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.CountryCode == countryCode && p.PartyId == partyId && p.IsActive);
        }

        public async Task<OcpiPartnerCredential> CreateOrUpdatePartnerAsync(
            string token, 
            string url, 
            string countryCode, 
            string partyId, 
            string businessName, 
            string role, 
            string version)
        {
            var existing = await GetPartnerByCountryAndPartyAsync(countryCode, partyId);

            if (existing != null)
            {
                // Update existing partner
                existing.Token = token;
                existing.Url = url;
                existing.BusinessName = businessName;
                existing.Role = role;
                existing.Version = version;
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
    }
}
