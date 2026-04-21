using BitzArt.Pagination;
using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core;
using OCPI.Core.Roaming.Services;


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
        private readonly IOcpiTariffService _tariffService;
        private readonly ILogger<OcpiTariffsController> _logger;

        public OcpiTariffsController(
            IOcpiTariffService tariffService,
            ILogger<OcpiTariffsController> logger)
        {
            _tariffService = tariffService;
            _logger = logger;
        }
        /// <summary>
        /// Get paginated list of all tariffs
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTariffs([FromQuery] OcpiPageRequest pageRequest)
        {
            // Set maximum Limit value
            SetMaxLimit(pageRequest, 100);

            var offset = pageRequest.Offset ?? 0;
            var limit = pageRequest.Limit ?? 100;

            // Fetch tariffs from database
            var tariffs = await _tariffService.GetTariffsAsync(offset, limit);

            var result = new PageResult<OcpiTariff>(tariffs, tariffs.Count, tariffs.Count);

            return OcpiOk(result);
        }

        /// <summary>
        /// Get a specific tariff
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{tariffId}")]
        public async Task<IActionResult> GetTariff(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tariffId)
        {
            var tariff = await _tariffService.GetTariffAsync(tariffId);
            
            if (tariff == null)
                throw OcpiException.UnknownLocation($"Tariff not found: {tariffId}");

            return OcpiOk(tariff);
        }
    }
}
