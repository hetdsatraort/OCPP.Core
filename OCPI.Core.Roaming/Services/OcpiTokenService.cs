using BitzArt.EnumToMemberValue;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;

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
            // Resolve wire-format strings before any DB access so the same values are
            // used in both the duplicate check and the INSERT/UPDATE.
            var countryCodeStr = token.CountryCode?.ToMemberValue();
            var typeStr = token.Type?.ToMemberValue();
            var whitelistStr = token.Whitelist?.ToMemberValue();
            var defaultProfileStr = token.DefaultProfileType?.ToMemberValue();

            var existing = await _dbContext.OcpiTokens
                .FirstOrDefaultAsync(t => t.CountryCode == countryCodeStr
                    && t.PartyId == token.PartyId
                    && t.TokenUid == token.Uid);

            if (existing != null)
            {
                existing.Type = Trunc(typeStr, 50);
                existing.VisualNumber = Trunc(token.VisualNumber, 64);
                existing.Issuer = Trunc(token.Issuer, 64);
                existing.GroupId = Trunc(token.GroupId, 36);
                existing.Valid = token.Valid ?? true;
                existing.Whitelist = Trunc(whitelistStr, 50);
                existing.Language = Trunc(token.LanguageCode, 2);
                existing.DefaultProfileType = Trunc(defaultProfileStr, 50);
                existing.LastUpdated = token.LastUpdated ?? DateTime.MinValue;

                _dbContext.OcpiTokens.Update(existing);
                _logger.LogInformation("Updated partner token {TokenUid}", token.Uid);
            }
            else
            {
                var newToken = new OCPP.Core.Database.OCPIDTO.OcpiToken
                {
                    CountryCode = Trunc(countryCodeStr, 2),
                    PartyId = Trunc(token.PartyId, 3),
                    TokenUid = Trunc(token.Uid, 36),
                    Type = Trunc(typeStr, 50),
                    VisualNumber = Trunc(token.VisualNumber, 64),
                    Issuer = Trunc(token.Issuer, 64),
                    GroupId = Trunc(token.GroupId, 36),
                    Valid = token.Valid ?? true,
                    Whitelist = Trunc(whitelistStr, 50),
                    Language = Trunc(token.LanguageCode, 2),
                    DefaultProfileType = Trunc(defaultProfileStr, 50),
                    PartnerCredentialId = partnerCredentialId,
                    LastUpdated = token.LastUpdated ?? DateTime.MinValue
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

            existing.Valid = token.Valid ?? existing.Valid;
            existing.Whitelist = token.Whitelist?.ToMemberValue();
            existing.LastUpdated = token.LastUpdated ?? DateTime.MinValue;

            _dbContext.OcpiTokens.Update(existing);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated partner token {TokenUid}", tokenUid);
        }

        public async Task<OCPP.Core.Database.OCPIDTO.OcpiToken> GetPartnerTokenAsync(string tokenUid)
        {
            return await _dbContext.OcpiTokens
                .FirstOrDefaultAsync(t => t.TokenUid == tokenUid) ?? new OCPP.Core.Database.OCPIDTO.OcpiToken();
        }

        private static string? Trunc(string? value, int maxLength) =>
            value is null ? null : value.Length <= maxLength ? value : value[..maxLength];
    }
}
