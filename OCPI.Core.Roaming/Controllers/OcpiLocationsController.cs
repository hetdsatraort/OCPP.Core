using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCPI.Core.Roaming.Models.OCPI;
using OCPI.Core.Roaming.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Locations Controller - Manages charging location information
    /// </summary>
    [Route("ocpi/2.2.1/locations")]
    [ApiController]
    public class OcpiLocationsController : ControllerBase
    {
        private readonly IOcpiLocationService _locationService;
        private readonly ILogger<OcpiLocationsController> _logger;

        public OcpiLocationsController(
            IOcpiLocationService locationService,
            ILogger<OcpiLocationsController> logger)
        {
            _locationService = locationService;
            _logger = logger;
        }

        /// <summary>
        /// Get all locations
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(OcpiPagedResponseDto<OcpiLocationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLocations(
            [FromQuery] string countryCode = null,
            [FromQuery] string partyId = null,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 100)
        {
            try
            {
                _logger.LogInformation($"OCPI GetLocations called. CountryCode: {countryCode}, PartyId: {partyId}");

                var locations = await _locationService.GetLocationsAsync(countryCode, partyId);

                var response = new OcpiPagedResponseDto<OcpiLocationDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = locations,
                    Timestamp = DateTime.UtcNow,
                    Limit = limit,
                    Offset = offset,
                    TotalCount = locations.Count
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLocations");

                var errorResponse = new OcpiPagedResponseDto<OcpiLocationDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Get a specific location by ID
        /// </summary>
        [HttpGet("{locationId}")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiLocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiLocationDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetLocation([FromRoute] string locationId)
        {
            try
            {
                _logger.LogInformation($"OCPI GetLocation called for: {locationId}");

                var location = await _locationService.GetLocationByIdAsync(locationId);

                if (location == null)
                {
                    var notFoundResponse = new OcpiResponseDto<OcpiLocationDto>
                    {
                        StatusCode = 3001,
                        StatusMessage = $"Location not found: {locationId}",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return NotFound(notFoundResponse);
                }

                var response = new OcpiResponseDto<OcpiLocationDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = location,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetLocation for: {locationId}");

                var errorResponse = new OcpiResponseDto<OcpiLocationDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Create or update a location
        /// </summary>
        [HttpPut("{locationId}")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiLocationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiLocationDto>), StatusCodes.Status201Created)]
        public async Task<IActionResult> PutLocation([FromRoute] string locationId, [FromBody] OcpiLocationDto location)
        {
            try
            {
                _logger.LogInformation($"OCPI PutLocation called for: {locationId}");

                if (!ModelState.IsValid)
                {
                    var errorResponse = new OcpiResponseDto<OcpiLocationDto>
                    {
                        StatusCode = 2001,
                        StatusMessage = "Invalid or missing parameters",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return BadRequest(errorResponse);
                }

                location.Id = locationId;
                var existingLocation = await _locationService.GetLocationByIdAsync(locationId);
                var isNewLocation = existingLocation == null;

                var savedLocation = await _locationService.CreateOrUpdateLocationAsync(location);

                var response = new OcpiResponseDto<OcpiLocationDto>
                {
                    StatusCode = 1000,
                    StatusMessage = isNewLocation ? "Location created" : "Location updated",
                    Data = savedLocation,
                    Timestamp = DateTime.UtcNow
                };

                return isNewLocation 
                    ? StatusCode(StatusCodes.Status201Created, response)
                    : Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in PutLocation for: {locationId}");

                var errorResponse = new OcpiResponseDto<OcpiLocationDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Get a specific EVSE
        /// </summary>
        [HttpGet("{locationId}/{evseUid}")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiEvseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiEvseDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvse([FromRoute] string locationId, [FromRoute] string evseUid)
        {
            try
            {
                _logger.LogInformation($"OCPI GetEvse called for location: {locationId}, EVSE: {evseUid}");

                var evse = await _locationService.GetEvseAsync(locationId, evseUid);

                if (evse == null)
                {
                    var notFoundResponse = new OcpiResponseDto<OcpiEvseDto>
                    {
                        StatusCode = 3001,
                        StatusMessage = $"EVSE not found: {evseUid}",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return NotFound(notFoundResponse);
                }

                var response = new OcpiResponseDto<OcpiEvseDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = evse,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetEvse for location: {locationId}, EVSE: {evseUid}");

                var errorResponse = new OcpiResponseDto<OcpiEvseDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Get a specific connector
        /// </summary>
        [HttpGet("{locationId}/{evseUid}/{connectorId}")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiConnectorDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiConnectorDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConnector(
            [FromRoute] string locationId, 
            [FromRoute] string evseUid, 
            [FromRoute] string connectorId)
        {
            try
            {
                _logger.LogInformation($"OCPI GetConnector called for location: {locationId}, EVSE: {evseUid}, Connector: {connectorId}");

                var connector = await _locationService.GetConnectorAsync(locationId, evseUid, connectorId);

                if (connector == null)
                {
                    var notFoundResponse = new OcpiResponseDto<OcpiConnectorDto>
                    {
                        StatusCode = 3001,
                        StatusMessage = $"Connector not found: {connectorId}",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return NotFound(notFoundResponse);
                }

                var response = new OcpiResponseDto<OcpiConnectorDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = connector,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetConnector for location: {locationId}, EVSE: {evseUid}, Connector: {connectorId}");

                var errorResponse = new OcpiResponseDto<OcpiConnectorDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }
    }
}
