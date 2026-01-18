using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.ChargingSession;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChargingSessionController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ChargingSessionController> _logger;

        public ChargingSessionController(
            OCPPCoreContext dbContext,
            ILogger<ChargingSessionController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Start a new charging session
        /// </summary>
        [HttpPost("start-charging-session")]
        [Authorize]
        public async Task<IActionResult> StartChargingSession([FromBody] StartChargingSessionRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Verify charging station exists
                var chargingStation = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(cs => cs.RecId == request.ChargingStationId && cs.Active == 1);

                if (chargingStation == null)
                {
                    return NotFound(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found or inactive"
                    });
                }

                // Check if there's already an active session for this charging gun
                var existingSession = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.ChargingGunId == request.ChargingGunId && 
                                             s.Active == 1 && 
                                             s.EndTime == DateTime.MinValue);

                if (existingSession != null)
                {
                    return BadRequest(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging gun is already in use"
                    });
                }

                // Create new charging session
                var session = new Database.EVCDTO.ChargingSession
                {
                    RecId = Guid.NewGuid().ToString(),
                    ChargingGunId = request.ChargingGunId,
                    ChargingStationID = request.ChargingStationId,
                    StartMeterReading = request.StartMeterReading,
                    EndMeterReading = "0",
                    EnergyTransmitted = "0",
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.MinValue,
                    ChargingSpeed = "0",
                    ChargingTariff = request.ChargingTariff ?? "0",
                    ChargingTotalFee = "0",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ChargingSessions.Add(session);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging session started: {session.RecId} at station {request.ChargingStationId}");

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Charging session started successfully",
                    Data = await MapToChargingSessionDto(session)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting charging session");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while starting the charging session"
                });
            }
        }

        /// <summary>
        /// End an active charging session
        /// </summary>
        [HttpPost("end-charging-session")]
        [Authorize]
        public async Task<IActionResult> EndChargingSession([FromBody] EndChargingSessionRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var session = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.RecId == request.SessionId && s.Active == 1);

                if (session == null)
                {
                    return NotFound(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging session not found or already ended"
                    });
                }

                if (session.EndTime != DateTime.MinValue)
                {
                    return BadRequest(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging session already ended"
                    });
                }

                // Update session with end details
                session.EndTime = DateTime.UtcNow;
                session.EndMeterReading = request.EndMeterReading;

                // Calculate energy transmitted
                if (double.TryParse(session.StartMeterReading, out double startReading) &&
                    double.TryParse(request.EndMeterReading, out double endReading))
                {
                    double energyTransmitted = endReading - startReading;
                    session.EnergyTransmitted = energyTransmitted.ToString("F2");

                    // Calculate total fee based on tariff and energy
                    if (double.TryParse(session.ChargingTariff, out double tariff))
                    {
                        double totalFee = energyTransmitted * tariff;
                        session.ChargingTotalFee = totalFee.ToString("F2");
                    }

                    // Calculate charging speed (kW)
                    var duration = session.EndTime - session.StartTime;
                    if (duration.TotalHours > 0)
                    {
                        double chargingSpeed = energyTransmitted / duration.TotalHours;
                        session.ChargingSpeed = chargingSpeed.ToString("F2");
                    }
                }

                session.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging session ended: {session.RecId}");

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Charging session ended successfully",
                    Data = await MapToChargingSessionDto(session)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending charging session");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while ending the charging session"
                });
            }
        }

        /// <summary>
        /// Get charging gun status
        /// </summary>
        [HttpGet("charging-gun-status/{chargingGunId}")]
        public async Task<IActionResult> GetChargingGunStatus(string chargingGunId)
        {
            try
            {
                // Find active session for this gun
                var activeSession = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.ChargingGunId == chargingGunId && 
                                             s.Active == 1 && 
                                             s.EndTime == DateTime.MinValue);

                // Get charging station info
                Database.EVCDTO.ChargingStation chargingStation = null;
                if (activeSession != null)
                {
                    chargingStation = await _dbContext.ChargingStations
                        .FirstOrDefaultAsync(cs => cs.RecId == activeSession.ChargingStationID);
                }

                var status = new ChargingGunStatusDto
                {
                    ChargingGunId = chargingGunId,
                    ChargingStationId = chargingStation?.RecId,
                    ChargingStationName = chargingStation?.ChargingPointId,
                    Status = activeSession != null ? "In Use" : "Available",
                    CurrentSessionId = activeSession?.RecId,
                    LastStatusUpdate = activeSession?.UpdatedOn ?? DateTime.UtcNow,
                    IsAvailable = activeSession == null
                };

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Charging gun status retrieved successfully",
                    Data = status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging gun status");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging gun status"
                });
            }
        }

        /// <summary>
        /// Get charging session details
        /// </summary>
        [HttpGet("charging-session-details/{sessionId}")]
        [Authorize]
        public async Task<IActionResult> GetChargingSessionDetails(string sessionId)
        {
            try
            {
                var session = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.RecId == sessionId);

                if (session == null)
                {
                    return NotFound(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging session not found"
                    });
                }

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Charging session details retrieved successfully",
                    Data = await MapToChargingSessionDto(session)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging session details");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging session details"
                });
            }
        }

        /// <summary>
        /// Get all charging sessions (with optional filters)
        /// </summary>
        [HttpGet("charging-sessions")]
        [Authorize]
        public async Task<IActionResult> GetChargingSessions(
            [FromQuery] string stationId = null,
            [FromQuery] string status = null,
            [FromQuery] int pageSize = 50,
            [FromQuery] int page = 1)
        {
            try
            {
                var query = _dbContext.ChargingSessions.Where(s => s.Active == 1);

                if (!string.IsNullOrEmpty(stationId))
                {
                    query = query.Where(s => s.ChargingStationID == stationId);
                }

                if (!string.IsNullOrEmpty(status))
                {
                    if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(s => s.EndTime == DateTime.MinValue);
                    }
                    else if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                    {
                        query = query.Where(s => s.EndTime != DateTime.MinValue);
                    }
                }

                var totalRecords = await query.CountAsync();
                var sessions = await query
                    .OrderByDescending(s => s.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var sessionDtos = new System.Collections.Generic.List<ChargingSessionDto>();
                foreach (var session in sessions)
                {
                    sessionDtos.Add(await MapToChargingSessionDto(session));
                }

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Charging sessions retrieved successfully",
                    Data = new
                    {
                        TotalRecords = totalRecords,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                        Sessions = sessionDtos
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charging sessions");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging sessions"
                });
            }
        }

        #region Helper Methods

        private async Task<ChargingSessionDto> MapToChargingSessionDto(Database.EVCDTO.ChargingSession session)
        {
            var chargingStation = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(cs => cs.RecId == session.ChargingStationID);

            string chargingHubName = null;
            if (chargingStation != null)
            {
                var chargingHub = await _dbContext.ChargingHubs
                    .FirstOrDefaultAsync(ch => ch.RecId == chargingStation.ChargingHubId);
                chargingHubName = chargingHub?.City;
            }

            var isActive = session.EndTime == DateTime.MinValue;
            var endTime = session.EndTime == DateTime.MinValue ? (DateTime?)null : session.EndTime;
            var duration = isActive 
                ? DateTime.UtcNow - session.StartTime 
                : (endTime.HasValue ? endTime.Value - session.StartTime : TimeSpan.Zero);

            return new ChargingSessionDto
            {
                RecId = session.RecId,
                ChargingGunId = session.ChargingGunId,
                ChargingStationId = session.ChargingStationID,
                ChargingStationName = chargingStation?.ChargingPointId,
                ChargingHubName = chargingHubName,
                StartMeterReading = session.StartMeterReading,
                EndMeterReading = session.EndMeterReading,
                EnergyTransmitted = session.EnergyTransmitted,
                StartTime = session.StartTime,
                EndTime = endTime,
                ChargingSpeed = session.ChargingSpeed,
                ChargingTariff = session.ChargingTariff,
                ChargingTotalFee = session.ChargingTotalFee,
                Status = isActive ? "Active" : "Completed",
                Duration = duration,
                Active = session.Active,
                CreatedOn = session.CreatedOn,
                UpdatedOn = session.UpdatedOn
            };
        }

        #endregion
    }
}
