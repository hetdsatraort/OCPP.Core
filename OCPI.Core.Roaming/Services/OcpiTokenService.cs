using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiTokenService : IOcpiTokenService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiTokenService> _logger;

        public OcpiTokenService(OCPPCoreContext dbContext, ILogger<OcpiTokenService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<OcpiAuthorizationInfo> AuthorizeTokenAsync(string tokenUid, OcpiLocationReferences? locationReferences = null)
        {
            var token = await _dbContext.OcpiTokens
                .FirstOrDefaultAsync(t => t.TokenUid == tokenUid && t.Valid);

            if (token == null)
            {
                _logger.LogWarning("Token {TokenUid} not found or invalid", tokenUid);
                return new OcpiAuthorizationInfo
                {
                    Allowed = AllowedType.NotAllowed,
                    Info = new OcpiDisplayText
                    {
                        Language = "en",
                        Text = "Token not authorized"
                    }
                };
            }

            // Token is valid
            _logger.LogInformation("Token {TokenUid} authorized successfully", tokenUid);
            return new OcpiAuthorizationInfo
            {
                Allowed = AllowedType.Allowed,
                LocationReferences = locationReferences,
                Info = new OcpiDisplayText
                {
                    Language = "en",
                    Text = "Authorized"
                }
            };
        }

        public async Task StorePartnerTokenAsync(int partnerCredentialId, OcpiToken token)
        {
            var existing = await _dbContext.OcpiTokens
                .FirstOrDefaultAsync(t => t.CountryCode == token.CountryCode.ToString() 
                    && t.PartyId == token.PartyId 
                    && t.TokenUid == token.Uid);

            if (existing != null)
            {
                // Update existing
                existing.Type = token.Type.ToString();
                existing.VisualNumber = token.VisualNumber;
                existing.Issuer = token.Issuer;
                existing.GroupId = token.GroupId;
                existing.Valid = token.Valid;
                existing.Whitelist = token.Whitelist?.ToString();
                existing.Language = token.Language;
                existing.DefaultProfileType = token.DefaultProfileType?.ToString();
                existing.LastUpdated = token.LastUpdated;
                
                _dbContext.OcpiTokens.Update(existing);
                _logger.LogInformation("Updated partner token {TokenUid}", token.Uid);
            }
            else
            {
                // Create new
                var newToken = new Database.OCPIDTO.OcpiToken
                {
                    CountryCode = token.CountryCode.ToString(),
                    PartyId = token.PartyId,
                    TokenUid = token.Uid,
                    Type = token.Type.ToString(),
                    VisualNumber = token.VisualNumber,
                    Issuer = token.Issuer,
                    GroupId = token.GroupId,
                    Valid = token.Valid,
                    Whitelist = token.Whitelist?.ToString(),
                    Language = token.Language,
                    DefaultProfileType = token.DefaultProfileType?.ToString(),
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = token.LastUpdated
                };
                
                await _dbContext.OcpiTokens.AddAsync(newToken);
                _logger.LogInformation("Created new partner token {TokenUid}", token.Uid);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdatePartnerTokenAsync(string tokenUid, OcpiToken token)
        {
            var existing = await _dbContext.OcpiTokens
                .FirstOrDefaultAsync(t => t.TokenUid == tokenUid);

            if (existing == null)
            {
                _logger.LogWarning("Attempted to update non-existent token {TokenUid}", tokenUid);
                return;
            }

            // Update mutable fields
            existing.Valid = token.Valid;
            existing.Whitelist = token.Whitelist?.ToString();
            existing.LastUpdated = token.LastUpdated;

            _dbContext.OcpiTokens.Update(existing);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Updated partner token {TokenUid}", tokenUid);
        }

        public async Task<Database.OCPIDTO.OcpiToken> GetPartnerTokenAsync(string tokenUid)
        {
            return await _dbContext.OcpiTokens
                .FirstOrDefaultAsync(t => t.TokenUid == tokenUid);
        }
    }
}
