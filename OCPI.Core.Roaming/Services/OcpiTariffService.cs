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
                .ToListAsync();

            var all = dbTariffs.Select(MapToOcpiTariff)
                .Concat(await BuildOwnGunTariffsAsync())
                .OrderBy(t => t.Id)
                .ToList();

            return all.Skip(offset).Take(limit).ToList();
        }

        public async Task<int> GetTariffCountAsync()
        {
            var dbCount = await _dbContext.OcpiTariffs.Where(t => t.IsActive).CountAsync();
            var gunCount = (await BuildOwnGunTariffsAsync()).Count;
            return dbCount + gunCount;
        }

        public async Task<OcpiTariff> GetTariffAsync(string countryCode, string partyId, string tariffId)
        {
            var dbTariff = await _dbContext.OcpiTariffs
                .FirstOrDefaultAsync(t => t.CountryCode == countryCode
                    && t.PartyId == partyId
                    && t.TariffId == tariffId
                    && t.IsActive);

            if (dbTariff != null)
                return MapToOcpiTariff(dbTariff);

            return await SynthesizeOwnGunTariffAsync(countryCode, partyId, tariffId) ?? null!;
        }

        /// <summary>
        /// Our own CPO server answers Tariffs module requests (bulk list and single-id lookup
        /// alike) the same way any external CPO's server would: by computing the answer itself,
        /// live, from its own local data — no separate sync job feeding it, no persisted duplicate
        /// of ChargingGun.ChargerTariff. A partner-sync background service only ever pulls FROM a
        /// CPO over HTTP, treating every CPO (including our own self-partner test loop) uniformly;
        /// it has no business reaching into ChargingGuns directly, so this computation lives here,
        /// in the code that actually answers the request. See SynthesizeOwnGunTariffAsync for the
        /// single-id form this mirrors.
        /// </summary>
        private async Task<List<OcpiTariff>> BuildOwnGunTariffsAsync()
        {
            var ourCountryCode = _configuration.GetValue<string>("OCPI:CountryCode") ?? "IN";
            var ourPartyId = _configuration.GetValue<string>("OCPI:PartyId") ?? "HYC";

            var guns = await _dbContext.ChargingGuns
                .Where(g => g.Active == 1 && g.ChargerTariff != null && g.ChargerTariff != "")
                .ToListAsync();

            var result = new List<OcpiTariff>();
            foreach (var gun in guns)
            {
                var tariff = BuildGunTariff(gun, ourCountryCode, ourPartyId);
                if (tariff != null)
                    result.Add(tariff);
            }
            return result;
        }

        /// <summary>
        /// Our own ChargingGuns carry a per-kWh <c>ChargerTariff</c> value that was never synced
        /// into the OcpiTariffs table (that table is populated only by partners pushing tariffs to
        /// us) — so a tariff_id we ourselves generated (see
        /// OcpiLocationService.MapToOcpiConnector / GunTariffIdPrefix) has no cached row above.
        /// Synthesize it on the fly instead, so both real eMSP partners pulling our Tariffs module
        /// and our own eMSP-role code (in a self-partner test setup) resolve a real price rather
        /// than silently falling back to "no tariff available".
        /// </summary>
        private async Task<OcpiTariff?> SynthesizeOwnGunTariffAsync(string countryCode, string partyId, string tariffId)
        {
            var ourCountryCode = _configuration.GetValue<string>("OCPI:CountryCode") ?? "IN";
            var ourPartyId = _configuration.GetValue<string>("OCPI:PartyId") ?? "HYC";

            if (!string.Equals(countryCode, ourCountryCode, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(partyId, ourPartyId, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(tariffId) ||
                !tariffId.StartsWith(OcpiLocationService.GunTariffIdPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            // gunRecId here is the gun's RecId with hyphens stripped (see
            // OcpiLocationService.MapToOcpiConnector — done so the id fits OcpiTariff.TariffId's
            // 36-char cap), so match it against RecId the same way.
            var gunRecId = tariffId.Substring(OcpiLocationService.GunTariffIdPrefix.Length);
            var gun = await _dbContext.ChargingGuns
                .FirstOrDefaultAsync(g => g.RecId.Replace("-", "") == gunRecId && g.Active == 1);

            return gun == null ? null : BuildGunTariff(gun, ourCountryCode, ourPartyId);
        }

        private static OcpiTariff? BuildGunTariff(
            OCPP.Core.Database.EVCDTO.ChargingGuns gun, string ourCountryCode, string ourPartyId)
        {
            if (!double.TryParse(gun.ChargerTariff, out var tariffValue) || tariffValue <= 0)
                return null;

            return new OcpiTariff
            {
                CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(ourCountryCode),
                PartyId = ourPartyId,
                Id = $"{OcpiLocationService.GunTariffIdPrefix}{gun.RecId.Replace("-", "")}",
                Currency = CurrencyCode.IndianRupee,
                Elements = new List<OcpiTariffElement>
                {
                    new OcpiTariffElement
                    {
                        PriceComponents = new List<OcpiPriceComponent>
                        {
                            new OcpiPriceComponent { Type = TariffDimensionType.Energy, Price = (decimal)tariffValue, StepSize = 1 }
                        }
                    }
                },
                LastUpdated = gun.UpdatedOn
            };
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

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // This DbContext is shared for a whole sync round (OcpiSyncBackgroundService
                // creates one per round, across every partner's locations/tariffs/sessions/CDRs).
                // A failed save (e.g. a constraint violation on this one tariff) otherwise leaves
                // the bad entity tracked as Added/Modified, so every later SaveChangesAsync() call
                // in the round keeps re-sending it alongside whatever else changed — and fails too,
                // even for entities that are themselves perfectly valid. Clear tracking so the rest
                // of the round can proceed; every Store*/CreateOrUpdate* method here already
                // re-queries and re-adds its own entity per call, so nothing relies on state
                // surviving across calls on this context.
                _dbContext.ChangeTracker.Clear();
                throw;
            }

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
