using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiCdrService : IOcpiCdrService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiCdrService> _logger;
        private readonly IConfiguration _configuration;

        public OcpiCdrService(
            OCPPCoreContext dbContext, 
            ILogger<OcpiCdrService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<string> CreateCdrAsync(OcpiCdr ocpiCdr, int? partnerCredentialId = null)
        {
            var existing = await _dbContext.OcpiCdrs
                .FirstOrDefaultAsync(c => c.CountryCode == ocpiCdr.CountryCode.ToString() 
                    && c.PartyId == ocpiCdr.PartyId 
                    && c.CdrId == ocpiCdr.Id);

            if (existing != null)
            {
                _logger.LogWarning("CDR {CdrId} already exists", ocpiCdr.Id);
                return existing.CdrId;
            }

            var newCdr = new Database.OCPIDTO.OcpiCdr
            {
                CountryCode = ocpiCdr.CountryCode.ToString(),
                PartyId = ocpiCdr.PartyId,
                CdrId = ocpiCdr.Id,
                StartDateTime = ocpiCdr.StartDateTime,
                EndDateTime = ocpiCdr.EndDateTime,
                SessionId = ocpiCdr.SessionId,
                AuthorizationReference = ocpiCdr.AuthorizationReference,
                AuthMethod = ocpiCdr.AuthMethod.ToString(),
                LocationId = ocpiCdr.CdrLocation?.Id,
                EvseUid = ocpiCdr.CdrLocation?.EvseUid,
                ConnectorId = ocpiCdr.CdrLocation?.ConnectorId,
                MeterId = ocpiCdr.MeterId,
                Currency = ocpiCdr.Currency.ToString(),
                TotalEnergy = ocpiCdr.TotalEnergy,
                TotalTime = ocpiCdr.TotalTime,
                TotalParkingTime = ocpiCdr.TotalParkingTime,
                TotalCostExclVat = ocpiCdr.TotalCost?.ExclVat ?? 0,
                TotalCostInclVat = ocpiCdr.TotalCost?.InclVat,
                TokenUid = ocpiCdr.AuthId,
                PartnerCredentialId = partnerCredentialId,
                LastUpdated = ocpiCdr.LastUpdated
            };

            await _dbContext.OcpiCdrs.AddAsync(newCdr);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Created CDR {CdrId}", ocpiCdr.Id);
            return newCdr.CdrId;
        }

        public async Task<OcpiCdr> GetCdrAsync(string cdrId)
        {
            var dbCdr = await _dbContext.OcpiCdrs
                .FirstOrDefaultAsync(c => c.CdrId == cdrId);

            if (dbCdr == null)
                return null;

            return MapToOcpiCdr(dbCdr);
        }

        public async Task<List<OcpiCdr>> GetCdrsAsync(DateTime? from = null, DateTime? to = null, int offset = 0, int limit = 100)
        {
            var query = _dbContext.OcpiCdrs.AsQueryable();

            if (from.HasValue)
                query = query.Where(c => c.StartDateTime >= from.Value);

            if (to.HasValue)
                query = query.Where(c => c.EndDateTime <= to.Value);

            var dbCdrs = await query
                .OrderByDescending(c => c.CreatedOn)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return dbCdrs.Select(MapToOcpiCdr).ToList();
        }

        private OcpiCdr MapToOcpiCdr(Database.OCPIDTO.OcpiCdr dbCdr)
        {
            return new OcpiCdr
            {
                CountryCode = Enum.Parse<CountryCode>(dbCdr.CountryCode, true),
                PartyId = dbCdr.PartyId,
                Id = dbCdr.CdrId,
                StartDateTime = dbCdr.StartDateTime,
                EndDateTime = dbCdr.EndDateTime,
                SessionId = dbCdr.SessionId,
                AuthorizationReference = dbCdr.AuthorizationReference,
                AuthMethod = Enum.Parse<AuthMethodType>(dbCdr.AuthMethod, true),
                CdrLocation = new Contracts.OcpiCdrLocation
                {
                    Id = dbCdr.LocationId,
                    EvseUid = dbCdr.EvseUid,
                    ConnectorId = dbCdr.ConnectorId
                },
                MeterId = dbCdr.MeterId,
                Currency = Enum.Parse<CurrencyCode>(dbCdr.Currency, true),
                TotalEnergy = dbCdr.TotalEnergy,
                TotalTime = dbCdr.TotalTime,
                TotalParkingTime = dbCdr.TotalParkingTime,
                TotalCost = new OcpiPrice 
                { 
                    ExclVat = dbCdr.TotalCostExclVat,
                    InclVat = dbCdr.TotalCostInclVat
                },
                AuthId = dbCdr.TokenUid,
                LastUpdated = dbCdr.LastUpdated
            };
        }
    }
}
