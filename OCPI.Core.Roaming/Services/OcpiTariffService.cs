using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
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

            return dbTariffs.Select(MapToOcpiTariff).ToList();
        }

        public async Task<OcpiTariff> GetTariffAsync(string tariffId)
        {
            var dbTariff = await _dbContext.OcpiTariffs
                .FirstOrDefaultAsync(t => t.TariffId == tariffId && t.IsActive);

            if (dbTariff == null)
                return null;

            return MapToOcpiTariff(dbTariff);
        }

        public async Task<string> CreateOrUpdateTariffAsync(OcpiTariff tariff)
        {
            var existing = await _dbContext.OcpiTariffs
                .FirstOrDefaultAsync(t => t.CountryCode == tariff.CountryCode.ToString() 
                    && t.PartyId == tariff.PartyId 
                    && t.TariffId == tariff.Id);

            if (existing != null)
            {
                // Update existing
                existing.Currency = tariff.Currency.ToString();
                existing.Type = tariff.Type?.ToString();
                existing.ElementsJson = JsonSerializer.Serialize(tariff.Elements);
                existing.LastUpdated = tariff.LastUpdated;
                
                // Extract simple pricing for quick queries
                if (tariff.Elements?.Any() == true)
                {
                    var firstElement = tariff.Elements.First();
                    foreach (var component in firstElement.PriceComponents ?? new List<OcpiPriceComponent>())
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
                var newTariff = new Database.OCPIDTO.OcpiTariff
                {
                    CountryCode = tariff.CountryCode.ToString(),
                    PartyId = tariff.PartyId,
                    TariffId = tariff.Id,
                    Currency = tariff.Currency.ToString(),
                    Type = tariff.Type?.ToString(),
                    ElementsJson = JsonSerializer.Serialize(tariff.Elements),
                    IsActive = true,
                    StartDateTime = tariff.TariffAltUrl != null ? DateTime.UtcNow : null,
                    LastUpdated = tariff.LastUpdated
                };

                // Extract simple pricing
                if (tariff.Elements?.Any() == true)
                {
                    var firstElement = tariff.Elements.First();
                    foreach (var component in firstElement.PriceComponents ?? new List<OcpiPriceComponent>())
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
            return tariff.Id;
        }

        private OcpiTariff MapToOcpiTariff(Database.OCPIDTO.OcpiTariff dbTariff)
        {
            var tariff = new OcpiTariff
            {
                CountryCode = Enum.Parse<CountryCode>(dbTariff.CountryCode, true),
                PartyId = dbTariff.PartyId,
                Id = dbTariff.TariffId,
                Currency = Enum.Parse<CurrencyCode>(dbTariff.Currency, true),
                Type = !string.IsNullOrEmpty(dbTariff.Type) ? Enum.Parse<TariffType>(dbTariff.Type) : null,
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
                    tariff.Elements = new List<OcpiTariffElement>
                    {
                        new OcpiTariffElement
                        {
                            PriceComponents = new List<OcpiPriceComponent>()
                        }
                    };

                    if (dbTariff.EnergyPrice.HasValue)
                    {
                        tariff.Elements[0].PriceComponents.Add(new OcpiPriceComponent
                        {
                            Type = TariffDimensionType.Energy,
                            Price = dbTariff.EnergyPrice.Value,
                            StepSize = 1
                        });
                    }

                    if (dbTariff.TimePrice.HasValue)
                    {
                        tariff.Elements[0].PriceComponents.Add(new OcpiPriceComponent
                        {
                            Type = TariffDimensionType.Time,
                            Price = dbTariff.TimePrice.Value,
                            StepSize = 60
                        });
                    }

                    if (dbTariff.SessionFee.HasValue)
                    {
                        tariff.Elements[0].PriceComponents.Add(new OcpiPriceComponent
                        {
                            Type = TariffDimensionType.Flat,
                            Price = dbTariff.SessionFee.Value
                        });
                    }
                }
            }

            return tariff;
        }
    }
}
