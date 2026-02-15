using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Models.ChargingHub;
using OCPP.Core.Management.Models.ChargingSession;
using System;
using System.Collections.Generic;
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

                // Log user wallet balance for information
                var lastWalletTransaction = await _dbContext.WalletTransactionLogs
                    .Where(w => w.UserId == userId && w.Active == 1)
                    .OrderByDescending(w => w.CreatedOn)
                    .FirstOrDefaultAsync();

                decimal currentBalance = 0;
                if (lastWalletTransaction != null && decimal.TryParse(lastWalletTransaction.CurrentCreditBalance, out var balance))
                {
                    currentBalance = balance;
                }

                _logger.LogInformation($"User {userId} starting session with balance: ₹{currentBalance:F2}");

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

                // Get charging gun details to fetch the correct tariff
                var chargingGun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.ChargingStationId == chargingStation.RecId
                        && g.ConnectorId == request.ConnectorId.ToString() && g.Active == 1);

                string tariffToUse = request.ChargingTariff ?? "0";

                if (chargingGun != null && !string.IsNullOrEmpty(chargingGun.ChargerTariff))
                {
                    tariffToUse = chargingGun.ChargerTariff;
                    _logger.LogInformation($"Using tariff from charging gun: {tariffToUse} ₹/kWh");
                }
                else if (!string.IsNullOrEmpty(request.ChargingTariff))
                {
                    _logger.LogInformation($"Using tariff from request: {tariffToUse} ₹/kWh");
                }
                else
                {
                    _logger.LogWarning($"No tariff found for charging gun. Using default: 0");
                }

                // Parse tariff for calculations
                double tariff = 0;
                double.TryParse(tariffToUse, out tariff);

                // Calculate minimum required balance based on session limits
                decimal minBalanceRequired = 100m; // Default minimum
                string balanceRequirementSource = "default minimum";
                var estimatedCosts = new List<(string source, decimal amount)>();

                // 1. Cost Limit - direct cost limit
                if (request.CostLimit.HasValue && request.CostLimit.Value > 0)
                {
                    estimatedCosts.Add(("Cost Limit", (decimal)request.CostLimit.Value));
                }

                // 2. Energy Limit - energy × tariff
                if (request.EnergyLimit.HasValue && request.EnergyLimit.Value > 0 && tariff > 0)
                {
                    decimal energyCost = (decimal)(request.EnergyLimit.Value * tariff);
                    estimatedCosts.Add(("Energy Limit", energyCost));
                }

                // 3. Time Limit - estimate based on average charging speed
                if (request.TimeLimit.HasValue && request.TimeLimit.Value > 0 && tariff > 0)
                {
                    // Get power output from charging gun to estimate energy consumption
                    double estimatedPowerKw = 7.0; // Default 7 kW if not available
                    if (chargingGun != null && !string.IsNullOrEmpty(chargingGun.PowerOutput) &&
                        double.TryParse(chargingGun.PowerOutput, out double gunPower))
                    {
                        estimatedPowerKw = gunPower;
                    }

                    // Calculate estimated energy: Power (kW) × Time (hours) = Energy (kWh)
                    double timeInHours = request.TimeLimit.Value / 60.0;
                    double estimatedEnergy = estimatedPowerKw * timeInHours;
                    decimal timeCost = (decimal)(estimatedEnergy * tariff);
                    estimatedCosts.Add(("Time Limit", timeCost));
                }

                // 4. Battery Increase Limit - just use 100 as specified
                if (request.BatteryIncreaseLimit.HasValue && request.BatteryIncreaseLimit.Value > 0)
                {
                    estimatedCosts.Add(("Battery Limit", 100m));
                }

                // Determine the maximum estimated cost
                if (estimatedCosts.Count > 0)
                {
                    var maxCost = estimatedCosts.OrderByDescending(x => x.amount).First();
                    minBalanceRequired = maxCost.amount;
                    balanceRequirementSource = maxCost.source;
                    _logger.LogInformation($"Calculated minimum balance: ₹{minBalanceRequired:F2} based on {balanceRequirementSource}. All estimates: {string.Join(", ", estimatedCosts.Select(x => $"{x.source}=₹{x.amount:F2}"))}");
                }
                else
                {
                    _logger.LogInformation($"No limits specified. Using default minimum balance: ₹{minBalanceRequired:F2}");
                }

                // Check if user balance is sufficient (block negative balance or insufficient funds)
                if (currentBalance < 0)
                {
                    _logger.LogWarning($"User {userId} has negative balance: ₹{currentBalance:F2}");
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = $"Cannot start session. Your wallet balance is negative (₹{currentBalance:F2}). Please recharge your wallet to continue."
                    });
                }

                if (currentBalance < minBalanceRequired)
                {
                    _logger.LogWarning($"User {userId} has insufficient balance for session. Balance: ₹{currentBalance:F2}, Required: ₹{minBalanceRequired:F2} (based on {balanceRequirementSource})");
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = $"Insufficient wallet balance. Minimum ₹{minBalanceRequired:F2} required to start charging (based on {balanceRequirementSource}). Your current balance: ₹{currentBalance:F2}. Please recharge your wallet."
                    });
                }

                _logger.LogInformation($"User {userId} balance check passed. Balance: ₹{currentBalance:F2}, Required: ₹{minBalanceRequired:F2}");

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
                double meterStart = 0;
                DateTime startTime = DateTime.UtcNow;
                string meterSource = "Unknown";

                // Priority 1: Use transaction meter start (most authoritative)
                if (ocppTransaction != null)
                {
                    meterStart = ocppTransaction.MeterStart;
                    startTime = ocppTransaction.StartTime;
                    meterSource = "OCPP Transaction";
                    _logger.LogInformation($"Using meter start from OCPP transaction: {meterStart:F3} kWh");
                }
                else
                {
                    // Priority 2: Fallback to connector real-time meter
                    var connectorStatus = await _dbContext.ConnectorStatuses
                        .FirstOrDefaultAsync(cs => cs.ChargePointId == chargePoint.ChargePointId
                            && cs.ConnectorId == request.ConnectorId && cs.Active == 1);

                    if (connectorStatus?.LastMeter.HasValue == true)
                    {
                        meterStart = connectorStatus.LastMeter.Value;
                        if (connectorStatus.LastMeterTime.HasValue)
                        {
                            startTime = connectorStatus.LastMeterTime.Value;
                        }
                        meterSource = "Connector Real-time";
                        _logger.LogWarning($"Transaction not found. Using connector meter: {meterStart:F3} kWh");
                    }
                    else
                    {
                        meterStart = 0;
                        meterSource = "Default (No data)";
                        _logger.LogError($"No meter reading available. Using default: 0 kWh");
                    }
                }

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
                    ChargingTariff = tariffToUse,
                    ChargingTotalFee = "0",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow,
                    // Session Limits
                    EnergyLimit = request.EnergyLimit,
                    CostLimit = request.CostLimit,
                    TimeLimit = request.TimeLimit,
                    BatteryIncreaseLimit = request.BatteryIncreaseLimit
                };

                _dbContext.ChargingSessions.Add(session);
                await _dbContext.SaveChangesAsync();
                var socResult = await GetCachedSoC(ocppTransaction.ChargePointId, request.ConnectorId, maxAgeMinutes: 2);
                if (socResult.Success && socResult.SoC.HasValue)
                {
                    session.SoCStart = socResult.SoC.Value;
                    session.SoCLastUpdate = socResult.Timestamp;
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation("StartChargingSession => Initial SoC captured: {0}% at {1}",
                        socResult.SoC.Value, socResult.Timestamp);
                }
                else
                {
                    _logger.LogInformation("StartChargingSession => No recent SoC data available for initial capture");
                }

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
                        MeterSource = meterSource,
                        Tariff = session.ChargingTariff,
                        StartTime = startTime,
                        BatteryStateOfCharge = new
                        {
                            StartSoC = session.SoCStart.HasValue ? Math.Round(session.SoCStart.Value, 1) : (double?)null,
                            EndSoC = (double?)null,
                            CurrentSoC = session.SoCStart.HasValue ? Math.Round(session.SoCStart.Value, 1) : (double?)null,
                            SoCGain = (double?)null,
                            LastUpdate = session.SoCLastUpdate,
                            Unit = "%",
                            IsRealtime = session.SoCStart.HasValue,
                            DataSource = session.SoCStart.HasValue ? "OCPP Server Cache (Live)" : "Not Available"
                        },
                        Recommendation = meterSource == "OCPP Transaction"
            ? "Meter reading from authoritative OCPP transaction"
            : meterSource == "Connector Real-time"
                ? "Using real-time connector meter (transaction not immediately available)"
                : "Warning: No meter data available. Check charge point connection."
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
                double? socGain = null;

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

                // Get real-time connector status BEFORE calling stop transaction
                var connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargePoint.ChargePointId
                        && cs.ConnectorId == connectorId && cs.Active == 1);

                double realtimeMeterReading = 0;
                DateTime? realtimeMeterTime = null;

                if (connectorStatus != null && connectorStatus.LastMeter.HasValue)
                {
                    realtimeMeterReading = connectorStatus.LastMeter.Value;
                    realtimeMeterTime = connectorStatus.LastMeterTime;
                    _logger.LogInformation($"Real-time meter from connector: {realtimeMeterReading:F3} kWh at {realtimeMeterTime}");
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

                // Get actual meter readings - use multiple sources for accuracy
                double startReading = 0;
                double endReading = 0;
                DateTime actualStartTime = session.StartTime;
                DateTime actualEndTime = DateTime.UtcNow;

                // Priority 1: OCPP Transaction (most authoritative after stop)
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

                        _logger.LogInformation($"Using OCPP transaction data: Start={startReading:F3}, Stop={endReading:F3}");
                    }
                    else
                    {
                        _logger.LogWarning($"Transaction {session.TransactionId} not found in database");
                    }
                }

                // Priority 2: If transaction stop meter not available, use real-time connector meter
                if (endReading == 0 && realtimeMeterReading > 0)
                {
                    endReading = realtimeMeterReading;
                    if (realtimeMeterTime.HasValue)
                    {
                        actualEndTime = realtimeMeterTime.Value;
                    }
                    _logger.LogInformation($"Using real-time connector meter: {endReading:F3} kWh");
                }

                // Priority 3: Fallback to session/manual readings
                if (startReading == 0 && double.TryParse(session.StartMeterReading, out double sessionStart))
                {
                    startReading = sessionStart;
                    _logger.LogInformation($"Using session start meter: {startReading:F3} kWh");
                }

                if (endReading == 0 && double.TryParse(request.EndMeterReading, out double manualEnd))
                {
                    endReading = manualEnd;
                    _logger.LogInformation($"Using manual end meter: {endReading:F3} kWh");
                }

                // Validate meter readings
                if (endReading < startReading)
                {
                    _logger.LogError($"Invalid meter readings: End ({endReading}) < Start ({startReading}). Setting to start value.");
                    endReading = startReading;
                }

                var socResult = await GetCachedSoC(chargingStation.ChargingPointId, int.Parse(session.ChargingGunId), maxAgeMinutes: 5);
                if (socResult.Success && socResult.SoC.HasValue)
                {
                    session.SoCEnd = socResult.SoC.Value;
                    session.SoCLastUpdate = socResult.Timestamp;

                    // Calculate SoC gain if we have both start and end
                    if (session.SoCStart.HasValue)
                    {
                        socGain = session.SoCEnd.Value - session.SoCStart.Value;
                        _logger.LogInformation("EndChargingSession => SoC Gain: {0}% (Start: {1}%, End: {2}%)",
                            socGain, session.SoCStart.Value, session.SoCEnd.Value);
                    }

                    // Clear cache after capturing
                    await ClearCachedSoC(chargingStation.ChargingPointId, int.Parse(session.ChargingGunId));
                }
                else
                {
                    _logger.LogInformation("EndChargingSession => No recent SoC data available for final capture");
                }

                // Get charging gun for accurate tariff
                var chargingGun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.ChargingStationId == chargingStation.RecId
                        && g.ConnectorId == session.ChargingGunId && g.Active == 1);

                double tariff = 0;
                if (chargingGun != null && !string.IsNullOrEmpty(chargingGun.ChargerTariff) &&
                    double.TryParse(chargingGun.ChargerTariff, out double gunTariff))
                {
                    tariff = gunTariff;
                    _logger.LogInformation($"Using tariff from charging gun: ₹{tariff:F2}/kWh");
                }
                else if (double.TryParse(session.ChargingTariff, out double sessionTariff))
                {
                    tariff = sessionTariff;
                    _logger.LogInformation($"Using tariff from session: ₹{tariff:F2}/kWh");
                }

                // Update session with end details
                session.EndTime = actualEndTime;
                session.EndMeterReading = endReading.ToString("F3");
                session.StartMeterReading = startReading.ToString("F3");

                decimal totalFee = 0;
                double energyTransmitted = Math.Max(0, endReading - startReading);

                session.EnergyTransmitted = energyTransmitted.ToString("F3");

                // Calculate total fee based on tariff and energy
                totalFee = (decimal)(energyTransmitted * tariff);
                session.ChargingTariff = tariff.ToString("F2");
                session.ChargingTotalFee = totalFee.ToString("F2");

                // Calculate charging speed (kW)
                var duration = actualEndTime - actualStartTime;
                // Only calculate charging speed if duration is positive
                if (duration.TotalSeconds > 0 && duration.TotalHours > 0)
                {
                    double chargingSpeed = energyTransmitted / duration.TotalHours;
                    session.ChargingSpeed = chargingSpeed.ToString("F2");
                }
                else if (duration.TotalSeconds <= 0)
                {
                    _logger.LogWarning($"Negative or zero duration detected for session {session.RecId}. Start: {actualStartTime}, End: {actualEndTime}");
                    session.ChargingSpeed = "0";
                }

                // Update charging gun meter reading
                if (chargingGun != null)
                {
                    chargingGun.ChargerMeterReading = endReading.ToString("F3");
                    chargingGun.UpdatedOn = DateTime.UtcNow;
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

                // Calculate new balance (allow negative balances)
                decimal newBalance = previousBalance - totalFee;

                // Log if balance goes negative
                if (newBalance < 0)
                {
                    _logger.LogWarning($"User {session.UserId} balance went negative. Previous: ₹{previousBalance:F2}, Fee: ₹{totalFee:F2}, New: ₹{newBalance:F2}");
                }

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
                    AdditionalInfo2 = $"Energy: {energyTransmitted:F3} kWh @ ₹{tariff:F2}/kWh = ₹{totalFee:F2}",
                    AdditionalInfo3 = $"Meter: {startReading:F3} → {endReading:F3} kWh | Duration: {duration.TotalMinutes:F0}min",
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.WalletTransactionLogs.Add(walletTransaction);
                session.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation($"Charging session ended: {session.RecId} (Txn: {session.TransactionId}). Energy: {energyTransmitted:F3}kWh, Fee: ₹{totalFee:F2}, Balance: ₹{newBalance:F2}");

                // Calculate SoC gain for response
                if (session.SoCStart.HasValue && session.SoCEnd.HasValue)
                {
                    socGain = session.SoCEnd.Value - session.SoCStart.Value;
                }

                // Build success message
                string successMessage = $"Charging session ended successfully. ₹{totalFee:F2} debited.";
                if (newBalance < 0)
                {
                    successMessage += $" Warning: Your balance is now negative (₹{newBalance:F2}). Please recharge your wallet.";
                }
                successMessage += $" OCPP: {ocppResult.Message}";

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = successMessage,
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
                        BatteryStateOfCharge = new
                        {
                            StartSoC = session.SoCStart.HasValue ? Math.Round(session.SoCStart.Value, 1) : (double?)null,
                            EndSoC = session.SoCEnd.HasValue ? Math.Round(session.SoCEnd.Value, 1) : (double?)null,
                            CurrentSoC = session.SoCEnd.HasValue ? Math.Round(session.SoCEnd.Value, 1) : (double?)null,
                            SoCGain = socGain.HasValue ? Math.Round(socGain.Value, 1) : (double?)null,
                            LastUpdate = session.SoCLastUpdate,
                            Unit = "%",
                            IsRealtime = false,
                            DataSource = session.SoCEnd.HasValue ? "Database (Historical)" : "Not Available"
                        },
                        DataSource = new
                        {
                            TransactionUsed = session.TransactionId.HasValue,
                            ConnectorMeterUsed = realtimeMeterReading > 0 && endReading == realtimeMeterReading,
                            ManualMeterUsed = !string.IsNullOrEmpty(request.EndMeterReading) && endReading.ToString("F3") == request.EndMeterReading,
                            ConnectorMeterValue = realtimeMeterReading > 0 ? $"{realtimeMeterReading:F3} kWh" : "Not available",
                            ConnectorMeterTime = realtimeMeterTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"
                        },
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
                Database.EVCDTO.ChargingGuns chargingGun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.RecId == chargingGunId && g.Active == 1);

                var activeSession = await _dbContext.ChargingSessions
                    .FirstOrDefaultAsync(s => s.ChargingGunId == chargingGun.ConnectorId &&
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
                    ConnectorId = chargingGun?.ConnectorId,
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

                // Get charging station and charging gun details
                var chargingStation = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(cs => cs.RecId == session.ChargingStationID);

                Database.EVCDTO.ChargingGuns chargingGun = null;
                Database.EVCDTO.ChargerTypeMaster chargerType = null;
                double actualTariff = 0;
                string powerOutput = null;

                if (chargingStation != null)
                {
                    // Find the charging gun used in this session
                    chargingGun = await _dbContext.ChargingGuns
                        .FirstOrDefaultAsync(g => g.ChargingStationId == chargingStation.RecId
                            && g.ConnectorId == session.ChargingGunId && g.Active == 1);

                    if (chargingGun != null)
                    {
                        // Get charger type details
                        if (!string.IsNullOrEmpty(chargingGun.ChargerTypeId))
                        {
                            chargerType = await _dbContext.ChargerTypeMasters
                                .FirstOrDefaultAsync(ct => ct.RecId == chargingGun.ChargerTypeId && ct.Active == 1);
                        }

                        // Use tariff from charging gun (most accurate)
                        if (!string.IsNullOrEmpty(chargingGun.ChargerTariff) &&
                            double.TryParse(chargingGun.ChargerTariff, out double gunTariff))
                        {
                            actualTariff = gunTariff;
                        }

                        powerOutput = chargingGun.PowerOutput;
                    }
                }

                // Fallback to session tariff if gun tariff not available
                if (actualTariff == 0 && double.TryParse(session.ChargingTariff, out double sessionTariff))
                {
                    actualTariff = sessionTariff;
                }

                // Get user vehicle details for SOC calculation
                var userVehicle = await _dbContext.UserVehicles
                    .Where(v => v.UserId == session.UserId && v.DefaultConfig == 1 && v.Active == 1)
                    .FirstOrDefaultAsync();

                double? batteryCapacity = null;
                string batteryCapacityUnit = null;
                string vehicleModel = null;
                string vehicleManufacturer = null;

                if (userVehicle != null)
                {
                    // Get battery capacity
                    if (!string.IsNullOrEmpty(userVehicle.BatteryCapacityId))
                    {
                        var batteryCapacityMaster = await _dbContext.BatteryCapacityMasters
                            .FirstOrDefaultAsync(bc => bc.RecId == userVehicle.BatteryCapacityId && bc.Active == 1);

                        if (batteryCapacityMaster != null &&
                            double.TryParse(batteryCapacityMaster.BatteryCapcacity, out double capacity))
                        {
                            batteryCapacity = capacity;
                            batteryCapacityUnit = batteryCapacityMaster.BatteryCapcacityUnit;
                        }
                    }

                    // Get vehicle details
                    if (!string.IsNullOrEmpty(userVehicle.CarModelID))
                    {
                        var evModel = await _dbContext.EVModelMasters
                            .FirstOrDefaultAsync(ev => ev.RecId == userVehicle.CarModelID && ev.Active == 1);

                        if (evModel != null)
                        {
                            vehicleModel = evModel.ModelName;

                            if (!string.IsNullOrEmpty(evModel.ManufacturerId))
                            {
                                var manufacturer = await _dbContext.CarManufacturerMasters
                                    .FirstOrDefaultAsync(m => m.RecId == evModel.ManufacturerId && m.Active == 1);
                                vehicleManufacturer = manufacturer?.ManufacturerName;
                            }
                        }
                    }
                }

                // Initialize variables for real-time data
                double meterStart = 0;
                double meterCurrent = 0;
                double energyConsumed = 0;
                double calculatedCost = 0;
                double averageChargingSpeed = 0;
                double peakChargingSpeed = 0;
                string status = "Unknown";
                DateTime actualStartTime = session.StartTime;
                DateTime? actualEndTime = session.EndTime == DateTime.MinValue ? null : session.EndTime;
                bool isActiveSession = session.EndTime == DateTime.MinValue;

                // Get real-time connector meter reading for active sessions
                double? realtimeConnectorMeter = null;
                if (isActiveSession && chargingStation != null)
                {
                    int connectorId = 1;
                    if (int.TryParse(session.ChargingGunId, out int extractedId))
                    {
                        connectorId = extractedId;
                    }

                    var connectorStatus = await _dbContext.ConnectorStatuses
                        .FirstOrDefaultAsync(cs => cs.ChargePointId == chargingStation.ChargingPointId
                            && cs.ConnectorId == connectorId && cs.Active == 1);

                    if (connectorStatus != null && connectorStatus.LastMeter.HasValue)
                    {
                        realtimeConnectorMeter = connectorStatus.LastMeter.Value;
                        _logger.LogInformation($"Fetched real-time connector meter for active session {sessionId}: {realtimeConnectorMeter:F3} kWh");
                    }
                }

                // Get real-time data from OCPP transaction if available
                if (session.TransactionId.HasValue)
                {
                    var transaction = await _dbContext.Transactions
                        .FirstOrDefaultAsync(t => t.TransactionId == session.TransactionId.Value);

                    if (transaction != null)
                    {
                        meterStart = transaction.MeterStart;

                        // For active sessions, use real-time connector meter if available
                        if (isActiveSession && realtimeConnectorMeter.HasValue)
                        {
                            meterCurrent = realtimeConnectorMeter.Value;
                            _logger.LogInformation($"Using real-time connector meter for active session: {meterCurrent:F3} kWh");
                        }
                        else if (transaction.StopTime.HasValue)
                        {
                            // Completed session - use transaction MeterStop
                            meterCurrent = transaction.MeterStop ?? transaction.MeterStart;
                        }
                        else
                        {
                            // Active session but no real-time meter - fallback to MeterStart
                            meterCurrent = transaction.MeterStart;
                            _logger.LogWarning($"No real-time meter available for active session {sessionId}. Using MeterStart.");
                        }

                        actualStartTime = transaction.StartTime;

                        if (transaction.StopTime.HasValue)
                        {
                            actualEndTime = transaction.StopTime.Value;
                            status = "Completed";
                        }
                        else
                        {
                            status = isActiveSession ? "Charging" : "Completed";
                        }

                        energyConsumed = Math.Max(0, meterCurrent - meterStart);
                        calculatedCost = energyConsumed * actualTariff;

                        var elapsedTime = (actualEndTime ?? DateTime.UtcNow) - actualStartTime;
                        if (elapsedTime.TotalHours > 0)
                        {
                            averageChargingSpeed = energyConsumed / elapsedTime.TotalHours;
                        }

                        // Peak charging speed from power output if available
                        if (!string.IsNullOrEmpty(powerOutput) && double.TryParse(powerOutput, out double power))
                        {
                            peakChargingSpeed = power;
                        }
                        else
                        {
                            peakChargingSpeed = averageChargingSpeed; // Fallback
                        }

                        if (isActiveSession)
                        {
                            _logger.LogInformation($"Real-time update for session {sessionId}: {energyConsumed:F2} kWh consumed, ₹{calculatedCost:F2} estimated");
                        }
                    }
                    else
                    {
                        // Transaction not found, use session data and real-time meter
                        if (double.TryParse(session.StartMeterReading, out meterStart))
                        {
                            meterCurrent = meterStart;
                        }

                        // For active sessions, try to use real-time connector meter
                        if (isActiveSession && realtimeConnectorMeter.HasValue)
                        {
                            meterCurrent = realtimeConnectorMeter.Value;
                            energyConsumed = Math.Max(0, meterCurrent - meterStart);
                            calculatedCost = energyConsumed * actualTariff;
                            _logger.LogInformation($"Using real-time connector meter (no transaction): {meterCurrent:F3} kWh");
                        }
                        else if (double.TryParse(session.EndMeterReading, out double endReading))
                        {
                            meterCurrent = endReading;
                            energyConsumed = Math.Max(0, endReading - meterStart);
                            calculatedCost = energyConsumed * actualTariff;
                        }

                        status = isActiveSession ? "Pending" : "Completed";
                    }
                }
                else
                {
                    // No transaction ID, use session data and real-time meter
                    if (double.TryParse(session.StartMeterReading, out meterStart))
                    {
                        meterCurrent = meterStart;
                    }

                    // For active sessions, try to use real-time connector meter
                    if (isActiveSession && realtimeConnectorMeter.HasValue)
                    {
                        meterCurrent = realtimeConnectorMeter.Value;
                        energyConsumed = Math.Max(0, meterCurrent - meterStart);
                        calculatedCost = energyConsumed * actualTariff;
                        _logger.LogInformation($"Using real-time connector meter (no transaction ID): {meterCurrent:F3} kWh");
                    }
                    else if (double.TryParse(session.EndMeterReading, out double endReading) && endReading > 0)
                    {
                        meterCurrent = endReading;
                        energyConsumed = Math.Max(0, endReading - meterStart);
                        calculatedCost = energyConsumed * actualTariff;
                    }

                    status = isActiveSession ? "Pending" : "Completed";
                }

                // Calculate SOC change if battery capacity is available
                double? socChange = null;
                double? socChangePercentage = null;
                double? estimatedRange = null;
                double? chargingEfficiency = null;
                var duration = (actualEndTime ?? DateTime.UtcNow) - actualStartTime;

                // Handle negative durations - set to zero if negative
                if (duration.TotalSeconds < 0)
                {
                    duration = TimeSpan.Zero;
                    _logger.LogWarning($"Negative duration detected for session {sessionId}. Setting duration to zero.");
                }

                if (batteryCapacity.HasValue && batteryCapacity.Value > 0 && energyConsumed > 0)
                {
                    // SOC change in kWh
                    socChange = energyConsumed;

                    // SOC change as percentage
                    socChangePercentage = (energyConsumed / batteryCapacity.Value) * 100;

                    // Estimated range added (assuming 4-5 km per kWh average for EVs)
                    estimatedRange = energyConsumed * 4.5;

                    // Charging efficiency (typically 85-95% for EVs)
                    // If we have power output, we can estimate losses
                    if (!string.IsNullOrEmpty(powerOutput) && double.TryParse(powerOutput, out double maxPower))
                    {
                        if (duration.TotalHours > 0)
                        {
                            double theoreticalEnergy = maxPower * duration.TotalHours;
                            if (theoreticalEnergy > 0)
                            {
                                chargingEfficiency = (energyConsumed / theoreticalEnergy) * 100;
                                // Cap at realistic values
                                chargingEfficiency = Math.Min(chargingEfficiency.Value, 100);
                            }
                        }
                    }
                    else
                    {
                        // Assume typical efficiency
                        chargingEfficiency = 90.0;
                    }
                }

                double? currentSoC = null;
                DateTime? currentSoCTime = null;
                if (isActiveSession && int.TryParse(session.ChargingGunId, out int gunConnectorId))
                {
                    var socResult = await GetCachedSoC(chargingStation.ChargingPointId, gunConnectorId, maxAgeMinutes: 5);
                    if (socResult.Success && socResult.SoC.HasValue)
                    {
                        currentSoC = socResult.SoC.Value;
                        currentSoCTime = socResult.Timestamp;
                        _logger.LogTrace("GetChargingSessionDetails => Real-time SoC: {0}% at {1}", currentSoC, currentSoCTime);
                    }
                }

                // Calculate SoC gain
                double? socGain = null;
                if (session.SoCStart.HasValue && (session.SoCEnd.HasValue || currentSoC.HasValue))
                {
                    double endValue = session.SoCEnd ?? currentSoC.Value;
                    socGain = endValue - session.SoCStart.Value;
                }

                bool isActive = session.EndTime == DateTime.MinValue;

                // Calculate cost breakdown
                var energyCost = calculatedCost;
                var serviceFee = 0.0; // Can add service fee logic here
                var taxes = energyCost * 0.18; // Assuming 18% GST
                var totalCost = energyCost + serviceFee + taxes;

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

                        // Meter Readings
                        MeterReadings = new
                        {
                            StartReading = Math.Round(meterStart, 3),
                            CurrentReading = Math.Round(meterCurrent, 3),
                            Unit = "kWh",
                            DataSource = isActiveSession && realtimeConnectorMeter.HasValue
                                ? "Real-time Connector (Live)"
                                : session.TransactionId.HasValue
                                    ? "OCPP Transaction"
                                    : "Session Data",
                            IsRealtime = isActiveSession && realtimeConnectorMeter.HasValue
                        },

                        // Energy Consumption
                        EnergyConsumption = new
                        {
                            TotalEnergy = Math.Round(energyConsumed, 3),
                            Unit = "kWh",
                            Description = $"{Math.Round(energyConsumed, 2)} kWh delivered to vehicle"
                        },

                        // State of Charge (SOC) Details
                        StateOfCharge = batteryCapacity.HasValue ? new
                        {
                            SocChange = socChange.HasValue ? Math.Round(socChange.Value, 2) : 12,
                            SocChangePercentage = socChangePercentage.HasValue ? Math.Round(socChangePercentage.Value, 1) : 2,
                            BatteryCapacity = Math.Round(batteryCapacity.Value, 1),
                            BatteryCapacityUnit = batteryCapacityUnit ?? "kWh",
                            EstimatedRangeAdded = estimatedRange.HasValue ? Math.Round(estimatedRange.Value, 0) : 4,
                            EstimatedRangeUnit = "km",
                            Description = socChangePercentage.HasValue
                                ? $"Battery charged by {Math.Round(socChangePercentage.Value, 1)}% (~{Math.Round(estimatedRange.Value, 0)} km range added)"
                                : "Battery capacity information available"
                        } : null,

                        // Vehicle Information
                        Vehicle = userVehicle != null ? new
                        {
                            Manufacturer = vehicleManufacturer,
                            Model = vehicleModel,
                            Variant = userVehicle.CarModelVariant,
                            RegistrationNumber = userVehicle.CarRegistrationNumber,
                            BatteryCapacity = batteryCapacity.HasValue ? $"{Math.Round(batteryCapacity.Value, 1)} {batteryCapacityUnit ?? "kWh"}" : "Not specified"
                        } : null,

                        // Charging Performance
                        ChargingPerformance = new
                        {
                            AverageChargingSpeed = Math.Round(averageChargingSpeed, 2),
                            PeakChargingSpeed = Math.Round(peakChargingSpeed, 2),
                            Unit = "kW",
                            ChargingEfficiency = chargingEfficiency.HasValue ? Math.Round(chargingEfficiency.Value, 1) : 80,
                            EfficiencyUnit = "%",
                            Description = chargingEfficiency.HasValue
                                ? $"Average {Math.Round(averageChargingSpeed, 1)} kW charging at {Math.Round(chargingEfficiency.Value, 1)}% efficiency"
                                : $"Average charging speed: {Math.Round(averageChargingSpeed, 1)} kW"
                        },

                        // Charger Information
                        ChargerDetails = chargingGun != null ? new
                        {
                            ChargerType = chargerType?.ChargerType ?? "Standard",
                            PowerOutput = powerOutput ?? "Not specified",
                            ChargerTariff = Math.Round(actualTariff, 2),
                            TariffUnit = "₹/kWh",
                            ConnectorId = chargingGun.ConnectorId,
                            ChargerStatus = chargingGun.ChargerStatus
                        } : null,

                        // Cost Breakdown
                        CostDetails = new
                        {
                            EnergyCost = Math.Round(energyCost, 2),
                            ServiceFee = Math.Round(serviceFee, 2),
                            Taxes = Math.Round(taxes, 2),
                            TotalCost = Math.Round(totalCost, 2),
                            Currency = "₹",
                            TariffApplied = Math.Round(actualTariff, 2),
                            TariffUnit = "₹/kWh",
                            Breakdown = $"Energy: ₹{Math.Round(energyCost, 2)} + Tax: ₹{Math.Round(taxes, 2)} = ₹{Math.Round(totalCost, 2)}"
                        },

                        // Session Timing
                        Timing = new
                        {
                            StartTime = actualStartTime,
                            EndTime = actualEndTime,
                            Duration = new
                            {
                                TotalMinutes = Math.Round(duration.TotalMinutes, 0),
                                Hours = duration.Hours,
                                Minutes = duration.Minutes,
                                TotalHours = Math.Round(duration.TotalHours, 2),
                                FormattedDuration = duration.Hours > 0
                                    ? $"{duration.Hours}h {duration.Minutes}m"
                                    : $"{duration.Minutes}m"
                            },
                            IsActive = isActive,
                            LastUpdate = DateTime.UtcNow
                        },

                        // Summary Statistics
                        Summary = new
                        {
                            EnergyDelivered = $"{Math.Round(energyConsumed, 2)} kWh",
                            SocGained = socChangePercentage.HasValue
                                ? $"{Math.Round(socChangePercentage.Value, 1)}%"
                                : "N/A",
                            RangeAdded = estimatedRange.HasValue
                                ? $"~{Math.Round(estimatedRange.Value, 0)} km"
                                : "N/A",
                            TotalCost = $"₹{Math.Round(totalCost, 2)}",
                            ChargingTime = duration.Hours > 0
                                ? $"{duration.Hours}h {duration.Minutes}m"
                                : $"{duration.Minutes}m",
                            AverageSpeed = $"{Math.Round(averageChargingSpeed, 1)} kW",
                            CostPerKwh = $"₹{Math.Round(actualTariff, 2)}"
                        },
                        BatteryStateOfCharge = new
                        {
                            StartSoC = session.SoCStart.HasValue ? Math.Round(session.SoCStart.Value, 1) : (double?)null,
                            EndSoC = session.SoCEnd.HasValue ? Math.Round(session.SoCEnd.Value, 1) : (double?)null,
                            CurrentSoC = currentSoC.HasValue ? Math.Round(currentSoC.Value, 1) : (double?)null,
                            SoCGain = socGain.HasValue ? Math.Round(socGain.Value, 1) : (double?)null,
                            LastUpdate = currentSoCTime ?? session.SoCLastUpdate,
                            Unit = "%",
                            IsRealtime = isActiveSession && currentSoC.HasValue,
                            DataSource = currentSoC.HasValue ? "OCPP Server Cache (Live)" : "Database (Historical)"
                        }
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
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var query = _dbContext.ChargingSessions.Where(s => s.Active == 1 && s.UserId == userId);

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

                // Use database aggregation for summary calculations - MUCH faster!
                var summaryData = await query.Select(s => new
                {
                    EnergyTransmitted = s.EnergyTransmitted,
                    ChargingTotalFee = s.ChargingTotalFee,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime
                }).ToListAsync();

                double totalEnergyTransmitted = 0;
                decimal totalChargingTotalFee = 0;
                TimeSpan totalChargingTime = TimeSpan.Zero;

                foreach (var item in summaryData)
                {
                    if (!string.IsNullOrEmpty(item.EnergyTransmitted) &&
                        double.TryParse(item.EnergyTransmitted, out double energy))
                    {
                        totalEnergyTransmitted += energy;
                    }

                    if (!string.IsNullOrEmpty(item.ChargingTotalFee) &&
                        decimal.TryParse(item.ChargingTotalFee, out decimal fee))
                    {
                        totalChargingTotalFee += fee;
                    }

                    if (item.EndTime != DateTime.MinValue)
                    {
                        var duration = item.EndTime - item.StartTime;
                        if (duration.TotalSeconds > 0)
                        {
                            totalChargingTime += duration;
                        }
                    }
                    else
                    {
                        var duration = DateTime.UtcNow - item.StartTime;
                        if (duration.TotalSeconds > 0)
                        {
                            totalChargingTime += duration;
                        }
                    }
                }

                // Get paginated sessions with related data in ONE query
                var pagedSessions = await query
                    .OrderByDescending(s => s.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Bulk load all related data to avoid N+1 queries
                var stationIds = pagedSessions.Select(s => s.ChargingStationID).Distinct().ToList();
                var stations = await _dbContext.ChargingStations
                    .Where(cs => stationIds.Contains(cs.RecId))
                    .ToDictionaryAsync(cs => cs.RecId, cs => cs);

                var hubIds = stations.Values.Select(s => s.ChargingHubId).Distinct().ToList();
                var hubs = await _dbContext.ChargingHubs
                    .Where(ch => hubIds.Contains(ch.RecId))
                    .ToDictionaryAsync(ch => ch.RecId, ch => ch);

                var gunKeys = pagedSessions.Select(s => new { s.ChargingStationID, s.ChargingGunId }).Distinct().ToList();
                var guns = await _dbContext.ChargingGuns
                    .Where(cg => stationIds.Contains(cg.ChargingStationId))
                    .ToListAsync();
                var gunsDict = guns.ToDictionary(g => $"{g.ChargingStationId}_{g.ConnectorId}", g => g);

                var chargePointIds = stations.Values.Select(s => s.ChargingPointId).Distinct().ToList();
                var connectorStatuses = await _dbContext.ConnectorStatuses
                    .Where(cs => chargePointIds.Contains(cs.ChargePointId) && cs.Active == 1)
                    .ToListAsync();
                var connectorDict = connectorStatuses.ToDictionary(cs => $"{cs.ChargePointId}_{cs.ConnectorId}", cs => cs);

                // Map paginated sessions to DTOs using pre-loaded data
                var sessionDtos = new System.Collections.Generic.List<ChargingSessionDto>();
                foreach (var session in pagedSessions)
                {
                    sessionDtos.Add(MapToChargingSessionDtoFast(session, stations, hubs, gunsDict, connectorDict));
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
                        Sessions = sessionDtos,
                        Summary = new
                        {
                            TotalEnergyTransmitted = Math.Round(totalEnergyTransmitted, 3),
                            TotalEnergyUnit = "kWh",
                            TotalChargingTotalFee = Math.Round(totalChargingTotalFee, 2),
                            TotalFeeUnit = "₹",
                            TotalChargingTime = new
                            {
                                TotalHours = Math.Round(totalChargingTime.TotalHours, 2),
                                TotalMinutes = Math.Round(totalChargingTime.TotalMinutes, 0),
                                FormattedDuration = totalChargingTime.Days > 0
                                    ? $"{totalChargingTime.Days}d {totalChargingTime.Hours}h {totalChargingTime.Minutes}m"
                                    : totalChargingTime.Hours > 0
                                        ? $"{totalChargingTime.Hours}h {totalChargingTime.Minutes}m"
                                        : $"{totalChargingTime.Minutes}m"
                            }
                        }
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

        /// <summary>
        /// Get real-time connector meter values
        /// </summary>
        [HttpGet("connector-meter-status/{chargePointId}/{connectorId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetConnectorMeterStatus(string chargePointId, int connectorId)
        {
            try
            {
                var connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargePointId
                        && cs.ConnectorId == connectorId && cs.Active == 1);

                if (connectorStatus == null)
                {
                    return Ok(new ChargingSessionResponseDto
                    {
                        Success = false,
                        Message = "Connector not found"
                    });
                }

                // Find active session for this connector
                var chargingStation = await _dbContext.ChargingStations
                    .FirstOrDefaultAsync(s => s.ChargingPointId == chargePointId && s.Active == 1);

                var activeSession = chargingStation != null
                    ? await _dbContext.ChargingSessions
                        .FirstOrDefaultAsync(s => s.ChargingStationID == chargingStation.RecId
                            && s.ChargingGunId == connectorId.ToString()
                            && s.Active == 1
                            && s.EndTime == DateTime.MinValue)
                    : null;

                double? energySinceStart = null;
                double? estimatedCost = null;
                TimeSpan? duration = null;

                if (activeSession != null && connectorStatus.LastMeter.HasValue)
                {
                    if (double.TryParse(activeSession.StartMeterReading, out double startMeter))
                    {
                        energySinceStart = Math.Max(0, connectorStatus.LastMeter.Value - startMeter);

                        if (double.TryParse(activeSession.ChargingTariff, out double tariff))
                        {
                            estimatedCost = energySinceStart.Value * tariff;
                        }

                        duration = DateTime.UtcNow - activeSession.StartTime;
                    }
                }

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = "Connector meter status retrieved successfully",
                    Data = new
                    {
                        ChargePointId = chargePointId,
                        ConnectorId = connectorId,
                        ConnectorName = connectorStatus.ConnectorName,
                        Status = connectorStatus.LastStatus,
                        StatusTime = connectorStatus.LastStatusTime,
                        MeterValue = connectorStatus.LastMeter.HasValue
                            ? Math.Round(connectorStatus.LastMeter.Value, 3)
                            : (double?)null,
                        MeterUnit = "kWh",
                        MeterTime = connectorStatus.LastMeterTime,
                        MeterAge = connectorStatus.LastMeterTime.HasValue
                            ? Math.Round((DateTime.UtcNow - connectorStatus.LastMeterTime.Value).TotalMinutes, 0) + " minutes ago"
                            : "Never updated",
                        HasActiveSession = activeSession != null,
                        ActiveSession = activeSession != null ? new
                        {
                            SessionId = activeSession.RecId,
                            StartTime = activeSession.StartTime,
                            StartMeter = activeSession.StartMeterReading,
                            EnergySinceStart = energySinceStart.HasValue ? Math.Round(energySinceStart.Value, 3) : (double?)null,
                            EstimatedCost = estimatedCost.HasValue ? Math.Round(estimatedCost.Value, 2) : (double?)null,
                            Duration = duration.HasValue
                                ? $"{duration.Value.Hours}h {duration.Value.Minutes}m"
                                : null,
                            Tariff = activeSession.ChargingTariff
                        } : null,
                        Recommendation = connectorStatus.LastMeter.HasValue && connectorStatus.LastMeterTime.HasValue
                            ? (DateTime.UtcNow - connectorStatus.LastMeterTime.Value).TotalMinutes < 5
                                ? "Meter values are up-to-date"
                                : "Warning: Meter values may be stale. Check charge point connection."
                            : "No meter values received yet"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving connector meter status");
                return Ok(new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while retrieving connector meter status"
                });
            }
        }

        /// <summary>
        /// Check active sessions for limit violations (designed to be called periodically)
        /// </summary>
        [HttpGet("check-session-limits")]
        public async Task<IActionResult> CheckSessionLimits()
        {
            try
            {
                var activeSessions = await _dbContext.ChargingSessions
                    .Where(s => s.Active == 1 && s.EndTime == DateTime.MinValue)
                    .ToListAsync();

                var violatedSessions = new List<SessionLimitCheckDto>();
                var autoStoppedSessions = new List<string>();

                foreach (var session in activeSessions)
                {
                    var limitCheck = await CheckSessionLimitViolations(session);

                    if (limitCheck.HasViolations)
                    {
                        violatedSessions.Add(limitCheck);

                        // Auto-stop the session if it violates limits
                        _logger.LogWarning($"Session {session.RecId} has violated limits: {string.Join(", ", limitCheck.ViolatedLimits)}");

                        try
                        {
                            // Attempt to stop the session automatically
                            var chargingStation = await _dbContext.ChargingStations
                                .FirstOrDefaultAsync(cs => cs.RecId == session.ChargingStationID);

                            if (chargingStation != null)
                            {
                                var chargePoint = await _dbContext.ChargePoints
                                    .FirstOrDefaultAsync(cp => cp.ChargePointId == chargingStation.ChargingPointId);

                                if (chargePoint != null && int.TryParse(session.ChargingGunId, out int connectorId))
                                {
                                    var ocppResult = await CallOCPPStopTransaction(chargingStation.ChargingPointId, connectorId);

                                    if (ocppResult.Success)
                                    {
                                        // Update session status
                                        session.EndTime = DateTime.UtcNow;
                                        session.Active = 0;
                                        session.UpdatedOn = DateTime.UtcNow;

                                        // Get final meter reading
                                        var connectorStatus = await _dbContext.ConnectorStatuses
                                            .FirstOrDefaultAsync(cs => cs.ChargePointId == chargingStation.ChargingPointId
                                                && cs.ConnectorId == connectorId && cs.Active == 1);

                                        double startMeter = 0;
                                        double endMeter = 0;
                                        double energyConsumed = 0;
                                        decimal totalFee = 0;

                                        if (connectorStatus?.LastMeter != null)
                                        {
                                            endMeter = connectorStatus.LastMeter.Value;
                                            session.EndMeterReading = endMeter.ToString("F2");

                                            if (double.TryParse(session.StartMeterReading, out startMeter))
                                            {
                                                energyConsumed = Math.Max(0, endMeter - startMeter);
                                                session.EnergyTransmitted = energyConsumed.ToString("F2");

                                                if (double.TryParse(session.ChargingTariff, out double tariff))
                                                {
                                                    totalFee = (decimal)(energyConsumed * tariff);
                                                    session.ChargingTotalFee = totalFee.ToString("F2");
                                                }
                                            }
                                        }

                                        // Debit wallet for auto-stopped session
                                        var lastTransaction = await _dbContext.WalletTransactionLogs
                                            .Where(w => w.UserId == session.UserId && w.Active == 1)
                                            .OrderByDescending(w => w.CreatedOn)
                                            .FirstOrDefaultAsync();

                                        decimal previousBalance = 0;
                                        if (lastTransaction != null && decimal.TryParse(lastTransaction.CurrentCreditBalance, out var lastBalance))
                                        {
                                            previousBalance = lastBalance;
                                        }

                                        // Always debit, even if balance goes negative
                                        decimal newBalance = previousBalance - totalFee;

                                        // Log if balance goes negative
                                        if (newBalance < 0)
                                        {
                                            _logger.LogWarning($"Auto-stopped session {session.RecId} - Balance went negative. Previous: ₹{previousBalance:F2}, Fee: ₹{totalFee:F2}, New: ₹{newBalance:F2}");
                                        }

                                        // Create wallet transaction log for auto-stopped charging payment
                                        var walletTransaction = new Database.EVCDTO.WalletTransactionLog
                                        {
                                            RecId = Guid.NewGuid().ToString(),
                                            UserId = session.UserId,
                                            PreviousCreditBalance = previousBalance.ToString("F2"),
                                            CurrentCreditBalance = newBalance.ToString("F2"),
                                            TransactionType = "Debit",
                                            ChargingSessionId = session.RecId,
                                            AdditionalInfo1 = $"Auto-stopped at {chargingStation.ChargingPointId} (OCPP Txn: {session.TransactionId})",
                                            AdditionalInfo2 = $"Energy: {energyConsumed:F3} kWh @ ₹{session.ChargingTariff}/kWh = ₹{totalFee:F2}",
                                            AdditionalInfo3 = $"Meter: {startMeter:F3} → {endMeter:F3} kWh | Reason: Limit violation",
                                            Active = 1,
                                            CreatedOn = DateTime.UtcNow,
                                            UpdatedOn = DateTime.UtcNow
                                        };

                                        _dbContext.WalletTransactionLogs.Add(walletTransaction);
                                        _logger.LogInformation($"Auto-stopped session {session.RecId} - Debited ₹{totalFee:F2}, New balance: ₹{newBalance:F2}");

                                        await _dbContext.SaveChangesAsync();
                                        autoStoppedSessions.Add(session.RecId);

                                        _logger.LogInformation($"Auto-stopped session {session.RecId} due to limit violations");
                                    }
                                    else
                                    {
                                        _logger.LogWarning($"Failed to auto-stop session {session.RecId}: {ocppResult.Message}");
                                    }
                                }
                            }
                        }
                        catch (Exception stopEx)
                        {
                            _logger.LogError(stopEx, $"Error auto-stopping session {session.RecId}");
                        }
                    }
                }

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = $"Checked {activeSessions.Count} active sessions. Found {violatedSessions.Count} with limit violations. Auto-stopped {autoStoppedSessions.Count} sessions.",
                    Data = new
                    {
                        TotalActiveSessions = activeSessions.Count,
                        ViolatedSessions = violatedSessions,
                        AutoStoppedSessionIds = autoStoppedSessions,
                        CheckedAt = DateTime.UtcNow
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking session limits");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while checking session limits"
                });
            }
        }

        /// <summary>
        /// Get limit status for a specific session
        /// </summary>
        [HttpGet("session-limit-status/{sessionId}")]
        [Authorize]
        public async Task<IActionResult> GetSessionLimitStatus(string sessionId)
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
                        Message = "Session not found"
                    });
                }

                var limitCheck = await CheckSessionLimitViolations(session);

                return Ok(new ChargingSessionResponseDto
                {
                    Success = true,
                    Message = limitCheck.HasViolations
                        ? "Session has violated one or more limits"
                        : "Session is within all configured limits",
                    Data = limitCheck
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking limit status for session {sessionId}");
                return StatusCode(500, new ChargingSessionResponseDto
                {
                    Success = false,
                    Message = "An error occurred while checking session limit status"
                });
            }
        }


        #region Helper Methods

        private async Task<ChargingSessionDto> MapToChargingSessionDto(Database.EVCDTO.ChargingSession session)
        {
            var chargingStation = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(cs => cs.RecId == session.ChargingStationID);

            var chargingGun = await _dbContext.ChargingGuns
                .FirstOrDefaultAsync(cg => cg.ChargingStationId == session.ChargingStationID && cg.ConnectorId == session.ChargingGunId);

            var connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargingStation.ChargingPointId
                        && cs.ConnectorId == int.Parse(chargingGun.ConnectorId) && cs.Active == 1);

            string chargingHubName = null;
            var chargingHub = await _dbContext.ChargingHubs
                    .FirstOrDefaultAsync(ch => ch.RecId == chargingStation.ChargingHubId);
            chargingHubName = chargingHub?.ChargingHubName;

            var isActive = session.EndTime == DateTime.MinValue;
            var endTime = session.EndTime == DateTime.MinValue ? (DateTime?)null : session.EndTime;
            var duration = isActive
                ? DateTime.UtcNow - session.StartTime
                : (endTime.HasValue ? endTime.Value - session.StartTime : TimeSpan.Zero);

            // Handle negative durations - set to zero if negative
            if (duration.TotalSeconds < 0)
            {
                duration = TimeSpan.Zero;
            }

            return new ChargingSessionDto
            {
                RecId = session.RecId,
                ChargingGunId = session.ChargingGunId,
                ChargingStationId = session.ChargingStationID,
                ChargingStationName = chargingStation?.ChargingPointId,
                ChargingHubName = chargingHubName,
                ChargingHub = chargingHub != null ? new ChargingHubDto
                {
                    RecId = chargingHub.RecId,
                    ChargingHubName = chargingHub.ChargingHubName,
                    Latitude = chargingHub.Latitude,
                    Active = chargingHub.Active,
                    CreatedOn = chargingHub.CreatedOn,
                    UpdatedOn = chargingHub.UpdatedOn
                } : new ChargingHubDto(),
                ChargingGun = chargingGun,
                ConnectorName = connectorStatus.ConnectorName,
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
                UpdatedOn = session.UpdatedOn,
                SoCStart = session.SoCStart,
                SoCEnd = session.SoCEnd,
                SoCLastUpdate = session.SoCLastUpdate,
                // Session Limits
                EnergyLimit = session.EnergyLimit,
                CostLimit = session.CostLimit,
                TimeLimit = session.TimeLimit,
                BatteryIncreaseLimit = session.BatteryIncreaseLimit
            };
        }

        // Fast mapping using pre-loaded data to avoid N+1 queries
        private ChargingSessionDto MapToChargingSessionDtoFast(
            Database.EVCDTO.ChargingSession session,
            Dictionary<string, Database.EVCDTO.ChargingStation> stations,
            Dictionary<string, Database.EVCDTO.ChargingHub> hubs,
            Dictionary<string, Database.EVCDTO.ChargingGuns> guns,
            Dictionary<string, Database.ConnectorStatus> connectorStatuses)
        {
            Database.EVCDTO.ChargingStation chargingStation = null;
            stations.TryGetValue(session.ChargingStationID, out chargingStation);

            Database.EVCDTO.ChargingGuns chargingGun = null;
            guns.TryGetValue($"{session.ChargingStationID}_{session.ChargingGunId}", out chargingGun);

            Database.ConnectorStatus connectorStatus = null;
            if (chargingStation != null && chargingGun != null)
            {
                connectorStatuses.TryGetValue($"{chargingStation.ChargingPointId}_{chargingGun.ConnectorId}", out connectorStatus);
            }

            string chargingHubName = null;
            var hub = new ChargingHub();
            if (chargingStation != null && hubs.TryGetValue(chargingStation.ChargingHubId, out hub))
            {
                chargingHubName = hub.ChargingHubName;
            }

            var isActive = session.EndTime == DateTime.MinValue;
            var endTime = session.EndTime == DateTime.MinValue ? (DateTime?)null : session.EndTime;
            var duration = isActive
                ? DateTime.UtcNow - session.StartTime
                : (endTime.HasValue ? endTime.Value - session.StartTime : TimeSpan.Zero);

            // Handle negative durations - set to zero if negative
            if (duration.TotalSeconds < 0)
            {
                duration = TimeSpan.Zero;
            }

            return new ChargingSessionDto
            {
                RecId = session.RecId,
                ChargingGunId = session.ChargingGunId,
                ChargingStationId = session.ChargingStationID,
                ChargingStationName = chargingStation?.ChargingPointId,
                ChargingHubName = chargingHubName,
                ChargingHub = chargingStation != null && hub != null
                    ? new ChargingHubDto
                    {
                        RecId = hub.RecId,
                        ChargingHubName = hub.ChargingHubName,
                        Latitude = hub.Latitude,
                        Active = hub.Active,
                        CreatedOn = hub.CreatedOn,
                        UpdatedOn = hub.UpdatedOn
                    }
                    : new ChargingHubDto(),
                ChargingGun = chargingGun,
                ConnectorName = connectorStatus?.ConnectorName,
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
                UpdatedOn = session.UpdatedOn,
                SoCStart = session.SoCStart,
                SoCEnd = session.SoCEnd,
                SoCLastUpdate = session.SoCLastUpdate,
                // Session Limits
                EnergyLimit = session.EnergyLimit,
                CostLimit = session.CostLimit,
                TimeLimit = session.TimeLimit,
                BatteryIncreaseLimit = session.BatteryIncreaseLimit
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

        private async Task<(bool Success, double? SoC, DateTime? Timestamp, int? TransactionId, string Message)> GetCachedSoC(string chargePointId, int connectorId, int maxAgeMinutes = 5)
        {
            try
            {
                string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
                if (string.IsNullOrEmpty(serverApiUrl))
                {
                    _logger.LogError("GetCachedSoC => ServerApiUrl not configured");
                    return (false, null, null, null, "Server API URL not configured");
                }

                string apiKeyConfig = _config.GetValue<string>("ApiKey");
                string apiKey = (apiKeyConfig != null) ? apiKeyConfig : string.Empty;

                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += '/';
                    }

                    httpClient.BaseAddress = new Uri(serverApiUrl);
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
                    }

                    string url = $"SoC/GetSoC?chargePointId={Uri.EscapeDataString(chargePointId)}&connectorId={connectorId}&maxAgeMinutes={maxAgeMinutes}";

                    _logger.LogTrace("GetCachedSoC => Calling: {0}", url);

                    HttpResponseMessage response = await httpClient.GetAsync(url);
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        var apiResponse = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                        if (apiResponse?.success == true && apiResponse?.data != null)
                        {
                            double? soc = apiResponse.data.soC;
                            DateTime? timestamp = apiResponse.data.timestamp;
                            int? transactionId = apiResponse.data.transactionId;

                            _logger.LogInformation("GetCachedSoC => Retrieved SoC: {0}% for {1}/{2}",
                                soc, chargePointId, connectorId);

                            return (true, soc, timestamp, transactionId, "SoC retrieved successfully");
                        }
                        else
                        {
                            _logger.LogInformation("GetCachedSoC => No cached SoC data available");
                            return (true, null, null, null, "No recent SoC data available");
                        }
                    }
                    else
                    {
                        _logger.LogError("GetCachedSoC => HTTP error: {0} - {1}", response.StatusCode, jsonResponse);
                        return (false, null, null, null, $"Server returned: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetCachedSoC => Exception: {0}", ex.Message);
                return (false, null, null, null, ex.Message);
            }
        }

        /// <summary>
        /// Clear cached SoC on OCPP Server
        /// </summary>
        private async Task<(bool Success, string Message)> ClearCachedSoC(string chargePointId, int connectorId)
        {
            try
            {
                string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
                if (string.IsNullOrEmpty(serverApiUrl))
                {
                    _logger.LogError("ClearCachedSoC => ServerApiUrl not configured");
                    return (false, "Server API URL not configured");
                }

                string apiKeyConfig = _config.GetValue<string>("ApiKey");
                string apiKey = (apiKeyConfig != null) ? apiKeyConfig : string.Empty;

                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += '/';
                    }

                    httpClient.BaseAddress = new Uri(serverApiUrl);
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        httpClient.DefaultRequestHeaders.Add("API-Key", apiKey);
                    }

                    var requestBody = new
                    {
                        ChargePointId = chargePointId,
                        ConnectorId = connectorId
                    };

                    var content = new StringContent(
                        JsonConvert.SerializeObject(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    HttpResponseMessage response = await httpClient.PostAsync("API/SoC/ClearSoC", content);
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("ClearCachedSoC => SoC cache cleared for {0}/{1}", chargePointId, connectorId);
                        return (true, "SoC cache cleared");
                    }
                    else
                    {
                        _logger.LogError("ClearCachedSoC => HTTP error: {0} - {1}", response.StatusCode, jsonResponse);
                        return (false, $"Server returned: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClearCachedSoC => Exception: {0}", ex.Message);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Check if a session has violated any of its configured limits
        /// </summary>
        private async Task<SessionLimitCheckDto> CheckSessionLimitViolations(Database.EVCDTO.ChargingSession session)
        {
            var result = new SessionLimitCheckDto
            {
                SessionId = session.RecId,
                ChargingStationId = session.ChargingStationID,
                UserId = session.UserId,
                HasViolations = false,
                LimitStatus = new SessionLimitStatus()
            };

            // Get charging station and connector status for real-time data
            var chargingStation = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(cs => cs.RecId == session.ChargingStationID);

            ConnectorStatus connectorStatus = null;
            int connectorId = 0;
            if (chargingStation != null && int.TryParse(session.ChargingGunId, out connectorId))
            {
                connectorStatus = await _dbContext.ConnectorStatuses
                    .FirstOrDefaultAsync(cs => cs.ChargePointId == chargingStation.ChargingPointId
                        && cs.ConnectorId == connectorId && cs.Active == 1);
            }

            // Calculate current session metrics
            double energyConsumed = 0;
            double currentCost = 0;
            double.TryParse(session.StartMeterReading, out double startMeter);

            if (connectorStatus?.LastMeter != null)
            {
                energyConsumed = connectorStatus.LastMeter.Value - startMeter;
                result.LimitStatus.EnergyConsumed = Math.Round(energyConsumed, 2);
            }
            else if (double.TryParse(session.EnergyTransmitted, out double storedEnergy))
            {
                energyConsumed = storedEnergy;
                result.LimitStatus.EnergyConsumed = Math.Round(energyConsumed, 2);
            }

            if (double.TryParse(session.ChargingTariff, out double tariff))
            {
                currentCost = energyConsumed * tariff;
                result.LimitStatus.CurrentCost = Math.Round(currentCost, 2);
            }

            var elapsedTime = DateTime.UtcNow - session.StartTime;
            int elapsedMinutes = (int)elapsedTime.TotalMinutes;
            result.LimitStatus.ElapsedMinutes = elapsedMinutes;

            double? batteryIncrease = null;
            if (session.SoCStart.HasValue)
            {
                // Try to get current SoC
                if (chargingStation != null && connectorId > 0)
                {
                    var socResult = await GetCachedSoC(chargingStation.ChargingPointId, connectorId, maxAgeMinutes: 5);
                    if (socResult.Success && socResult.SoC.HasValue)
                    {
                        batteryIncrease = socResult.SoC.Value - session.SoCStart.Value;
                        result.LimitStatus.BatteryIncrease = Math.Round(batteryIncrease.Value, 1);
                    }
                }
                else if (session.SoCEnd.HasValue)
                {
                    batteryIncrease = session.SoCEnd.Value - session.SoCStart.Value;
                    result.LimitStatus.BatteryIncrease = Math.Round(batteryIncrease.Value, 1);
                }
            }

            // Check Energy Limit
            if (session.EnergyLimit.HasValue && energyConsumed > 0)
            {
                result.LimitStatus.EnergyLimit = session.EnergyLimit.Value;
                result.LimitStatus.EnergyPercentage = Math.Round((energyConsumed / session.EnergyLimit.Value) * 100, 1);

                if (energyConsumed >= session.EnergyLimit.Value)
                {
                    result.HasViolations = true;
                    result.ViolatedLimits.Add($"Energy: {energyConsumed:F2} kWh >= {session.EnergyLimit.Value:F2} kWh limit");
                }
            }

            // Check Cost Limit
            if (session.CostLimit.HasValue && currentCost > 0)
            {
                result.LimitStatus.CostLimit = session.CostLimit.Value;
                result.LimitStatus.CostPercentage = Math.Round((currentCost / session.CostLimit.Value) * 100, 1);

                if (currentCost >= session.CostLimit.Value)
                {
                    result.HasViolations = true;
                    result.ViolatedLimits.Add($"Cost: {currentCost:F2} >= {session.CostLimit.Value:F2} limit");
                }
            }

            // Check Time Limit
            if (session.TimeLimit.HasValue)
            {
                result.LimitStatus.TimeLimit = session.TimeLimit.Value;
                result.LimitStatus.TimePercentage = Math.Round((elapsedMinutes / (double)session.TimeLimit.Value) * 100, 1);

                if (elapsedMinutes >= session.TimeLimit.Value)
                {
                    result.HasViolations = true;
                    result.ViolatedLimits.Add($"Time: {elapsedMinutes} min >= {session.TimeLimit.Value} min limit");
                }
            }

            // Check Battery Increase Limit
            if (session.BatteryIncreaseLimit.HasValue && batteryIncrease.HasValue)
            {
                result.LimitStatus.BatteryIncreaseLimit = session.BatteryIncreaseLimit.Value;
                result.LimitStatus.BatteryPercentage = Math.Round((batteryIncrease.Value / session.BatteryIncreaseLimit.Value) * 100, 1);

                if (batteryIncrease.Value >= session.BatteryIncreaseLimit.Value)
                {
                    result.HasViolations = true;
                    result.ViolatedLimits.Add($"Battery: +{batteryIncrease.Value:F1}% >= +{session.BatteryIncreaseLimit.Value:F1}% limit");
                }
            }

            return result;
        }
        #endregion

        #region Charging Estimation
        /// <summary>
        /// Get charging estimation for energy, cost, time, kilometres, and battery increase
        /// </summary>
        [HttpPost("estimate-charging")]
        public async Task<IActionResult> EstimateCharging([FromBody] ChargingEstimationRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Ok(new ChargingEstimationResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Get charging gun details
                var chargingGun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.RecId == request.ChargingGunId 
                        && g.ChargingStationId == request.ChargingStationId
                        && g.ConnectorId == request.ConnectorId
                        && g.Active == 1);

                if (chargingGun == null)
                {
                    return Ok(new ChargingEstimationResponseDto
                    {
                        Success = false,
                        Message = "Charging gun not found or inactive"
                    });
                }

                // Parse charger capacity (power output)
                if (!double.TryParse(chargingGun.PowerOutput, out double powerOutputKw) || powerOutputKw <= 0)
                {
                    return Ok(new ChargingEstimationResponseDto
                    {
                        Success = false,
                        Message = "Invalid charger power output"
                    });
                }

                // Parse tariff
                if (!double.TryParse(chargingGun.ChargerTariff, out double tariff) || tariff < 0)
                {
                    tariff = 0; // Default to 0 if not available
                }

                // Get charger type if available
                string chargerType = "Unknown";
                if (!string.IsNullOrEmpty(chargingGun.ChargerTypeId))
                {
                    var chargerTypeMaster = await _dbContext.ChargerTypeMasters
                        .FirstOrDefaultAsync(ct => ct.RecId == chargingGun.ChargerTypeId && ct.Active == 1);
                    if (chargerTypeMaster != null)
                    {
                        chargerType = chargerTypeMaster.ChargerType;
                    }
                }

                // Default car assumptions (typical electric vehicle in India)
                double batteryCapacity = request.BatteryCapacity ?? 40.0; // Default 40 kWh
                double efficiencyKmPerKwh = 4.5; // Average efficiency: 4.5 km/kWh
                double chargingEfficiency = 0.90; // 90% charging efficiency (accounting for losses)

                // Adjust charging efficiency based on charger type
                if (chargerType.ToLower().Contains("dc") || chargerType.ToLower().Contains("fast"))
                {
                    chargingEfficiency = 0.87; // DC fast charging slightly less efficient
                }

                // Determine energy to be charged
                double energyKwh = 0;
                double timeHours = 0;
                
                // Priority order for energy calculation:
                // 1. Specific energy request (DesiredEnergy)
                // 2. Budget/cost-based request (DesiredCost)
                // 3. Time-based request (DesiredDuration)
                // 4. Default to 1 hour
                
                if (request.DesiredEnergy.HasValue && request.DesiredEnergy.Value > 0)
                {
                    // User specified exact energy amount
                    energyKwh = request.DesiredEnergy.Value;
                }
                else if (request.DesiredCost.HasValue && request.DesiredCost.Value > 0 && tariff > 0)
                {
                    // Calculate energy based on budget: Energy = Cost / (Tariff × 1.18)
                    // The 1.18 accounts for 18% GST on the energy cost
                    double costBeforeTax = request.DesiredCost.Value / 1.18;
                    energyKwh = costBeforeTax / tariff;
                }
                else if (request.DesiredDuration.HasValue && request.DesiredDuration.Value > 0)
                {
                    // Calculate energy based on duration: Energy = Power × Time × Efficiency
                    timeHours = request.DesiredDuration.Value / 60.0;
                    energyKwh = powerOutputKw * timeHours * chargingEfficiency;
                }
                else
                {
                    // Default: Estimate for 1 hour of charging
                    energyKwh = powerOutputKw * 1.0 * chargingEfficiency;
                }

                // Cap energy at battery capacity if battery info is provided
                if (request.CurrentBatteryPercentage.HasValue && request.CurrentBatteryPercentage.Value >= 0)
                {
                    double currentSoC = request.CurrentBatteryPercentage.Value;
                    double availableCapacity = batteryCapacity * ((100 - currentSoC) / 100.0);
                    energyKwh = Math.Min(energyKwh, availableCapacity);
                }
                else
                {
                    // If no current SoC, assume we can charge up to 80% of battery (20% to 100%)
                    double maxChargeable = batteryCapacity * 0.80;
                    energyKwh = Math.Min(energyKwh, maxChargeable);
                }

                // Calculate time required
                timeHours = energyKwh / (powerOutputKw * chargingEfficiency);
                double timeMinutes = timeHours * 60;

                // Calculate cost
                double energyCost = energyKwh * tariff;
                double taxAmount = energyCost * 0.18; // 18% GST
                double totalCostWithTax = energyCost + taxAmount;

                // Calculate kilometres that can be added
                double kilometres = energyKwh * efficiencyKmPerKwh;

                // Calculate battery increase percentage
                double batteryIncrease = (energyKwh / batteryCapacity) * 100;

                // Calculate cost per kilometre
                double costPerKm = kilometres > 0 ? totalCostWithTax / kilometres : 0;

                var response = new ChargingEstimationResponseDto
                {
                    Success = true,
                    Message = "Estimation calculated successfully",
                    EstimatedEnergy = Math.Round(energyKwh, 2),
                    EstimatedCost = Math.Round(energyCost, 2),
                    EstimatedCostWithTax = Math.Round(totalCostWithTax, 2),
                    EstimatedTimeMinutes = Math.Round(timeMinutes, 1),
                    EstimatedTimeHours = Math.Round(timeHours, 2),
                    EstimatedKilometres = Math.Round(kilometres, 1),
                    EstimatedBatteryIncrease = Math.Round(batteryIncrease, 1),
                    Charger = new ChargerDetails
                    {
                        PowerOutput = powerOutputKw,
                        Tariff = tariff,
                        ChargerType = chargerType,
                        ConnectorId = chargingGun.ConnectorId
                    },
                    Car = new CarAssumptions
                    {
                        BatteryCapacity = batteryCapacity,
                        Efficiency = efficiencyKmPerKwh,
                        CurrentBatteryPercentage = request.CurrentBatteryPercentage,
                        ChargingEfficiency = chargingEfficiency
                    },
                    CostDetails = new CostBreakdown
                    {
                        EnergyCost = Math.Round(energyCost, 2),
                        TaxAmount = Math.Round(taxAmount, 2),
                        TotalCost = Math.Round(totalCostWithTax, 2),
                        CostPerKm = Math.Round(costPerKm, 2),
                        TariffApplied = tariff,
                        Currency = "₹"
                    }
                };

                _logger.LogInformation($"Charging estimation calculated: {energyKwh:F2} kWh, ₹{totalCostWithTax:F2}, {timeMinutes:F1} min, {kilometres:F1} km, +{batteryIncrease:F1}%");

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating charging estimation");
                return Ok(new ChargingEstimationResponseDto
                {
                    Success = false,
                    Message = $"Error calculating estimation: {ex.Message}"
                });
            }
        }

        #endregion
    }
}
