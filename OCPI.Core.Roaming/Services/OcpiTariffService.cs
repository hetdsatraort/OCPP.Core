using BitzArt.EnumToMemberValue;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using System.Text.Json;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiTariffService : IOcpiTariffService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiTariffService> _logger;
        private readonly IConfiguration _configuration;

        public OcpiTariffService(
            OCPPCoreContext dbContext,
            ILogger<OcpiTariffService> logger,
            IConfiguration configuration)
        {
            _dbContext = dbContext;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<OcpiTariff>> GetTariffsAsync(int offset = 0, int limit = 100)
        {
            var dbTariffs = await _dbContext.OcpiTariffs
                .Where(t => t.IsActive)
                .OrderBy(t => t.TariffId)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return dbTariffs.Select(s => MapToOcpiTariff(s)).ToList();
        }

        public async Task<int> GetTariffCountAsync()
        {
            return await _dbContext.OcpiTariffs.Where(t => t.IsActive).CountAsync();
        }

        public async Task<OcpiTariff> GetTariffAsync(string countryCode, string partyId, string tariffId)
        {
            var dbTariff = await _dbContext.OcpiTariffs
                .FirstOrDefaultAsync(t => t.CountryCode == countryCode
                    && t.PartyId == partyId
                    && t.TariffId == tariffId
                    && t.IsActive);

            if (dbTariff == null)
                return null!;

            return MapToOcpiTariff(dbTariff);
        }

        public async Task<string> CreateOrUpdateTariffAsync(OcpiTariff tariff)
        {
            // Resolve wire-format strings before any DB access so the same values
            // are used in both the duplicate check and the INSERT/UPDATE.
            var countryCodeStr = tariff.CountryCode?.ToMemberValue();
            var currencyStr = tariff.Currency?.ToMemberValue();
            var typeStr = tariff.Type?.ToMemberValue();

            // Match on the key regardless of IsActive: (CountryCode, PartyId, TariffId) is a unique
            // index, so a soft-deleted row still occupies that key and must be revived here rather
            // than hit with a second INSERT (which would violate the unique constraint).
            var existing = await _dbContext.OcpiTariffs
                .FirstOrDefaultAsync(t => t.CountryCode == countryCodeStr
                    && t.PartyId == tariff.PartyId
                    && t.TariffId == tariff.Id);

            if (existing != null)
            {
                // Update existing (also revives a previously soft-deleted tariff)
                existing.IsActive = true;
                existing.Currency = currencyStr;
                existing.Type = typeStr;
                existing.ElementsJson = JsonSerializer.Serialize(tariff.Elements);
                existing.LastUpdated = tariff.LastUpdated ?? DateTime.UtcNow;

                // Extract simple pricing for quick queries
                if (tariff.Elements?.Any() == true)
                {
                    var firstElement = tariff.Elements.First();
                    foreach (var component in firstElement.PriceComponents ?? [])
                    {
                        switch (component.Type)
                        {
                            case TariffDimensionType.Energy:
                                existing.EnergyPrice = component.Price;
                                break;
                            case TariffDimensionType.Time:
                                existing.TimePrice = component.Price;
                                break;
                            case TariffDimensionType.Flat:
                                existing.SessionFee = component.Price;
                                break;
                        }
                    }
                }

                _dbContext.OcpiTariffs.Update(existing);
                _logger.LogInformation("Updated tariff {TariffId}", tariff.Id);
            }
            else
            {
                // Create new
                var newTariff = new OCPP.Core.Database.OCPIDTO.OcpiTariff
                {
                    CountryCode = countryCodeStr,
                    PartyId = tariff.PartyId,
                    TariffId = tariff.Id,
                    Currency = currencyStr,
                    Type = typeStr,
                    ElementsJson = JsonSerializer.Serialize(tariff.Elements),
                    IsActive = true,
                    StartDateTime = tariff.TariffAltUrl != null ? DateTime.UtcNow : null,
                    LastUpdated = tariff.LastUpdated ?? DateTime.UtcNow
                };

                // Extract simple pricing
                if (tariff.Elements?.Any() == true)
                {
                    var firstElement = tariff.Elements.First();
                    foreach (var component in firstElement.PriceComponents ?? [])
                    {
                        switch (component.Type)
                        {
                            case TariffDimensionType.Energy:
                                newTariff.EnergyPrice = component.Price;
                                break;
                            case TariffDimensionType.Time:
                                newTariff.TimePrice = component.Price;
                                break;
                            case TariffDimensionType.Flat:
                                newTariff.SessionFee = component.Price;
                                break;
                        }
                    }
                }

                await _dbContext.OcpiTariffs.AddAsync(newTariff);
                _logger.LogInformation("Created new tariff {TariffId}", tariff.Id);
            }

            await _dbContext.SaveChangesAsync();
            return tariff.Id!;
        }

        public async Task<bool> DeleteTariffAsync(string countryCode, string partyId, string tariffId)
        {
            var existing = await _dbContext.OcpiTariffs
                .FirstOrDefaultAsync(t => t.CountryCode == countryCode
                    && t.PartyId == partyId
                    && t.TariffId == tariffId
                    && t.IsActive);

            if (existing == null)
                return false;

            existing.IsActive = false;
            existing.LastUpdated = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Deleted tariff {TariffId}", tariffId);
            return true;
        }

        private OcpiTariff MapToOcpiTariff(OCPP.Core.Database.OCPIDTO.OcpiTariff dbTariff)
        {
            var tariff = new OcpiTariff
            {
                CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(dbTariff.CountryCode),
                PartyId = dbTariff.PartyId,
                Id = dbTariff.TariffId,
                Currency = OcpiEnumMemberHelper.ParseMemberValue<CurrencyCode>(dbTariff.Currency),
                Type = !string.IsNullOrEmpty(dbTariff.Type) ? OcpiEnumMemberHelper.ParseMemberValue<TariffType>(dbTariff.Type) : null,
                LastUpdated = dbTariff.LastUpdated
            };

            // Deserialize elements if available
            if (!string.IsNullOrEmpty(dbTariff.ElementsJson))
            {
                try
                {
                    tariff.Elements = JsonSerializer.Deserialize<List<OcpiTariffElement>>(dbTariff.ElementsJson);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize tariff elements for {TariffId}", dbTariff.TariffId);

                    // Fallback: create simple tariff from extracted prices
                    var fallbackComponents = new List<OcpiPriceComponent>();

                    if (dbTariff.EnergyPrice.HasValue)
                        fallbackComponents.Add(new OcpiPriceComponent
                        {
                            Type = TariffDimensionType.Energy,
                            Price = dbTariff.EnergyPrice.Value,
                            StepSize = 1
                        });

                    if (dbTariff.TimePrice.HasValue)
                        fallbackComponents.Add(new OcpiPriceComponent
                        {
                            Type = TariffDimensionType.Time,
                            Price = dbTariff.TimePrice.Value,
                            StepSize = 60
                        });

                    if (dbTariff.SessionFee.HasValue)
                        fallbackComponents.Add(new OcpiPriceComponent
                        {
                            Type = TariffDimensionType.Flat,
                            Price = dbTariff.SessionFee.Value
                        });

                    tariff.Elements = new List<OcpiTariffElement>
                    {
                        new OcpiTariffElement { PriceComponents = fallbackComponents }
                    };
                }
            }

            return tariff;
        }
    }
}
