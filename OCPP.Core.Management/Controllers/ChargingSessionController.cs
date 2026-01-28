using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.ChargingSession;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChargingSessionController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ChargingSessionController> _logger;
        private readonly IConfiguration _config;

        public ChargingSessionController(
            OCPPCoreContext dbContext,
            ILogger<ChargingSessionController> logger,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
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
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Get user ID from token or request
                string userId = request.UserId;
                if (string.IsNullOrEmpty(userId))
                {
                    userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                }

                if (string.IsNullOrEmpty(userId))
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "User not authenticated"
                    });
                }

                // Verify user exists and is active
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == userId && u.Active == 1);
                if (user == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "User not found or inactive"
                    });
                }

                // Verify charging station exists
                var chargingStation = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(cs => cs.RecId == request.ChargingStationId && cs.Active == 1);

                if (chargingStation == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found or inactive"
                    });
                }

                // Verify charge point exists
                var chargePoint = await _dbContext.ChargePoints
                    .FirstOrDefaultAsync(cp => cp.ChargePointId == chargingStation.ChargingPointId);

                if (chargePoint == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charge point not found"
                    });
                }

                // Check if there's already an active session for this charging gun
                var existingSession = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.ChargingGunId == request.ChargingGunId && 
                                             s.Active == 1 && 
                                             s.EndTime == DateTime.MinValue);

                if (existingSession != null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging gun is already in use"
                    });
                }

                // Call OCPP server to start transaction
                var ocppResult = await CallOCPPStartTransaction(
                    chargePoint.ChargePointId, 
                    request.ConnectorId, 
                    request.ChargeTagId);

                if (!ocppResult.Success)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = ocppResult.Message
                    });
                }

                // Wait briefly for transaction to be recorded in database
                await Task.Delay(1000);

                // Get the actual OCPP transaction that was just created
                var ocppTransaction = await _dbContext.Transactions
                    .Where(t => t.ChargePointId == chargePoint.ChargePointId && 
                               t.ConnectorId == request.ConnectorId &&
                               t.StartTagId == request.ChargeTagId)
                    .OrderByDescending(t => t.TransactionId)
                    .FirstOrDefaultAsync();

                int? transactionId = ocppTransaction?.TransactionId;
                double meterStart = ocppTransaction?.MeterStart ?? 0;
                DateTime startTime = ocppTransaction?.StartTime ?? DateTime.UtcNow;

                // Create new charging session in database
                var session = new Database.EVCDTO.ChargingSession
                {
                    RecId = Guid.NewGuid().ToString(),
                    UserId = userId,
                    TransactionId = transactionId,
                    ChargingGunId = request.ConnectorId.ToString(),
                    ChargingStationID = request.ChargingStationId,
                    StartMeterReading = meterStart.ToString("F2"),
                    EndMeterReading = "0",
                    EnergyTransmitted = "0",
                    StartTime = startTime,
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

                _logger.LogInformation($"Charging session started: {session.RecId} (Transaction: {transactionId}) for user {userId} at station {request.ChargingStationId}");

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = $"Charging session started successfully. OCPP Status: {ocppResult.Message}",
                    Data = new
                    {
                        Session = await MapToChargingSessionDto(session),
                        TransactionId = transactionId,
                        MeterStart = meterStart,
                        Tariff = session.ChargingTariff
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting charging session");
                return Ok(new ChargingSessionResponseDto
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
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var session = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.RecId == request.SessionId && s.Active == 1);

                if (session == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging session not found or already ended"
                    });
                }

                if (session.EndTime != DateTime.MinValue)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging session already ended"
                    });
                }

                // Verify user exists
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.RecId == session.UserId && u.Active == 1);
                if (user == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "User not found or inactive"
                    });
                }

                // Get charging station and charge point details
                var chargingStation = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(cs => cs.RecId == session.ChargingStationID);

                if (chargingStation == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found"
                    });
                }

                var chargePoint = await _dbContext.ChargePoints
                    .FirstOrDefaultAsync(cp => cp.ChargePointId == chargingStation.ChargingPointId);

                if (chargePoint == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charge point not found"
                    });
                }

                // Extract connector ID from charging gun ID
                int connectorId = 1;
                if (int.TryParse(session.ChargingGunId, out int extractedId))
                {
                    connectorId = extractedId;
                }

                // Call OCPP server to stop transaction
                var ocppResult = await CallOCPPStopTransaction(
                    chargePoint.ChargePointId, 
                    connectorId);

                if (!ocppResult.Success)
                {
                    _logger.LogWarning($"OCPP stop transaction failed: {ocppResult.Message}. Continuing with database update.");
                }

                // Wait for transaction to be updated in database
                await Task.Delay(1000);

                // Get actual meter readings from OCPP Transaction table
                double startReading = 0;
                double endReading = 0;
                DateTime actualStartTime = session.StartTime;
                DateTime actualEndTime = DateTime.UtcNow;

                if (session.TransactionId.HasValue)
                {
                    var transaction = await _dbContext.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == session.TransactionId.Value);

                    if (transaction != null)
                    {
                        startReading = transaction.MeterStart;
                        endReading = transaction.MeterStop ?? endReading;
                        actualStartTime = transaction.StartTime;
                        actualEndTime = transaction.StopTime ?? DateTime.UtcNow;

                        _logger.LogInformation($"Using OCPP transaction data: Start={startReading}, Stop={endReading}");
                    }
                    else
                    {
                        // Fallback to manual readings if transaction not found
                        if (double.TryParse(session.StartMeterReading, out double sessionStart))
                        {
                            startReading = sessionStart;
                        }
                        if (double.TryParse(request.EndMeterReading, out double manualEnd))
                        {
                            endReading = manualEnd;
                        }
                        _logger.LogWarning($"Transaction {session.TransactionId} not found, using fallback values");
                    }
                }
                else
                {
                    // No transaction ID, use manual readings
                    if (double.TryParse(session.StartMeterReading, out double sessionStart))
                    {
                        startReading = sessionStart;
                    }
                    if (double.TryParse(request.EndMeterReading, out double manualEnd))
                    {
                        endReading = manualEnd;
                    }
                    _logger.LogWarning($"No TransactionId found, using manual meter readings");
                }

                // Update session with end details
                session.EndTime = actualEndTime;
                session.EndMeterReading = endReading.ToString("F2");
                session.StartMeterReading = startReading.ToString("F2");

                decimal totalFee = 0;
                double energyTransmitted = Math.Max(0, endReading - startReading);

                session.EnergyTransmitted = energyTransmitted.ToString("F2");

                // Calculate total fee based on tariff and energy
                if (double.TryParse(session.ChargingTariff, out double tariff))
                {
                    totalFee = (decimal)(energyTransmitted * tariff);
                    session.ChargingTotalFee = totalFee.ToString("F2");
                }

                // Calculate charging speed (kW)
                var duration = actualEndTime - actualStartTime;
                if (duration.TotalHours > 0)
                {
                    double chargingSpeed = energyTransmitted / duration.TotalHours;
                    session.ChargingSpeed = chargingSpeed.ToString("F2");
                }

                // Get current wallet balance
                var lastTransaction = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == session.UserId && w.Active == 1)
                    .OrderByDescending(w => w.CreatedOn)
                    .FirstOrDefaultAsync();

                decimal previousBalance = 0;
                if (lastTransaction != null && decimal.TryParse(lastTransaction.CurrentCreditBalance, out var lastBalance))
                {
                    previousBalance = lastBalance;
                }

                // Check if user has sufficient balance
                if (previousBalance < totalFee)
                {
                    _logger.LogWarning($"Insufficient wallet balance for user {session.UserId}. Required: {totalFee}, Available: {previousBalance}");
                    
                    // Still update the session
                    session.UpdatedOn = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();

                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = $"Insufficient wallet balance. Required: {totalFee:F2}, Available: {previousBalance:F2}. Please recharge your wallet.",
                        Data = new
                        {
                            Session = await MapToChargingSessionDto(session),
                            EnergyConsumed = energyTransmitted,
                            Cost = totalFee,
                            MeterStart = startReading,
                            MeterStop = endReading,
                            Duration = duration.TotalMinutes
                        }
                    });
                }

                decimal newBalance = previousBalance - totalFee;

                // Create wallet transaction log for charging payment
                var walletTransaction = new Database.EVCDTO.WalletTransactionLog
                {
                    RecId = Guid.NewGuid().ToString(),
                    UserId = session.UserId,
                    PreviousCreditBalance = previousBalance.ToString("F2"),
                    CurrentCreditBalance = newBalance.ToString("F2"),
                    TransactionType = "Debit",
                    ChargingSessionId = session.RecId,
                    AdditionalInfo1 = $"Charging at {chargingStation.ChargingPointId} (OCPP Txn: {session.TransactionId})",
                    AdditionalInfo2 = $"Energy: {energyTransmitted:F2} kWh @ {session.ChargingTariff}/kWh = ${totalFee:F2}",
                    AdditionalInfo3 = $"Meter: {startReading:F2} → {endReading:F2} | Duration: {duration.TotalMinutes:F0}min",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.WalletTransactionLogs.Add(walletTransaction);
                session.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging session ended: {session.RecId} (Txn: {session.TransactionId}). Energy: {energyTransmitted:F2}kWh, Fee: ${totalFee:F2}, Balance: ${newBalance:F2}");

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = $"Charging session ended successfully. ${totalFee:F2} debited. OCPP: {ocppResult.Message}",
                    Data = new
                    {
                        Session = await MapToChargingSessionDto(session),
                        TransactionId = session.TransactionId,
                        EnergyConsumed = energyTransmitted,
                        Cost = totalFee,
                        MeterStart = startReading,
                        MeterStop = endReading,
                        Duration = duration.TotalMinutes,
                        ChargingSpeed = session.ChargingSpeed,
                        WalletTransaction = new
                        {
                            TransactionId = walletTransaction.RecId,
                            PreviousBalance = previousBalance,
                            AmountDebited = totalFee,
                            NewBalance = newBalance
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ending charging session");
                return Ok(new ChargingSessionResponseDto
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
                return Ok(new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving charging gun status"
                });
            }
        }

        /// <summary>
        /// Get comprehensive charging session details with real-time data for active sessions
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
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging session not found"
                    });
                }

                var sessionDto = await MapToChargingSessionDto(session);
                
                // Initialize variables for real-time data
                double meterStart = 0;
                double meterCurrent = 0;
                double energyConsumed = 0;
                double estimatedCost = 0;
                double chargingSpeed = 0;
                string status = "Unknown";
                DateTime actualStartTime = session.StartTime;
                DateTime? actualEndTime = session.EndTime == DateTime.MinValue ? null : session.EndTime;

                // Get real-time data from OCPP transaction if available
                if (session.TransactionId.HasValue)
                {
                    var transaction = await _dbContext.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == session.TransactionId.Value);

                    if (transaction != null)
                    {
                        meterStart = transaction.MeterStart;
                        meterCurrent = transaction.MeterStop ?? transaction.MeterStart;
                        actualStartTime = transaction.StartTime;
                        
                        if (transaction.StopTime.HasValue)
                        {
                            actualEndTime = transaction.StopTime.Value;
                            status = "Completed";
                        }
                        else
                        {
                            status = session.EndTime == DateTime.MinValue ? "Charging" : "Completed";
                        }

                        energyConsumed = Math.Max(0, meterCurrent - meterStart);

                        if (double.TryParse(session.ChargingTariff, out double tariff))
                        {
                            estimatedCost = energyConsumed * tariff;
                        }

                        var elapsedTime = (actualEndTime ?? DateTime.UtcNow) - actualStartTime;
                        if (elapsedTime.TotalHours > 0)
                        {
                            chargingSpeed = energyConsumed / elapsedTime.TotalHours;
                        }

                        if (session.EndTime == DateTime.MinValue)
                        {
                            _logger.LogInformation($"Real-time update for session {sessionId}: {energyConsumed:F2} kWh consumed, ${estimatedCost:F2} estimated");
                        }
                    }
                    else
                    {
                        // Transaction not found, use session data
                        if (double.TryParse(session.StartMeterReading, out meterStart))
                        {
                            meterCurrent = meterStart;
                        }
                        if (double.TryParse(session.EndMeterReading, out double endReading))
                        {
                            meterCurrent = endReading;
                            energyConsumed = Math.Max(0, endReading - meterStart);
                        }
                        status = session.EndTime == DateTime.MinValue ? "Pending" : "Completed";
                    }
                }
                else
                {
                    // No transaction ID, use session data
                    if (double.TryParse(session.StartMeterReading, out meterStart))
                    {
                        meterCurrent = meterStart;
                    }
                    if (double.TryParse(session.EndMeterReading, out double endReading) && endReading > 0)
                    {
                        meterCurrent = endReading;
                        energyConsumed = Math.Max(0, endReading - meterStart);
                        
                        if (double.TryParse(session.ChargingTariff, out double tariff))
                        {
                            estimatedCost = energyConsumed * tariff;
                        }
                    }
                    status = session.EndTime == DateTime.MinValue ? "Pending" : "Completed";
                }

                var duration = (actualEndTime ?? DateTime.UtcNow) - actualStartTime;
                bool isActive = session.EndTime == DateTime.MinValue;

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Charging session details retrieved successfully",
                    Data = new
                    {
                        Session = sessionDto,
                        TransactionId = session.TransactionId,
                        Status = status,
                        IsActive = isActive,
                        MeterStart = Math.Round(meterStart, 2),
                        MeterCurrent = Math.Round(meterCurrent, 2),
                        EnergyConsumed = Math.Round(energyConsumed, 2),
                        EstimatedCost = Math.Round(estimatedCost, 2),
                        ChargingSpeed = Math.Round(chargingSpeed, 2),
                        Tariff = session.ChargingTariff,
                        Duration = new
                        {
                            TotalMinutes = Math.Round(duration.TotalMinutes, 0),
                            Hours = duration.Hours,
                            Minutes = duration.Minutes,
                            TotalHours = Math.Round(duration.TotalHours, 2)
                        },
                        StartTime = actualStartTime,
                        EndTime = actualEndTime,
                        LastUpdate = DateTime.UtcNow
                    }
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
        /// Unlock a charging connector
        /// </summary>
        [HttpPost("unlock-connector")]
        [Authorize]
        public async Task<IActionResult> UnlockConnector([FromBody] UnlockConnectorRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new ChargingSessionResponseDto
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
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charging station not found or inactive"
                    });
                }

                // Verify charge point exists
                var chargePoint = await _dbContext.ChargePoints
                    .FirstOrDefaultAsync(cp => cp.ChargePointId == chargingStation.ChargingPointId);

                if (chargePoint == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Charge point not found"
                    });
                }

                var stuckSessions = await _dbContext.ChargingSessions
    .Where(s => s.ChargingGunId == request.ConnectorId.ToString() &&
               s.ChargingStationID == request.ChargingStationId &&
               s.Active == 1 &&
               s.EndTime == DateTime.MinValue)
    .ToListAsync();

                var cleanedSessions = new System.Collections.Generic.List<string>();
                var warnings = new System.Collections.Generic.List<string>();

                // Clean up stuck sessions before unlocking
                if (stuckSessions.Any())
                {
                    _logger.LogWarning($"Found {stuckSessions.Count} stuck session(s) for connector {request.ConnectorId} at station {request.ChargingStationId}");

                    foreach (var stuckSession in stuckSessions)
                    {
                        try
                        {
                            // Mark session as force-ended
                            stuckSession.EndTime = DateTime.UtcNow;
                            stuckSession.UpdatedOn = DateTime.UtcNow;

                            // Try to get final meter reading from transaction if available
                            if (stuckSession.TransactionId.HasValue)
                            {
                                var transaction = await _dbContext.Transactions
                                    .FirstOrDefaultAsync(t => t.TransactionId == stuckSession.TransactionId.Value);

                                if (transaction != null)
                                {
                                    double meterStop = transaction.MeterStop ?? transaction.MeterStart;
                                    stuckSession.EndMeterReading = meterStop.ToString("F2");

                                    double energyConsumed = Math.Max(0, meterStop - transaction.MeterStart);
                                    stuckSession.EnergyTransmitted = energyConsumed.ToString("F2");

                                    if (double.TryParse(stuckSession.ChargingTariff, out double tariff))
                                    {
                                        decimal fee = (decimal)(energyConsumed * tariff);
                                        stuckSession.ChargingTotalFee = fee.ToString("F2");
                                    }
                                }
                            }

                            cleanedSessions.Add(stuckSession.RecId);
                            _logger.LogInformation($"Force-ended stuck session: {stuckSession.RecId}");

                            warnings.Add($"Session {stuckSession.RecId} was automatically ended");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error cleaning up stuck session {stuckSession.RecId}");
                            warnings.Add($"Failed to clean session {stuckSession.RecId}: {ex.Message}");
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                }

                // Call OCPP server to unlock connector
                var ocppResult = await CallOCPPUnlockConnector(
                    chargePoint.ChargePointId,
                    request.ConnectorId);

                if (!ocppResult.Success)
                {
                    return Ok(new ChargingSessionResponseDto  // Changed from Ok to BadRequest
                    {
                        Success = false,
                        Message = ocppResult.Message,
                        Data = new
                        {
                            CleanedSessions = cleanedSessions,
                            Warnings = warnings
                        }
                    });
                }

                _logger.LogInformation($"Connector unlocked: Station {request.ChargingStationId}, Connector {request.ConnectorId}");

                var responseMessage = ocppResult.Message;
                if (cleanedSessions.Any())
                {
                    responseMessage += $". {cleanedSessions.Count} stuck session(s) automatically cleared.";
                }

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = responseMessage,
                    Data = new
                    {
                        ChargingStationId = request.ChargingStationId,
                        ConnectorId = request.ConnectorId,
                        ChargePointId = chargePoint.ChargePointId,
                        Status = "Unlocked",
                        CleanedSessions = cleanedSessions,
                        SessionsCleared = cleanedSessions.Count,
                        Warnings = warnings
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking connector");
                return Ok(new ChargingSessionResponseDto  // Changed from Ok to StatusCode
                {
                    Success = false,
                    Message = "An error occurred while unlocking the connector"
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
                return Ok(new ChargingSessionResponseDto
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

        private async Task<(bool Success, string Message)> CallOCPPStartTransaction(string chargePointId, int connectorId, string chargeTagId)
        {
            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            string apiKeyConfig = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
            {
                _logger.LogWarning("CallOCPPStartTransaction: ServerApiUrl not configured");
                return (false, "OCPP server URL not configured");
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += "/";
                    }
                    Uri uri = new Uri(serverApiUrl);
                    uri = new Uri(uri, $"StartTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}/{Uri.EscapeDataString(chargeTagId)}");
                    httpClient.Timeout = new TimeSpan(0, 0, 15);

                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    {
                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                    }

                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                    
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(jsonResult))
                        {
                            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                            string status = jsonObject.status;
                            
                            _logger.LogInformation($"OCPP StartTransaction result: {status}");
                            
                            switch (status)
                            {
                                case "Accepted":
                                    return (true, "Transaction accepted by charge point");
                                case "Rejected":
                                    return (false, "Transaction rejected by charge point");
                                case "Timeout":
                                    return (false, "Charge point did not respond in time");
                                default:
                                    return (false, $"Unknown status: {status}");
                            }
                        }
                        return (false, "Empty response from OCPP server");
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return (false, "Charge point is offline");
                    }
                    else
                    {
                        _logger.LogError($"OCPP API returned status: {response.StatusCode}");
                        return (false, "OCPP server returned error");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OCPP StartTransaction API");
                return (false, $"Error communicating with OCPP server: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> CallOCPPStopTransaction(string chargePointId, int connectorId)
        {
            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            string apiKeyConfig = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
            {
                _logger.LogWarning("CallOCPPStopTransaction: ServerApiUrl not configured");
                return (false, "OCPP server URL not configured");
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += "/";
                    }
                    Uri uri = new Uri(serverApiUrl);
                    uri = new Uri(uri, $"StopTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}");
                    httpClient.Timeout = new TimeSpan(0, 0, 15);

                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    {
                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                    }

                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                    
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(jsonResult))
                        {
                            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                            string status = jsonObject.status;
                            
                            _logger.LogInformation($"OCPP StopTransaction result: {status}");
                            
                            switch (status)
                            {
                                case "Accepted":
                                    return (true, "Stop transaction accepted by charge point");
                                case "Rejected":
                                    return (false, "Stop transaction rejected by charge point");
                                case "Timeout":
                                    return (false, "Charge point did not respond in time");
                                default:
                                    return (false, $"Unknown status: {status}");
                            }
                        }
                        return (false, "Empty response from OCPP server");
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return (false, "Charge point is offline");
                    }
                    else
                    {
                        _logger.LogError($"OCPP API returned status: {response.StatusCode}");
                        return (false, "OCPP server returned error");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OCPP StopTransaction API");
                return (false, $"Error communicating with OCPP server: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Message)> CallOCPPUnlockConnector(string chargePointId, int connectorId)
        {
            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            string apiKeyConfig = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
            {
                _logger.LogWarning("CallOCPPUnlockConnector: ServerApiUrl not configured");
                return (false, "OCPP server URL not configured");
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += "/";
                    }
                    Uri uri = new Uri(serverApiUrl);
                    uri = new Uri(uri, $"UnlockConnector/{Uri.EscapeDataString(chargePointId)}/{connectorId}");
                    httpClient.Timeout = new TimeSpan(0, 0, 15);

                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    {
                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                    }

                    HttpResponseMessage response = await httpClient.GetAsync(uri);
                    
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(jsonResult))
                        {
                            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                            string status = jsonObject.status;
                            
                            _logger.LogInformation($"OCPP UnlockConnector result: {status}");
                            
                            switch (status)
                            {
                                case "Unlocked":
                                    return (true, "Connector unlocked successfully");
                                case "UnlockFailed":
                                    return (false, "Failed to unlock connector");
                                case "OngoingAuthorizedTransaction":
                                    return (false, "Cannot unlock - ongoing authorized transaction");
                                case "UnknownConnector":
                                    return (false, "Unknown connector");
                                case "NotSupported":
                                    return (false, "Unlock not supported by charge point");
                                case "Timeout":
                                    return (false, "Charge point did not respond in time");
                                default:
                                    return (false, $"Unknown status: {status}");
                            }
                        }
                        return (false, "Empty response from OCPP server");
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return (false, "Charge point is offline");
                    }
                    else
                    {
                        _logger.LogError($"OCPP API returned status: {response.StatusCode}");
                        return (false, "OCPP server returned error");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OCPP UnlockConnector API");
                return (false, $"Error communicating with OCPP server: {ex.Message}");
            }
        }

        #endregion
    }
}
