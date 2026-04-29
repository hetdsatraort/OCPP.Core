using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Locations Receiver Controller - Receives location data from partners (eMSP role)
    /// </summary>
    [OcpiEndpoint(OcpiModule.Locations, "Receiver", "2.2.1")]
    [Route("2.2.1/locations/receiver")]
    [OcpiAuthorize]
    public class OcpiLocations_ReceiverController : OcpiController
    {
        private readonly IOcpiLocationService _locationService;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly ILogger<OcpiLocations_ReceiverController> _logger;

        public OcpiLocations_ReceiverController(
            IOcpiLocationService locationService,
            IOcpiCredentialsService credentialsService,
            ILogger<OcpiLocations_ReceiverController> logger)
        {
            _locationService = locationService;
            _credentialsService = credentialsService;
            _logger = logger;
        }

        /// <summary>
        /// Receive location data from partner
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{locationId}")]
        public async Task<IActionResult> PutLocation(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromBody] OcpiLocation location)
        {
            // Validate location data
            OcpiValidate(location);

            // Get partner credential from Authorization header
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(token);

            if (partner == null)
                throw OcpiException.InvalidParameters("Invalid partner credentials");

            // Store/update location in database
            await _locationService.StorePartnerLocationAsync(partner.Id, location);

            _logger.LogInformation("Stored partner location {LocationId}", locationId);

            return OcpiOk(location);
        }

        /// <summary>
        /// Partially update location data
        /// </summary>
        [HttpPatch("{countryCode}/{partyId}/{locationId}")]
        public async Task<IActionResult> PatchLocation(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromBody] OcpiLocation location)
        {
            // Validate location data
            OcpiValidate(location);

            // Get partner credential
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(token);

            if (partner == null)
                throw OcpiException.InvalidParameters("Invalid partner credentials");

            // Partially update location in database
            await _locationService.StorePartnerLocationAsync(partner.Id, location);

            _logger.LogInformation("Updated partner location {LocationId}", locationId);

            return OcpiOk(location);
        }

        /// <summary>
        /// Update EVSE within a location
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{locationId}/{evseUid}")]
        public async Task<IActionResult> PutEvse(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromRoute] string evseUid,
            [FromBody] OcpiEvse evse)
        {
            // Validate EVSE data
            OcpiValidate(evse);

            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(token);

            if (partner == null)
                throw OcpiException.InvalidParameters("Invalid partner credentials");

            // Resolve the stored partner location PK
            var partnerLocationId = await _locationService.GetPartnerLocationDbIdAsync(countryCode, partyId, locationId);
            if (partnerLocationId == null)
                throw OcpiException.UnknownLocation($"Location {locationId} not found for partner {countryCode}/{partyId}");

            await _locationService.StorePartnerEvseAsync(partnerLocationId.Value, evse);

            _logger.LogInformation("Stored EVSE {EvseUid} for partner location {LocationId}", evseUid, locationId);

            return OcpiOk(evse);
        }

        /// <summary>
        /// Update connector within an EVSE
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{locationId}/{evseUid}/{connectorId}")]
        public async Task<IActionResult> PutConnector(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromRoute] string evseUid,
            [FromRoute] string connectorId,
            [FromBody] OcpiConnector connector)
        {
            // Validate connector data
            OcpiValidate(connector);

            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(token);

            if (partner == null)
                throw OcpiException.InvalidParameters("Invalid partner credentials");

            // Resolve the stored partner location PK
            var partnerLocationId = await _locationService.GetPartnerLocationDbIdAsync(countryCode, partyId, locationId);
            if (partnerLocationId == null)
                throw OcpiException.UnknownLocation($"Location {locationId} not found for partner {countryCode}/{partyId}");

            // Resolve the stored partner EVSE PK
            var partnerEvseId = await _locationService.GetPartnerEvseDbIdAsync(partnerLocationId.Value, evseUid);
            if (partnerEvseId == null)
                throw OcpiException.UnknownLocation($"EVSE {evseUid} not found under location {locationId}");

            await _locationService.StorePartnerConnectorAsync(partnerEvseId.Value, connector);

            _logger.LogInformation("Stored connector {ConnectorId} for EVSE {EvseUid}", connectorId, evseUid);

            return OcpiOk(connector);
        }
    }
}