using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.ChargingHub;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChargingHubController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ChargingHubController> _logger;

        public ChargingHubController(
            OCPPCoreContext dbContext,
            ILogger<ChargingHubController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        #region Charging Hub Management

        /// <summary>
        /// Add new charging hub
        /// </summary>
        [HttpPost("charging-hub-add")]
        [Authorize]
        public async Task<IActionResult> AddChargingHub([FromBody] ChargingHubRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var hub = new Database.EVCDTO.ChargingHub
                {
                    RecId = Guid.NewGuid().ToString(),
                    AddressLine1 = request.AddressLine1,
                    AddressLine2 = request.AddressLine2,
                    AddressLine3 = request.AddressLine3,
                    ChargingHubImage = request.ChargingHubImage,
                    City = request.City,
                    State = request.State,
                    Pincode = request.Pincode,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    OpeningTime = request.OpeningTime,
                    ClosingTime = request.ClosingTime,
                    TypeATariff = request.TypeATariff,
                    TypeBTariff = request.TypeBTariff,
                    Amenities = request.Amenities,
                    AdditionalInfo1 = request.AdditionalInfo1,
                    AdditionalInfo2 = request.AdditionalInfo2,
                    AdditionalInfo3 = request.AdditionalInfo3,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargingHubs.Add(hub);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging hub added: {hub.RecId} in {hub.City}");

                return Ok(new ChargingHubResponseDto
                {
                    Success = true,
                    Message = "Charging hub added successfully",
                    Hub = MapToChargingHubDto(hub)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding charging hub");
                return StatusCode(500, new ChargingHubResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding charging hub"
                });
            }
        }

        /// <summary>
        /// Update charging hub
        /// </summary>
        [HttpPut("charging-hub-update")]
        [Authorize]
        public async Task<IActionResult> UpdateChargingHub([FromBody] ChargingHubUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var hub = await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == request.RecId && h.Active == 1);
                if (hub == null)
                {
                    return NotFound(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Charging hub not found"
                    });
                }

                // Update properties
                hub.AddressLine1 = request.AddressLine1;
                hub.AddressLine2 = request.AddressLine2;
                hub.AddressLine3 = request.AddressLine3;
                hub.ChargingHubImage = request.ChargingHubImage;
                hub.City = request.City;
                hub.State = request.State;
                hub.Pincode = request.Pincode;
                hub.Latitude = request.Latitude;
                hub.Longitude = request.Longitude;
                hub.OpeningTime = request.OpeningTime;
                hub.ClosingTime = request.ClosingTime;
                hub.TypeATariff = request.TypeATariff;
                hub.TypeBTariff = request.TypeBTariff;
                hub.Amenities = request.Amenities;
                hub.AdditionalInfo1 = request.AdditionalInfo1;
                hub.AdditionalInfo2 = request.AdditionalInfo2;
                hub.AdditionalInfo3 = request.AdditionalInfo3;
                hub.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging hub updated: {hub.RecId}");

                return Ok(new ChargingHubResponseDto
                {
                    Success = true,
                    Message = "Charging hub updated successfully",
                    Hub = MapToChargingHubDto(hub)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charging hub");
                return StatusCode(500, new ChargingHubResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating charging hub"
                });
            }
        }

        /// <summary>
        /// Delete charging hub (soft delete)
        /// </summary>
        [HttpDelete("charging-hub-delete/{hubId}")]
        [Authorize]
        public async Task<IActionResult> DeleteChargingHub(string hubId)
        {
            try
            {
                var hub = await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == hubId && h.Active == 1);
                if (hub == null)
                {
                    return NotFound(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Charging hub not found"
                    });
                }

                // Soft delete
                hub.Active = 0;
                hub.UpdatedOn = DateTime.UtcNow;

                // Also soft delete associated stations
                var stations = await _dbContext.ChargingStations
                    .Where(s => s.ChargingHubId == hubId && s.Active == 1)
                    .ToListAsync();

                foreach (var station in stations)
                {
                    station.Active = 0;
                    station.UpdatedOn = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging hub deleted: {hubId}");

                return Ok(new ChargingHubResponseDto
                {
                    Success = true,
                    Message = "Charging hub deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting charging hub");
                return StatusCode(500, new ChargingHubResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting charging hub"
                });
            }
        }

        /// <summary>
        /// Get list of all charging hubs
        /// </summary>
        [HttpGet("charging-hub-list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargingHubList([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var hubs = await _dbContext.ChargingHubs
                    .Where(h => h.Active == 1)
                    .OrderByDescending(h => h.CreatedOn)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalCount = await _dbContext.ChargingHubs.CountAsync(h => h.Active == 1);

                var hubDtos = hubs.Select(h =>
                {
                    var dto = MapToChargingHubDto(h);
                    dto.StationCount = _dbContext.ChargingStations.Count(s => s.ChargingHubId == h.RecId && s.Active == 1);
                    
                    var reviews = _dbContext.ChargingHubReviews
                        .Where(r => r.ChargingHubId == h.RecId && r.Active == 1)
                        .ToList();
                    
                    if (reviews.Any())
                    {
                        dto.AverageRating = reviews.Average(r => r.Rating);
                    }
                    
                    return dto;
                }).ToList();

                return Ok(new ChargingHubListResponseDto
                {
                    Success = true,
                    Message = "Charging hubs retrieved successfully",
                    Hubs = hubDtos,
                    TotalCount = totalCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging hub list");
                return StatusCode(500, new ChargingHubResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging hubs"
                });
            }
        }

        /// <summary>
        /// Get charging hub details with stations and reviews
        /// </summary>
        [HttpGet("charging-hub-details/{hubId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargingHubDetails(string hubId)
        {
            try
            {
                var hub = await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == hubId && h.Active == 1);
                if (hub == null)
                {
                    return NotFound(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Charging hub not found"
                    });
                }

                // Get stations
                var stations = await _dbContext.ChargingStations
                    .Where(s => s.ChargingHubId == hubId && s.Active == 1)
                    .ToListAsync();

                var stationDtos = stations.Select(s =>
                {
                    var chargePoint = _dbContext.ChargePoints.FirstOrDefault(cp => cp.ChargePointId == s.ChargingPointId);
                    return MapToChargingStationDto(s, chargePoint);
                }).ToList();

                // Get reviews
                var reviews = await _dbContext.ChargingHubReviews
                    .Where(r => r.ChargingHubId == hubId && r.Active == 1)
                    .OrderByDescending(r => r.ReviewTime)
                    .ToListAsync();

                var reviewDtos = reviews.Select(MapToReviewDto).ToList();

                double avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

                return Ok(new ChargingHubDetailsResponseDto
                {
                    Success = true,
                    Message = "Charging hub details retrieved successfully",
                    Hub = MapToChargingHubDto(hub),
                    Stations = stationDtos,
                    Reviews = reviewDtos,
                    AverageRating = avgRating,
                    TotalReviews = reviews.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging hub details");
                return StatusCode(500, new ChargingHubResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging hub details"
                });
            }
        }

        /// <summary>
        /// Search charging hubs by location (lat/long and radius)
        /// </summary>
        [HttpPost("charging-hub-search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchChargingHubsByLocation([FromBody] ChargingHubSearchDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingHubListResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var allHubs = await _dbContext.ChargingHubs
                    .Where(h => h.Active == 1)
                    .ToListAsync();

                // Calculate distance and filter
                var nearbyHubs = allHubs
                    .Select(h =>
                    {
                        if (double.TryParse(h.Latitude, out var hubLat) && 
                            double.TryParse(h.Longitude, out var hubLng))
                        {
                            var distance = CalculateDistance(request.Latitude, request.Longitude, hubLat, hubLng);
                            if (distance <= request.RadiusKm)
                            {
                                var dto = MapToChargingHubDto(h);
                                dto.DistanceKm = Math.Round(distance, 2);
                                dto.StationCount = _dbContext.ChargingStations.Count(s => s.ChargingHubId == h.RecId && s.Active == 1);
                                
                                var reviews = _dbContext.ChargingHubReviews
                                    .Where(r => r.ChargingHubId == h.RecId && r.Active == 1)
                                    .ToList();
                                
                                if (reviews.Any())
                                {
                                    dto.AverageRating = reviews.Average(r => r.Rating);
                                }
                                
                                return dto;
                            }
                        }
                        return null;
                    })
                    .Where(dto => dto != null)
                    .OrderBy(dto => dto.DistanceKm)
                    .ToList();

                return Ok(new ChargingHubListResponseDto
                {
                    Success = true,
                    Message = $"Found {nearbyHubs.Count} charging hubs within {request.RadiusKm}km",
                    Hubs = nearbyHubs,
                    TotalCount = nearbyHubs.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching charging hubs by location");
                return StatusCode(500, new ChargingHubListResponseDto
                {
                    Success = false,
                    Message = "An error occurred while searching charging hubs"
                });
            }
        }

        #endregion

        #region Charging Station Management

        /// <summary>
        /// Add new charging station
        /// </summary>
        [HttpPost("charging-station-add")]
        [Authorize]
        public async Task<IActionResult> AddChargingStation([FromBody] ChargingStationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Verify hub exists
                var hub = await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == request.ChargingHubId && h.Active == 1);
                if (hub == null)
                {
                    return NotFound(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging hub not found"
                    });
                }

                // Verify ChargePoint exists
                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == request.ChargingPointId);
                if (chargePoint == null)
                {
                    return NotFound(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charge point not found"
                    });
                }

                var station = new Database.EVCDTO.ChargingStation
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargingHubId = request.ChargingHubId,
                    ChargingPointId = request.ChargingPointId,
                    ChargingGunCount = request.ChargingGunCount,
                    ChargingStationImage = request.ChargingStationImage,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargingStations.Add(station);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging station added: {station.RecId} at hub {request.ChargingHubId}");

                return Ok(new ChargingStationResponseDto
                {
                    Success = true,
                    Message = "Charging station added successfully",
                    Station = MapToChargingStationDto(station, chargePoint)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding charging station");
                return StatusCode(500, new ChargingStationResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding charging station"
                });
            }
        }

        /// <summary>
        /// Update charging station
        /// </summary>
        [HttpPut("charging-station-update")]
        [Authorize]
        public async Task<IActionResult> UpdateChargingStation([FromBody] ChargingStationUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.RecId == request.RecId && s.Active == 1);
                if (station == null)
                {
                    return NotFound(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                // Update properties
                station.ChargingHubId = request.ChargingHubId;
                station.ChargingPointId = request.ChargingPointId;
                station.ChargingGunCount = request.ChargingGunCount;
                station.ChargingStationImage = request.ChargingStationImage;
                station.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == request.ChargingPointId);

                _logger.LogInformation($"Charging station updated: {station.RecId}");

                return Ok(new ChargingStationResponseDto
                {
                    Success = true,
                    Message = "Charging station updated successfully",
                    Station = MapToChargingStationDto(station, chargePoint)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charging station");
                return StatusCode(500, new ChargingStationResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating charging station"
                });
            }
        }

        /// <summary>
        /// Delete charging station (soft delete)
        /// </summary>
        [HttpDelete("charging-station-delete/{stationId}")]
        [Authorize]
        public async Task<IActionResult> DeleteChargingStation(string stationId)
        {
            try
            {
                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.RecId == stationId && s.Active == 1);
                if (station == null)
                {
                    return NotFound(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                // Soft delete
                station.Active = 0;
                station.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging station deleted: {stationId}");

                return Ok(new ChargingStationResponseDto
                {
                    Success = true,
                    Message = "Charging station deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting charging station");
                return StatusCode(500, new ChargingStationResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting charging station"
                });
            }
        }

        /// <summary>
        /// Get list of charging stations for a hub
        /// </summary>
        [HttpGet("charging-station-list/{hubId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargingStationList(string hubId)
        {
            try
            {
                var stations = await _dbContext.ChargingStations
                    .Where(s => s.ChargingHubId == hubId && s.Active == 1)
                    .ToListAsync();

                var stationDtos = stations.Select(s =>
                {
                    var chargePoint = _dbContext.ChargePoints.FirstOrDefault(cp => cp.ChargePointId == s.ChargingPointId);
                    return MapToChargingStationDto(s, chargePoint);
                }).ToList();

                return Ok(new ChargingStationListResponseDto
                {
                    Success = true,
                    Message = "Charging stations retrieved successfully",
                    Stations = stationDtos,
                    TotalCount = stationDtos.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging station list");
                return StatusCode(500, new ChargingStationResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging stations"
                });
            }
        }

        /// <summary>
        /// Get charging station details with chargers and reviews
        /// </summary>
        [HttpGet("charging-station-details/{stationId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargingStationDetails(string stationId)
        {
            try
            {
                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.RecId == stationId && s.Active == 1);
                if (station == null)
                {
                    return NotFound(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == station.ChargingPointId);

                // Get chargers (connectors)
                var chargers = await _dbContext.ConnectorStatuses
                    .Where(c => c.ChargePointId == station.ChargingPointId && c.Active == 1)
                    .ToListAsync();

                var chargerDtos = chargers.Select(c => MapToChargerDto(c, station.RecId, chargePoint?.Name)).ToList();

                // Get reviews
                var reviews = await _dbContext.ChargingHubReviews
                    .Where(r => r.ChargingStationId == stationId && r.Active == 1)
                    .OrderByDescending(r => r.ReviewTime)
                    .ToListAsync();

                var reviewDtos = reviews.Select(MapToReviewDto).ToList();

                return Ok(new ChargingStationDetailsResponseDto
                {
                    Success = true,
                    Message = "Charging station details retrieved successfully",
                    Station = MapToChargingStationDto(station, chargePoint),
                    Chargers = chargerDtos,
                    Reviews = reviewDtos
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging station details");
                return StatusCode(500, new ChargingStationResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging station details"
                });
            }
        }

        #endregion

        #region Charger (Gun/Connector) Management

        /// <summary>
        /// Add new charger/gun (connector)
        /// </summary>
        [HttpPost("chargers-add")]
        [Authorize]
        public async Task<IActionResult> AddCharger([FromBody] ChargerRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Check if connector already exists (active only)
                var existing = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(c => c.ChargePointId == request.ChargePointId && c.ConnectorId == request.ConnectorId && c.Active == 1);

                if (existing != null)
                {
                    return BadRequest(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger with this connector ID already exists for this charge point"
                    });
                }

                var charger = new ConnectorStatus
                {
                    ChargePointId = request.ChargePointId,
                    ConnectorId = request.ConnectorId,
                    ConnectorName = request.ConnectorName,
                    LastStatus = "Available",
                    Active = 1
                };

                _dbContext.ConnectorStatuses.Add(charger);
                await _dbContext.SaveChangesAsync();

                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.ChargingPointId == request.ChargePointId);
                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == request.ChargePointId);

                _logger.LogInformation($"Charger added: {request.ChargePointId}:{request.ConnectorId}");

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger added successfully",
                    Charger = MapToChargerDto(charger, station?.RecId, chargePoint?.Name)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding charger");
                return StatusCode(500, new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding charger"
                });
            }
        }

        /// <summary>
        /// Update charger/gun (connector)
        /// </summary>
        [HttpPut("chargers-update")]
        [Authorize]
        public async Task<IActionResult> UpdateCharger([FromBody] ChargerUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var charger = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(c => c.ChargePointId == request.ChargePointId && c.ConnectorId == request.ConnectorId && c.Active == 1);

                if (charger == null)
                {
                    return NotFound(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger not found"
                    });
                }

                charger.ConnectorName = request.ConnectorName;
                if (!string.IsNullOrEmpty(request.LastStatus))
                {
                    charger.LastStatus = request.LastStatus;
                    charger.LastStatusTime = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();

                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.ChargingPointId == request.ChargePointId);
                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == request.ChargePointId);

                _logger.LogInformation($"Charger updated: {request.ChargePointId}:{request.ConnectorId}");

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger updated successfully",
                    Charger = MapToChargerDto(charger, station?.RecId, chargePoint?.Name)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charger");
                return StatusCode(500, new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating charger"
                });
            }
        }

        /// <summary>
        /// Delete charger/gun (connector) - Soft delete
        /// </summary>
        [HttpDelete("chargers-delete/{chargePointId}/{connectorId}")]
        [Authorize]
        public async Task<IActionResult> DeleteCharger(string chargePointId, int connectorId)
        {
            try
            {
                var charger = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(c => c.ChargePointId == chargePointId && c.ConnectorId == connectorId && c.Active == 1);

                if (charger == null)
                {
                    return NotFound(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger not found"
                    });
                }

                // Soft delete
                charger.Active = 0;
                charger.LastStatusTime = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger deleted (soft): {chargePointId}:{connectorId}");

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting charger");
                return StatusCode(500, new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting charger"
                });
            }
        }

        /// <summary>
        /// Get list of chargers for a charging station
        /// </summary>
        [HttpGet("charger-list/{stationId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargerList(string stationId)
        {
            try
            {
                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.RecId == stationId && s.Active == 1);
                if (station == null)
                {
                    return NotFound(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                var chargers = await _dbContext.ConnectorStatuses
                    .Where(c => c.ChargePointId == station.ChargingPointId)
                    .ToListAsync();

                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == station.ChargingPointId);

                var chargerDtos = chargers.Select(c => MapToChargerDto(c, stationId, chargePoint?.Name)).ToList();

                return Ok(new ChargerListResponseDto
                {
                    Success = true,
                    Message = "Chargers retrieved successfully",
                    Chargers = chargerDtos,
                    TotalCount = chargerDtos.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charger list");
                return StatusCode(500, new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving chargers"
                });
            }
        }

        /// <summary>
        /// Get charger details
        /// </summary>
        [HttpGet("charger-details/{chargePointId}/{connectorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargerDetails(string chargePointId, int connectorId)
        {
            try
            {
                var charger = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(c => c.ChargePointId == chargePointId && c.ConnectorId == connectorId && c.Active == 1);

                if (charger == null)
                {
                    return NotFound(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger not found"
                    });
                }

                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.ChargingPointId == chargePointId);
                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == chargePointId);

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger details retrieved successfully",
                    Charger = MapToChargerDto(charger, station?.RecId, chargePoint?.Name)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charger details");
                return StatusCode(500, new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charger details"
                });
            }
        }

        #endregion

        #region Review Management

        /// <summary>
        /// Add hub review
        /// </summary>
        [HttpPost("charging-hub-review-add")]
        [Authorize]
        public async Task<IActionResult> AddHubReview([FromBody] ReviewRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                if (string.IsNullOrEmpty(request.ChargingHubId))
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "ChargingHubId is required"
                    });
                }

                var review = new Database.EVCDTO.ChargingHubReview
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargingHubId = request.ChargingHubId,
                    ChargingStationId = request.ChargingStationId,
                    Rating = request.Rating,
                    Description = request.Description,
                    ReviewTime = DateTime.UtcNow,
                    ReviewImage1 = request.ReviewImage1,
                    ReviewImage2 = request.ReviewImage2,
                    ReviewImage3 = request.ReviewImage3,
                    ReviewImage4 = request.ReviewImage4,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargingHubReviews.Add(review);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Hub review added: {review.RecId}");

                return Ok(new ReviewResponseDto
                {
                    Success = true,
                    Message = "Review added successfully",
                    Review = MapToReviewDto(review)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding hub review");
                return StatusCode(500, new ReviewResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding review"
                });
            }
        }

        /// <summary>
        /// Add station review
        /// </summary>
        [HttpPost("charging-stn-review-add")]
        [Authorize]
        public async Task<IActionResult> AddStationReview([FromBody] ReviewRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                if (string.IsNullOrEmpty(request.ChargingStationId))
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "ChargingStationId is required"
                    });
                }

                var review = new Database.EVCDTO.ChargingHubReview
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargingHubId = request.ChargingHubId,
                    ChargingStationId = request.ChargingStationId,
                    Rating = request.Rating,
                    Description = request.Description,
                    ReviewTime = DateTime.UtcNow,
                    ReviewImage1 = request.ReviewImage1,
                    ReviewImage2 = request.ReviewImage2,
                    ReviewImage3 = request.ReviewImage3,
                    ReviewImage4 = request.ReviewImage4,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargingHubReviews.Add(review);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Station review added: {review.RecId}");

                return Ok(new ReviewResponseDto
                {
                    Success = true,
                    Message = "Review added successfully",
                    Review = MapToReviewDto(review)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding station review");
                return StatusCode(500, new ReviewResponseDto
                {
                    Success = false,
                    Message = "An error occurred while adding review"
                });
            }
        }

        /// <summary>
        /// Update review
        /// </summary>
        [HttpPut("charging-hub-review-update")]
        [Authorize]
        public async Task<IActionResult> UpdateReview([FromBody] ReviewUpdateDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var review = await _dbContext.ChargingHubReviews.FirstOrDefaultAsync(r => r.RecId == request.RecId && r.Active == 1);
                if (review == null)
                {
                    return NotFound(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Review not found"
                    });
                }

                review.Rating = request.Rating;
                review.Description = request.Description;
                review.ReviewImage1 = request.ReviewImage1;
                review.ReviewImage2 = request.ReviewImage2;
                review.ReviewImage3 = request.ReviewImage3;
                review.ReviewImage4 = request.ReviewImage4;
                review.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Review updated: {review.RecId}");

                return Ok(new ReviewResponseDto
                {
                    Success = true,
                    Message = "Review updated successfully",
                    Review = MapToReviewDto(review)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating review");
                return StatusCode(500, new ReviewResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating review"
                });
            }
        }

        /// <summary>
        /// Delete review (soft delete)
        /// </summary>
        [HttpDelete("charging-hub-review-delete/{reviewId}")]
        [Authorize]
        public async Task<IActionResult> DeleteReview(string reviewId)
        {
            try
            {
                var review = await _dbContext.ChargingHubReviews.FirstOrDefaultAsync(r => r.RecId == reviewId && r.Active == 1);
                if (review == null)
                {
                    return NotFound(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Review not found"
                    });
                }

                review.Active = 0;
                review.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Review deleted: {reviewId}");

                return Ok(new ReviewResponseDto
                {
                    Success = true,
                    Message = "Review deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review");
                return StatusCode(500, new ReviewResponseDto
                {
                    Success = false,
                    Message = "An error occurred while deleting review"
                });
            }
        }

        /// <summary>
        /// Get reviews for a charging hub
        /// </summary>
        [HttpGet("charging-hub-review-list/{hubId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetHubReviewList(string hubId)
        {
            try
            {
                var reviews = await _dbContext.ChargingHubReviews
                    .Where(r => r.ChargingHubId == hubId && r.Active == 1)
                    .OrderByDescending(r => r.ReviewTime)
                    .ToListAsync();

                var reviewDtos = reviews.Select(MapToReviewDto).ToList();
                double avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

                return Ok(new ReviewListResponseDto
                {
                    Success = true,
                    Message = "Reviews retrieved successfully",
                    Reviews = reviewDtos,
                    TotalCount = reviewDtos.Count,
                    AverageRating = avgRating
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hub reviews");
                return StatusCode(500, new ReviewResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving reviews"
                });
            }
        }

        #endregion

        #region Helper Methods

        private ChargingHubDto MapToChargingHubDto(Database.EVCDTO.ChargingHub hub)
        {
            return new ChargingHubDto
            {
                RecId = hub.RecId,
                AddressLine1 = hub.AddressLine1,
                AddressLine2 = hub.AddressLine2,
                AddressLine3 = hub.AddressLine3,
                ChargingHubImage = hub.ChargingHubImage,
                City = hub.City,
                State = hub.State,
                Pincode = hub.Pincode,
                Latitude = hub.Latitude,
                Longitude = hub.Longitude,
                OpeningTime = hub.OpeningTime,
                ClosingTime = hub.ClosingTime,
                TypeATariff = hub.TypeATariff,
                TypeBTariff = hub.TypeBTariff,
                Amenities = hub.Amenities,
                AdditionalInfo1 = hub.AdditionalInfo1,
                AdditionalInfo2 = hub.AdditionalInfo2,
                AdditionalInfo3 = hub.AdditionalInfo3,
                Active = hub.Active,
                CreatedOn = hub.CreatedOn,
                UpdatedOn = hub.UpdatedOn
            };
        }

        private ChargingStationDto MapToChargingStationDto(Database.EVCDTO.ChargingStation station, ChargePoint chargePoint = null)
        {
            var hub = _dbContext.ChargingHubs.FirstOrDefault(h => h.RecId == station.ChargingHubId);
            
            return new ChargingStationDto
            {
                RecId = station.RecId,
                ChargingPointId = station.ChargingPointId,
                ChargingHubId = station.ChargingHubId,
                ChargingGunCount = station.ChargingGunCount,
                ChargingStationImage = station.ChargingStationImage,
                CreatedOn = station.CreatedOn,
                UpdatedOn = station.UpdatedOn,
                ChargePointName = chargePoint?.Name,
                ChargePointComment = chargePoint?.Comment,
                HubCity = hub?.City,
                HubState = hub?.State
            };
        }

        private ChargerDto MapToChargerDto(ConnectorStatus connector, string stationRecId = null, string chargePointName = null)
        {
            return new ChargerDto
            {
                ChargePointId = connector.ChargePointId,
                ConnectorId = connector.ConnectorId,
                ConnectorName = connector.ConnectorName,
                LastStatus = connector.LastStatus,
                LastStatusTime = connector.LastStatusTime,
                LastMeter = connector.LastMeter,
                LastMeterTime = connector.LastMeterTime,
                StationRecId = stationRecId,
                ChargePointName = chargePointName
            };
        }

        private ReviewDto MapToReviewDto(Database.EVCDTO.ChargingHubReview review)
        {
            return new ReviewDto
            {
                RecId = review.RecId,
                ChargingHubId = review.ChargingHubId,
                ChargingStationId = review.ChargingStationId,
                Rating = review.Rating,
                Description = review.Description,
                ReviewTime = review.ReviewTime,
                ReviewImage1 = review.ReviewImage1,
                ReviewImage2 = review.ReviewImage2,
                ReviewImage3 = review.ReviewImage3,
                ReviewImage4 = review.ReviewImage4,
                CreatedOn = review.CreatedOn,
                UpdatedOn = review.UpdatedOn
            };
        }

        /// <summary>
        /// Calculate distance between two coordinates using Haversine formula
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in km

            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        #endregion
    }
}
