using BitzArt.Pagination;
using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Locations Sender Controller - Provides location data to partners (CPO role)
    /// </summary>
    [OcpiEndpoint(OcpiModule.Locations, "Sender", "2.2.1")]
    [Route("2.2.1/locations")]
    [OcpiAuthorize]
    public class OcpiLocations_SenderController : OcpiController
    {
        private readonly IOcpiLocationService _locationService;
        private readonly ILogger<OcpiLocations_SenderController> _logger;

        public OcpiLocations_SenderController(
            IOcpiLocationService locationService,
            ILogger<OcpiLocations_SenderController> logger)
        {
            _locationService = locationService;
            _logger = logger;
        }

        /// <summary>
        /// Get paginated list of all locations
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetLocations([FromQuery] OcpiPageRequest pageRequest)
        {
            try
            {
                SetMaxLimit(pageRequest, 100);
                pageRequest.Offset ??= 0;
                pageRequest.Limit ??= 100;

                var total = await _locationService.GetOurLocationCountAsync();
                var locations = await _locationService.GetOurLocationsAsync(pageRequest.Offset.Value, pageRequest.Limit.Value);

                var result = new PageResult<OCPI.Core.Roaming.Services.OcpiLocation, OcpiPageRequest>(locations, pageRequest, total);
                return OcpiOk(result);
            }
            catch (Exception ex)
            {
                throw OcpiException.ServerError(ex.Message, ex.InnerException);
            }
            
        }

        /// <summary>
        /// Get a specific location
        /// </summary>
        // No {country_code}/{party_id} prefix here: as the CPO Sender interface we only ever serve
        // our own locations, so there's nothing to disambiguate. That prefix belongs on the eMSP
        // Receiver side (see OcpiLocations_ReceiverController), where one eMSP stores locations
        // pushed by many different CPOs and needs it to tell them apart.
        [HttpGet("{locationId}")]
        public async Task<IActionResult> GetLocation([FromRoute] string locationId)
        {
            var location = await _locationService.GetOurLocationAsync(locationId);

            if (location == null)
                throw OcpiException.UnknownLocation($"Location not found: {locationId}");

            return OcpiOk(location);
        }

        /// <summary>
        /// Get a specific EVSE within a location
        /// </summary>
        [HttpGet("{locationId}/{evseUid}")]
        public async Task<IActionResult> GetEvse(
            [FromRoute] string locationId,
            [FromRoute] string evseUid)
        {
            var evse = await _locationService.GetOurEvseAsync(locationId, evseUid);

            if (evse == null)
                throw OcpiException.UnknownLocation($"EVSE not found: {evseUid}");

            return OcpiOk(evse);
        }

        /// <summary>
        /// Get a specific connector within an EVSE
        /// </summary>
        [HttpGet("{locationId}/{evseUid}/{connectorId}")]
        public async Task<IActionResult> GetConnector(
            [FromRoute] string locationId,
            [FromRoute] string evseUid,
            [FromRoute] string connectorId)
        {
            var connector = await _locationService.GetOurConnectorAsync(locationId, evseUid, connectorId);

            if (connector == null)
                throw OcpiException.UnknownLocation($"Connector not found: {connectorId}");

            return OcpiOk(connector);
        }
    }
}
