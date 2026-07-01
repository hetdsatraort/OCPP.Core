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
            SetMaxLimit(pageRequest, 100);
            pageRequest.Offset ??= 0;
            pageRequest.Limit ??= 100;

            var total = await _tariffService.GetTariffCountAsync();
            var tariffs = await _tariffService.GetTariffsAsync(pageRequest.Offset.Value, pageRequest.Limit.Value);

            var result = new PageResult<OcpiTariff, OcpiPageRequest>(tariffs, pageRequest, total);
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

        /// <summary>
        /// Receive a tariff pushed from a partner CPO (Receiver interface)
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{tariffId}")]
        public async Task<IActionResult> PutTariff(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tariffId,
            [FromBody] OcpiTariff tariff)
        {
            // OcpiValidate(tariff);

            // Route values are authoritative — a PUT body may omit them.
            if (string.IsNullOrEmpty(tariff.Id))
                tariff.Id = tariffId;
            if (string.IsNullOrEmpty(tariff.PartyId))
                tariff.PartyId = partyId;
            if (tariff.CountryCode == null)
                tariff.CountryCode = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(countryCode);

            await _tariffService.CreateOrUpdateTariffAsync(tariff);

            _logger.LogInformation("Stored pushed tariff {TariffId} from {CountryCode}/{PartyId}", tariffId, countryCode, partyId);

            return OcpiOk(tariff);
        }

        /// <summary>
        /// Remove a tariff pushed from a partner CPO (Receiver interface)
        /// </summary>
        [HttpDelete("{countryCode}/{partyId}/{tariffId}")]
        public async Task<IActionResult> DeleteTariff(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tariffId)
        {
            var removed = await _tariffService.DeleteTariffAsync(countryCode, partyId, tariffId);
            if (!removed)
                throw OcpiException.UnknownLocation($"Tariff not found: {tariffId}");

            return OcpiOk(new { });
        }
    }
}
