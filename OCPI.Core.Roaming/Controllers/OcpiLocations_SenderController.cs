using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPP.Core.Database;
using Microsoft.EntityFrameworkCore;
using BitzArt.Pagination;
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
            // Set maximum Limit value (required for OCPI.Net PageResult handling)
            SetMaxLimit(pageRequest, 100);

            var offset = pageRequest.Offset ?? 0;
            var limit = pageRequest.Limit ?? 100;

            // Fetch from database
            var locations = await _locationService.GetOurLocationsAsync(offset, limit);

            var result = new PageResult<OcpiLocation>(locations, locations.Count, locations.Count);
            // OcpiOk with PageResult automatically adds pagination headers
            return OcpiOk(result);
        }

        /// <summary>
        /// Get a specific location
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{locationId}")]
        public async Task<IActionResult> GetLocation(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId)
        {
            var location = await _locationService.GetOurLocationAsync(locationId);
            
            if (location == null)
                throw OcpiException.UnknownLocation($"Location not found: {locationId}");

            return OcpiOk(location);
        }

        /// <summary>
        /// Get a specific EVSE within a location
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{locationId}/{evseUid}")]
        public async Task<IActionResult> GetEvse(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
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
        [HttpGet("{countryCode}/{partyId}/{locationId}/{evseUid}/{connectorId}")]
        public async Task<IActionResult> GetConnector(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
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
