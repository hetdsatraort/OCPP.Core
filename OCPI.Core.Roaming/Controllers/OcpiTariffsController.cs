using BitzArt.Pagination;
using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core;


namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Tariffs Sender Controller - Provides tariff information to partners
    /// </summary>
    [OcpiEndpoint(OcpiModule.Tariffs, "Sender", "2.2.1")]
    [Route("2.2.1/tariffs")]
    [OcpiAuthorize]
    public class OcpiTariffsController : OcpiController
    {
        /// <summary>
        /// Get paginated list of all tariffs
        /// </summary>
        [HttpGet]
        public IActionResult GetTariffs([FromQuery] OcpiPageRequest pageRequest)
        {
            // Set maximum Limit value
            SetMaxLimit(pageRequest, 100);

            // TODO: Fetch tariffs from database
            var tariffs = new List<OcpiTariff>
            {
                CreateSampleTariff("TARIFF-001", "Standard AC Charging"),
                CreateSampleTariff("TARIFF-002", "Fast DC Charging")
            };

            var pagedResult = tariffs
                .Skip(pageRequest.Offset ?? 0)
                .Take(pageRequest.Limit ?? 100)
                .ToList();

            var result = new PageResult<OcpiTariff>(pagedResult, tariffs.Count, pagedResult.Count);

            return OcpiOk(result);
        }

        /// <summary>
        /// Get a specific tariff
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{tariffId}")]
        public IActionResult GetTariff(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tariffId)
        {
            // TODO: Fetch from database
            var tariff = CreateSampleTariff(tariffId, $"Tariff {tariffId}");
            
            return OcpiOk(tariff);
        }

        private OcpiTariff CreateSampleTariff(string id, string name)
        {
            return new OcpiTariff
            {
                CountryCode = CountryCode.India,
                PartyId = "CPO",
                Id = id,
                Currency = CurrencyCode.IndianRupee,
                Elements = new List<OcpiTariffElement>
                {
                    new OcpiTariffElement
                    {
                        PriceComponents = new List<OcpiPriceComponent>
                        {
                            // Energy component
                            new OcpiPriceComponent
                            {
                                Type = TariffDimensionType.Energy,
                                Price = 22, // ₹0.30 per kWh
                                StepSize = 1
                            },
                            // Time component
                            new OcpiPriceComponent
                            {
                                Type = TariffDimensionType.Time,
                                Price = 0.10m, // ₹0.10 per minute
                                StepSize = 60
                            }
                        }
                    }
                },
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}
