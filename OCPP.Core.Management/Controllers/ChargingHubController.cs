using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.ChargingHub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
                    return Ok(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var hub = new Database.EVCDTO.ChargingHub
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargingHubName = request.ChargingHubName,
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
                return Ok(new ChargingHubResponseDto
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
                    return Ok(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var hub = await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == request.RecId && h.Active == 1);
                if (hub == null)
                {
                    return Ok(new ChargingHubResponseDto
                    {
                        Success = false,
                        Message = "Charging hub not found"
                    });
                }

                // Update properties
                hub.ChargingHubName = request.ChargingHubName;
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
                return Ok(new ChargingHubResponseDto
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
                    return Ok(new ChargingHubResponseDto
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
                return Ok(new ChargingHubResponseDto
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
                return Ok(new ChargingHubResponseDto
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
                    return Ok(new ChargingHubResponseDto
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
                return Ok(new ChargingHubResponseDto
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
                    return Ok(new ChargingHubListResponseDto
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
                return Ok(new ChargingHubListResponseDto
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
                    return Ok(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Verify hub exists
                var hub = await _dbContext.ChargingHubs.FirstOrDefaultAsync(h => h.RecId == request.ChargingHubId && h.Active == 1);
                if (hub == null)
                {
                    return Ok(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging hub not found"
                    });
                }

                // Check if ChargePoint exists, if not create it
                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == request.ChargingPointId);
                if (chargePoint == null)
                {
                    // Create new ChargePoint in OCPP layer
                    chargePoint = new ChargePoint
                    {
                        ChargePointId = request.ChargingPointId,
                        Name = $"Station {request.ChargingPointId}",
                        Comment = "Created via Management API"
                    };
                    _dbContext.ChargePoints.Add(chargePoint);
                    _logger.LogInformation($"ChargePoint created: {request.ChargingPointId}");
                }

                // Create ChargingStation in Management layer
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
                return Ok(new ChargingStationResponseDto
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
                    return Ok(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var station = await _dbContext.ChargingStations.FirstOrDefaultAsync(s => s.RecId == request.RecId && s.Active == 1);
                if (station == null)
                {
                    return Ok(new ChargingStationResponseDto
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
                return Ok(new ChargingStationResponseDto
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
                    return Ok(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                station.Active = 0;
                station.UpdatedOn = DateTime.UtcNow;

                // CASCADE: Soft delete all associated charging guns
                var chargingGuns = await _dbContext.ChargingGuns
                    .Where(c => c.ChargingStationId == stationId && c.Active == 1)
                    .ToListAsync();

                foreach (var gun in chargingGuns)
                {
                    gun.Active = 0;
                    gun.UpdatedOn = DateTime.UtcNow;
                }

                // Also soft delete associated connector statuses in OCPP layer
                var connectors = await _dbContext.ConnectorStatuses
                    .Where(c => c.ChargePointId == station.ChargingPointId && c.Active == 1)
                    .ToListAsync();

                foreach (var connector in connectors)
                {
                    connector.Active = 0;
                    connector.LastStatusTime = DateTime.UtcNow;
                }

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
                return Ok(new ChargingStationResponseDto
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
                return Ok(new ChargingStationResponseDto
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
                    return Ok(new ChargingStationResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                var chargePoint = await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == station.ChargingPointId);

                // Get chargers from ChargingGuns table
                var chargers = await _dbContext.ChargingGuns
                    .Where(c => c.ChargingStationId == stationId && c.Active == 1)
                    .ToListAsync();

                var chargerDtos = new List<ChargerDto>();
                foreach (var charger in chargers)
                {
                    chargerDtos.Add(await MapToChargerDtoAsync(charger));
                }

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
                return Ok(new ChargingStationResponseDto
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
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Verify station exists
                var station = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(s => s.RecId == request.ChargingStationId && s.Active == 1);

                if (station == null)
                {
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                // Verify hub exists
                var hub = await _dbContext.ChargingHubs
                    .FirstOrDefaultAsync(h => h.RecId == station.ChargingHubId && h.Active == 1);

                // Verify ChargePoint exists
                var chargePoint = await _dbContext.ChargePoints
                    .FirstOrDefaultAsync(cp => cp.ChargePointId == request.ChargePointId);

                if (chargePoint == null)
                {
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charge point not found"
                    });
                }

                // Check if connector status exists, if not create it in OCPP layer
                var connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(c => c.ChargePointId == request.ChargePointId 
                        && c.ConnectorId.ToString() == request.ConnectorId && c.Active == 1);

                if (connectorStatus == null)
                {
                    // Parse connector ID
                    if (!int.TryParse(request.ConnectorId, out int connectorIdInt))
                    {
                        return Ok(new ChargerResponseDto
                        {
                            Success = false,
                            Message = "Invalid connector ID format"
                        });
                    }

                    connectorStatus = new ConnectorStatus
                    {
                        ChargePointId = request.ChargePointId,
                        ConnectorId = connectorIdInt,
                        ConnectorName = $"Connector {request.ConnectorId}",
                        LastStatus = "Available",
                        Active = 1
                    };
                    _dbContext.ConnectorStatuses.Add(connectorStatus);
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation($"Connector status created: {request.ChargePointId}:{request.ConnectorId}");
                }

                // Check if charger already exists (active only)
                var existing = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(c => c.ChargingStationId == request.ChargingStationId 
                        && c.ConnectorId == request.ConnectorId && c.Active == 1);

                if (existing != null)
                {
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger with this connector ID already exists for this station"
                    });
                }

                var charger = new Database.EVCDTO.ChargingGuns
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargingStationId = request.ChargingStationId,
                    ChargingHubId = station.ChargingHubId,
                    ConnectorId = request.ConnectorId,
                    ChargerTypeId = request.ChargerTypeId,
                    ChargerTariff = request.ChargerTariff,
                    PowerOutput = request.PowerOutput,
                    ChargerStatus = "Available",
                    AdditionalInfo1 = request.AdditionalInfo1,
                    AdditionalInfo2 = request.AdditionalInfo2,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargingGuns.Add(charger);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger added: {charger.RecId} at station {request.ChargingStationId}");

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger added successfully",
                    Charger = await MapToChargerDtoAsync(charger)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding charger");
                return Ok(new ChargerResponseDto
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
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var charger = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(c => c.RecId == request.RecId && c.Active == 1);

                if (charger == null)
                {
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger not found"
                    });
                }

                // Update properties
                if (!string.IsNullOrEmpty(request.ChargerTypeId))
                    charger.ChargerTypeId = request.ChargerTypeId;
                if (!string.IsNullOrEmpty(request.ChargerTariff))
                    charger.ChargerTariff = request.ChargerTariff;
                if (!string.IsNullOrEmpty(request.PowerOutput))
                    charger.PowerOutput = request.PowerOutput;
                if (!string.IsNullOrEmpty(request.ChargerStatus))
                    charger.ChargerStatus = request.ChargerStatus;
                if (!string.IsNullOrEmpty(request.AdditionalInfo1))
                    charger.AdditionalInfo1 = request.AdditionalInfo1;
                if (!string.IsNullOrEmpty(request.AdditionalInfo2))
                    charger.AdditionalInfo2 = request.AdditionalInfo2;

                charger.UpdatedOn = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger updated: {request.RecId}");

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger updated successfully",
                    Charger = await MapToChargerDtoAsync(charger)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charger");
                return Ok(new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while updating charger"
                });
            }
        }

        /// <summary>
        /// Delete charger/gun (connector) - Soft delete
        /// </summary>
        [HttpDelete("chargers-delete/{chargerId}")]
        [Authorize]
        public async Task<IActionResult> DeleteCharger(string chargerId)
        {
            try
            {
                var charger = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(c => c.RecId == chargerId && c.Active == 1);

                if (charger == null)
                {
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger not found"
                    });
                }

                // Soft delete
                charger.Active = 0;
                charger.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charger deleted (soft): {chargerId}");

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger deleted successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting charger");
                return Ok(new ChargerResponseDto
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
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                var chargers = await _dbContext.ChargingGuns
                    .Where(c => c.ChargingStationId == stationId && c.Active == 1)
                    .ToListAsync();

                var chargerDtos = new List<ChargerDto>();
                foreach (var charger in chargers)
                {
                    chargerDtos.Add(await MapToChargerDtoAsync(charger));
                }

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
                return Ok(new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving chargers"
                });
            }
        }

        /// <summary>
        /// Get charger details
        /// </summary>
        [HttpGet("charger-details/{chargerId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetChargerDetails(string chargerId)
        {
            try
            {
                var charger = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(c => c.RecId == chargerId && c.Active == 1);

                if (charger == null)
                {
                    return Ok(new ChargerResponseDto
                    {
                        Success = false,
                        Message = "Charger not found"
                    });
                }

                return Ok(new ChargerResponseDto
                {
                    Success = true,
                    Message = "Charger details retrieved successfully",
                    Charger = await MapToChargerDtoAsync(charger)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charger details");
                return Ok(new ChargerResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charger details"
                });
            }
        }

        #endregion

        #region Comprehensive Listing API

        /// <summary>
        /// Get comprehensive list of charging hubs with stations and chargers
        /// Supports search, filtering, and pagination
        /// </summary>
        [HttpPost("comprehensive-list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComprehensiveList([FromBody] ChargingHubComprehensiveSearchDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingHubComprehensiveResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Get all active hubs
                var hubsQuery = _dbContext.ChargingHubs.Where(h => h.Active == 1);

                // Apply text search filter
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    var searchLower = request.SearchTerm.ToLower();
                    hubsQuery = hubsQuery.Where(h =>
                        h.ChargingHubName.ToLower().Contains(searchLower) ||
                        h.City.ToLower().Contains(searchLower) ||
                        h.State.ToLower().Contains(searchLower) ||
                        h.AddressLine1.ToLower().Contains(searchLower)
                    );
                }

                // Apply city filter
                if (!string.IsNullOrEmpty(request.City))
                {
                    hubsQuery = hubsQuery.Where(h => h.City.ToLower() == request.City.ToLower());
                }

                // Apply state filter
                if (!string.IsNullOrEmpty(request.State))
                {
                    hubsQuery = hubsQuery.Where(h => h.State.ToLower() == request.State.ToLower());
                }

                // Apply pincode filter
                if (!string.IsNullOrEmpty(request.Pincode))
                {
                    hubsQuery = hubsQuery.Where(h => h.Pincode == request.Pincode);
                }

                var allHubs = await hubsQuery.ToListAsync();

                // Build comprehensive hub list with stations and chargers
                var hubList = new List<ChargingHubWithStationsDto>();

                foreach (var hub in allHubs)
                {
                    // Calculate distance if location provided
                    double? distance = null;
                    if (request.Latitude.HasValue && request.Longitude.HasValue &&
                        double.TryParse(hub.Latitude, out var hubLat) &&
                        double.TryParse(hub.Longitude, out var hubLng))
                    {
                        distance = CalculateDistance(
                            request.Latitude.Value,
                            request.Longitude.Value,
                            hubLat,
                            hubLng
                        );

                        // Filter by radius if specified
                        if (request.RadiusKm.HasValue && distance > request.RadiusKm.Value)
                        {
                            continue;
                        }
                    }

                    // Get stations for this hub
                    var stations = await _dbContext.ChargingStations
                        .Where(s => s.ChargingHubId == hub.RecId && s.Active == 1)
                        .ToListAsync();

                    var stationList = new List<ChargingStationWithChargersDto>();
                    int totalChargers = 0;
                    int availableChargers = 0;

                    foreach (var station in stations)
                    {
                        var chargePoint = await _dbContext.ChargePoints
                            .FirstOrDefaultAsync(cp => cp.ChargePointId == station.ChargingPointId);

                        // Get chargers from ChargingGuns table
                        var chargers = await _dbContext.ChargingGuns
                            .Where(c => c.ChargingStationId == station.RecId && c.Active == 1)
                            .ToListAsync();

                        // Filter by charger status if specified
                        if (!string.IsNullOrEmpty(request.ChargerStatus))
                        {
                            chargers = chargers
                                .Where(c => c.ChargerStatus.Equals(request.ChargerStatus, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }

                        var chargerDtos = new List<ChargerDto>();
                        int stationAvailable = 0;
                        
                        foreach (var charger in chargers)
                        {
                            var chargerDto = await MapToChargerDtoAsync(charger);
                            chargerDtos.Add(chargerDto);
                            
                            // Count available based on ChargerStatus or LastStatus from connector
                            if (charger.ChargerStatus?.Equals("Available", StringComparison.OrdinalIgnoreCase) == true ||
                                chargerDto.LastStatus?.Equals("Available", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                stationAvailable++;
                            }
                        }

                        totalChargers += chargers.Count;
                        availableChargers += stationAvailable;

                        stationList.Add(new ChargingStationWithChargersDto
                        {
                            RecId = station.RecId,
                            ChargingPointId = station.ChargingPointId,
                            ChargePointName = chargePoint?.Name,
                            ChargingGunCount = station.ChargingGunCount,
                            ChargingStationImage = station.ChargingStationImage,
                            TotalChargers = chargers.Count,
                            AvailableChargers = stationAvailable,
                            Chargers = chargerDtos
                        });
                    }

                    // Apply available chargers filter
                    if (request.HasAvailableChargers.HasValue && request.HasAvailableChargers.Value)
                    {
                        if (availableChargers == 0)
                        {
                            continue;
                        }
                    }

                    // Get reviews for rating
                    var reviews = await _dbContext.ChargingHubReviews
                        .Where(r => r.ChargingHubId == hub.RecId && r.Active == 1)
                        .ToListAsync();

                    var avgRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0;

                    hubList.Add(new ChargingHubWithStationsDto
                    {
                        RecId = hub.RecId,
                        ChargingHubName = hub.ChargingHubName,
                        AddressLine1 = hub.AddressLine1,
                        City = hub.City,
                        State = hub.State,
                        Pincode = hub.Pincode,
                        Latitude = hub.Latitude,
                        Longitude = hub.Longitude,
                        ChargingHubImage = hub.ChargingHubImage,
                        OpeningTime = hub.OpeningTime.ToString(),
                        ClosingTime = hub.ClosingTime.ToString(),
                        TypeATariff = hub.TypeATariff,
                        TypeBTariff = hub.TypeBTariff,
                        Amenities = hub.Amenities,
                        DistanceKm = distance.HasValue ? Math.Round(distance.Value, 2) : null,
                        AverageRating = Math.Round(avgRating, 1),
                        TotalReviews = reviews.Count,
                        TotalStations = stations.Count,
                        TotalChargers = totalChargers,
                        AvailableChargers = availableChargers,
                        Stations = stationList
                    });
                }

                // Apply sorting
                hubList = ApplySorting(hubList, request.SortBy, request.SortOrder);

                // Get total count before pagination
                var totalCount = hubList.Count;

                // Apply pagination
                var paginatedHubs = hubList
                    .Skip((request.PageNumber - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToList();

                var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

                _logger.LogInformation($"Comprehensive list retrieved: {paginatedHubs.Count} hubs (page {request.PageNumber} of {totalPages})");

                return Ok(new ChargingHubComprehensiveResponseDto
                {
                    Success = true,
                    Message = $"Found {totalCount} charging hub(s) matching criteria",
                    Hubs = paginatedHubs,
                    TotalCount = totalCount,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize,
                    TotalPages = totalPages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving comprehensive list");
                return StatusCode(500, new ChargingHubComprehensiveResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving comprehensive list"
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
                // Get UserId from JWT token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "User not authenticated"
                    });
                }

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
                    UserId = userId,
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

                _logger.LogInformation($"Hub review added: {review.RecId} by User: {userId}");

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
                return Ok(new ReviewResponseDto
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
                // Get UserId from JWT token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "User not authenticated"
                    });
                }

                if (!ModelState.IsValid)
                {
                    return Ok(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                if (string.IsNullOrEmpty(request.ChargingStationId))
                {
                    return Ok(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "ChargingStationId is required"
                    });
                }

                var review = new Database.EVCDTO.ChargingHubReview
                {
                    RecId = Guid.NewGuid().ToString(),
                    UserId = userId,
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

                _logger.LogInformation($"Station review added: {review.RecId} by User: {userId}");

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
                return Ok(new ReviewResponseDto
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
                // Get UserId from JWT token
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "User not authenticated"
                    });
                }

                if (!ModelState.IsValid)
                {
                    return Ok(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var review = await _dbContext.ChargingHubReviews.FirstOrDefaultAsync(r => r.RecId == request.RecId && r.Active == 1);
                if (review == null)
                {
                    return Ok(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Review not found"
                    });
                }

                // Check if user owns this review
                if (review.UserId != userId)
                {
                    _logger.LogWarning($"Unauthorized update attempt on review {request.RecId} by user {userId}");
                    return StatusCode(403, new ReviewResponseDto
                    {
                        Success = false,
                        Message = "You can only edit your own reviews"
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
                return Ok(new ReviewResponseDto
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
                // Get UserId from JWT token
                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "User not authenticated"
                    });
                }

                var review = await _dbContext.ChargingHubReviews.FirstOrDefaultAsync(r => r.RecId == reviewId && r.Active == 1);
                if (review == null)
                {
                    return Ok(new ReviewResponseDto
                    {
                        Success = false,
                        Message = "Review not found"
                    });
                }

                // Check if user owns this review
                if (review.UserId != userId)
                {
                    _logger.LogWarning($"Unauthorized delete attempt on review {reviewId} by user {userId}");
                    return StatusCode(403, new ReviewResponseDto
                    {
                        Success = false,
                        Message = "You can only delete your own reviews"
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
                return Ok(new ReviewResponseDto
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
                return Ok(new ReviewResponseDto
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
                ChargingHubName = hub.ChargingHubName,
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
                ConnectorId = connector.ConnectorId.ToString(),
                ConnectorName = connector.ConnectorName,
                LastStatus = connector.LastStatus,
                LastStatusTime = connector.LastStatusTime,
                LastMeter = connector.LastMeter,
                LastMeterTime = connector.LastMeterTime,
                ChargePointName = chargePointName
            };
        }

        private async Task<ChargerDto> MapToChargerDtoAsync(Database.EVCDTO.ChargingGuns charger)
        {
            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == charger.ChargingStationId);

            var hub = await _dbContext.ChargingHubs
                .FirstOrDefaultAsync(h => h.RecId == charger.ChargingHubId);

            var chargePoint = station != null 
                ? await _dbContext.ChargePoints.FirstOrDefaultAsync(cp => cp.ChargePointId == station.ChargingPointId)
                : null;

            var chargerType = charger.ChargerTypeId != null
                ? await _dbContext.ChargerTypeMasters.FirstOrDefaultAsync(ct => ct.RecId == charger.ChargerTypeId)
                : null;

            // Get connector status from OCPP layer
            ConnectorStatus connectorStatus = null;
            if (station != null && int.TryParse(charger.ConnectorId, out int connectorIdInt))
            {
                connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(cs => cs.ChargePointId == station.ChargingPointId 
                        && cs.ConnectorId == connectorIdInt && cs.Active == 1);
            }

            return new ChargerDto
            {
                RecId = charger.RecId,
                ChargingStationId = charger.ChargingStationId,
                ChargingHubId = charger.ChargingHubId,
                ChargePointId = station?.ChargingPointId,
                ConnectorId = charger.ConnectorId,
                ChargerTypeId = charger.ChargerTypeId,
                ChargerTypeName = chargerType?.ChargerType,
                ChargerTariff = charger.ChargerTariff,
                PowerOutput = charger.PowerOutput,
                ChargerStatus = charger.ChargerStatus,
                ChargerMeterReading = charger.ChargerMeterReading,
                AdditionalInfo1 = charger.AdditionalInfo1,
                AdditionalInfo2 = charger.AdditionalInfo2,
                Active = charger.Active,
                CreatedOn = charger.CreatedOn,
                UpdatedOn = charger.UpdatedOn,
                
                // OCPP Connector Status Info
                ConnectorName = connectorStatus?.ConnectorName,
                LastStatus = connectorStatus?.LastStatus,
                LastStatusTime = connectorStatus?.LastStatusTime,
                LastMeter = connectorStatus?.LastMeter,
                LastMeterTime = connectorStatus?.LastMeterTime,
                
                // Related info
                ChargePointName = chargePoint?.Name,
                ChargingHubName = hub?.ChargingHubName
            };
        }

        private ReviewDto MapToReviewDto(Database.EVCDTO.ChargingHubReview review)
        {
            return new ReviewDto
            {
                RecId = review.RecId,
                UserId = review.UserId,
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

        /// <summary>
        /// Apply sorting to hub list based on criteria
        /// </summary>
        private List<ChargingHubWithStationsDto> ApplySorting(
            List<ChargingHubWithStationsDto> hubs,
            string sortBy,
            string sortOrder)
        {
            var isDescending = sortOrder?.Equals("Desc", StringComparison.OrdinalIgnoreCase) ?? false;

            return sortBy?.ToLower() switch
            {
                "distance" => isDescending
                    ? hubs.OrderByDescending(h => h.DistanceKm ?? double.MaxValue).ToList()
                    : hubs.OrderBy(h => h.DistanceKm ?? double.MaxValue).ToList(),

                "name" => isDescending
                    ? hubs.OrderByDescending(h => h.ChargingHubName).ToList()
                    : hubs.OrderBy(h => h.ChargingHubName).ToList(),

                "rating" => isDescending
                    ? hubs.OrderByDescending(h => h.AverageRating).ToList()
                    : hubs.OrderBy(h => h.AverageRating).ToList(),

                "availablechargers" => isDescending
                    ? hubs.OrderByDescending(h => h.AvailableChargers).ToList()
                    : hubs.OrderBy(h => h.AvailableChargers).ToList(),

                "totalchargers" => isDescending
                    ? hubs.OrderByDescending(h => h.TotalChargers).ToList()
                    : hubs.OrderBy(h => h.TotalChargers).ToList(),

                _ => isDescending
                    ? hubs.OrderByDescending(h => h.DistanceKm ?? double.MaxValue).ToList()
                    : hubs.OrderBy(h => h.DistanceKm ?? double.MaxValue).ToList()
            };
        }

        #endregion
    }
}
