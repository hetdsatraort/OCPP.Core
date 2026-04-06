using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;

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
        /// <summary>
        /// Receive location data from partner
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{locationId}")]
        public IActionResult PutLocation(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromBody] OcpiLocation location)
        {
            // Validate location data
            OcpiValidate(location);

            // TODO: Store/update location in database
            // _dbContext.Locations.Update(MapToDbLocation(location));
            // await _dbContext.SaveChangesAsync();

            return OcpiOk(location);
        }

        /// <summary>
        /// Partially update location data
        /// </summary>
        [HttpPatch("{countryCode}/{partyId}/{locationId}")]
        public IActionResult PatchLocation(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromBody] OcpiLocation location)
        {
            // Validate location data
            OcpiValidate(location);

            // TODO: Partially update location in database

            return OcpiOk(location);
        }

        /// <summary>
        /// Update EVSE within a location
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{locationId}/{evseUid}")]
        public IActionResult PutEvse(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromRoute] string evseUid,
            [FromBody] OcpiEvse evse)
        {
            // Validate EVSE data
            OcpiValidate(evse);

            // TODO: Update EVSE in database

            return OcpiOk(evse);
        }

        /// <summary>
        /// Update connector within an EVSE
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{locationId}/{evseUid}/{connectorId}")]
        public IActionResult PutConnector(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromRoute] string evseUid,
            [FromRoute] string connectorId,
            [FromBody] OcpiConnector connector)
        {
            // Validate connector data
            OcpiValidate(connector);

            // TODO: Update connector in database

            return OcpiOk(connector);
        }
    }
}