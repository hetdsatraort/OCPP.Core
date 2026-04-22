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

        private readonly IOcpiLocationService _locationService;
        public OcpiCdrService(
            OCPPCoreContext dbContext, 
            ILogger<OcpiCdrService> logger,
            IConfiguration configuration,
            IOcpiLocationService locationService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
            _locationService = locationService;
        }

        public async Task<string> CreateCdrAsync(OCPI.Contracts.OcpiCdr ocpiCdr, int? partnerCredentialId = null)
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

            var location = ocpiCdr.CdrLocation?.Id != null
                ? await _locationService.GetOurLocationAsync(ocpiCdr.CdrLocation.Id)
                : null;

            var newCdr = new OCPP.Core.Database.OCPIDTO.OcpiCdr
            {
                CountryCode = ocpiCdr.CountryCode?.ToString(),
                PartyId = ocpiCdr.PartyId,
                CdrId = ocpiCdr.Id,
                StartDateTime = ocpiCdr.StartDateTime ?? DateTime.UtcNow,
                EndDateTime = ocpiCdr.EndDateTime ?? DateTime.UtcNow,
                SessionId = ocpiCdr.SessionId,
                AuthorizationReference = ocpiCdr.AuthorizationReference,
                AuthMethod = ocpiCdr.AuthMethod?.ToString(),
                LocationId = ocpiCdr.CdrLocation?.Id,
                EvseUid = location?.Evses?.FirstOrDefault(e => e.Uid == ocpiCdr.CdrLocation?.EvseUid)?.Uid,
                ConnectorId = location?.Evses?.FirstOrDefault(e => e.Uid == ocpiCdr.CdrLocation?.EvseUid)?.Connectors?.FirstOrDefault(c => c.Id == ocpiCdr.CdrLocation?.ConnectorId)?.Id,
                MeterId = ocpiCdr.MeterId,
                Currency = ocpiCdr.Currency?.ToString(),
                TotalEnergy = ocpiCdr.TotalEnergy ?? 0,
                TotalTime = ocpiCdr.TotalTime ?? 0,
                TotalParkingTime = ocpiCdr.TotalParkingTime,
                TotalCostExclVat = ocpiCdr.TotalCost?.ExclVat ?? 0m,
                TotalCostInclVat = ocpiCdr.TotalCost?.InclVat,
                TokenUid = ocpiCdr.CdrToken?.Uid,
                PartnerCredentialId = partnerCredentialId,
                LastUpdated = ocpiCdr.LastUpdated ?? DateTime.UtcNow
            };

            await _dbContext.OcpiCdrs.AddAsync(newCdr);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Created CDR {CdrId}", ocpiCdr.Id);
            return newCdr.CdrId!;
        }

        public async Task<OCPI.Contracts.OcpiCdr?> GetCdrAsync(string cdrId)
        {
            var dbCdr = await _dbContext.OcpiCdrs
                .FirstOrDefaultAsync(c => c.CdrId == cdrId);

            if (dbCdr == null)
                return null;

            return MapToOcpiCdr(dbCdr);
        }

        public async Task<List<OCPI.Contracts.OcpiCdr>> GetCdrsAsync(DateTime? from = null, DateTime? to = null, int offset = 0, int limit = 100)
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

            return dbCdrs.Select(s => MapToOcpiCdr(s)).ToList();
        }

        private OCPI.Contracts.OcpiCdr MapToOcpiCdr(OCPP.Core.Database.OCPIDTO.OcpiCdr dbCdr)
        {
            return new OCPI.Contracts.OcpiCdr
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
                CdrToken = new OcpiCdrToken { Uid = dbCdr.TokenUid },
                LastUpdated = dbCdr.LastUpdated
            };
        }
    }
}
