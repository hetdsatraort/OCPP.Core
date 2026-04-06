using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPP.Core.Database;
using Microsoft.EntityFrameworkCore;
using BitzArt.Pagination;

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
        private readonly OCPPCoreContext _dbContext;

        public OcpiLocations_SenderController(OCPPCoreContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// Get paginated list of all locations
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetLocations([FromQuery] OcpiPageRequest pageRequest)
        {
            // Set maximum Limit value (required for OCPI.Net PageResult handling)
            SetMaxLimit(pageRequest, 100);

            // TODO: Fetch from database and map to OCPI.Contracts.OcpiLocation
            // For now, returning sample data
            var locations = new List<OcpiLocation>
            {
                CreateSampleLocation("LOC001", "Main Charging Hub"),
                CreateSampleLocation("LOC002", "Downtown Station")
            };

            // Apply pagination
            var pagedResult = locations
                .Skip(pageRequest.Offset ?? 0)
                .Take(pageRequest.Limit ?? 100)
                .ToList();

            var result = new PageResult<OcpiLocation>(pagedResult, locations.Count, pagedResult.Count);
            // OcpiOk with PageResult automatically adds pagination headers
            return OcpiOk(result);
        }

        /// <summary>
        /// Get a specific location
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{locationId}")]
        public IActionResult GetLocation(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId)
        {
            // TODO: Fetch from database
            var location = CreateSampleLocation(locationId, $"Location {locationId}");
            
            return OcpiOk(location);
        }

        /// <summary>
        /// Get a specific EVSE within a location
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{locationId}/{evseUid}")]
        public IActionResult GetEvse(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromRoute] string evseUid)
        {
            // TODO: Fetch from database
            var evse = CreateSampleEvse(evseUid);
            
            return OcpiOk(evse);
        }

        /// <summary>
        /// Get a specific connector within an EVSE
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{locationId}/{evseUid}/{connectorId}")]
        public IActionResult GetConnector(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string locationId,
            [FromRoute] string evseUid,
            [FromRoute] string connectorId)
        {
            // TODO: Fetch from database
            var connector = CreateSampleConnector(connectorId);
            
            return OcpiOk(connector);
        }

        private OcpiLocation CreateSampleLocation(string id, string name)
        {
            return new OcpiLocation
            {
                CountryCode = CountryCode.India,
                PartyId = "CPO",
                Id = id,
                Publish = true,
                Name = name,
                Address = "123 Main Street",
                City = "Los Angeles",
                PostalCode = "90001",
                Country = "India",
                Coordinates = new OcpiGeolocation
                {
                    Latitude = "34.0522",
                    Longitude = "-118.2437"
                },
                TimeZone = "America/Los_Angeles",
                LastUpdated = DateTime.UtcNow,
                Evses = new List<OcpiEvse>
                {
                    CreateSampleEvse("EVSE-001")
                }
            };
        }

        private OcpiEvse CreateSampleEvse(string uid)
        {
            return new OcpiEvse
            {
                Uid = uid,
                EvseId = $"US*CPO*{uid}",
                Status = EvseStatus.Available,
                LastUpdated = DateTime.UtcNow,
                Connectors = new List<OcpiConnector>
                {
                    CreateSampleConnector("1")
                }
            };
        }

        private OcpiConnector CreateSampleConnector(string id)
        {
            return new OcpiConnector
            {
                Id = id,
                Standard = ConnectorType.IEC_62196_T2_Combo,
                Format = ConnectorFormat.Socket,
                PowerType = PowerType.Ac3Phase,
                MaxVoltage = 230,
                MaxAmperage = 32,
                MaxElectricPower = 22000,
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}
