using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.TransactionMonitor;
using OCPP.Core.Management.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    /// <summary>
    /// Admin monitoring of OCPP transactions, with focus on stale transactions
    /// (open in the DB although the charger is no longer running them).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Administrator,Admin")]
    public class TransactionMonitorController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<TransactionMonitorController> _logger;
        private readonly IConfiguration _config;

        public TransactionMonitorController(
            OCPPCoreContext dbContext,
            ILogger<TransactionMonitorController> logger,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
        }

        private int StaleThresholdMinutes => _config.GetValue<int>("SessionLimits:StaleTransactionThresholdMinutes", 30);
        private int OrphanGraceMinutes => _config.GetValue<int>("SessionLimits:OrphanTransactionGraceMinutes", 5);
        private int LongRunningHours => _config.GetValue<int>("SessionLimits:LongRunningTransactionHours", 24);

        /// <summary>
        /// Counts of open / stale transactions by reason
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var openTransactions = await _dbContext.Transactions
                    .AsNoTracking()
                    .Where(t => !t.StopTime.HasValue)
                    .ToListAsync();

                var items = await EnrichAsync(openTransactions);
                var since = DateTime.UtcNow.AddHours(-24);
                var systemReasons = new[] { TransactionStopReasons.StaleSuperseded, TransactionStopReasons.StaleAutoClosed, TransactionStopReasons.AdminForceClosed };

                var summary = new TransactionMonitorSummaryDto
                {
                    OpenTransactions = items.Count,
                    StaleTransactions = items.Count(i => i.IsStale),
                    Superseded = items.Count(i => i.StaleReasons.Contains("Superseded")),
                    SessionEnded = items.Count(i => i.StaleReasons.Contains("SessionEnded")),
                    NoSession = items.Count(i => i.StaleReasons.Contains("NoSession")),
                    NoMeterUpdates = items.Count(i => i.StaleReasons.Contains("NoMeterUpdates")),
                    LongRunning = items.Count(i => i.StaleReasons.Contains("LongRunning")),
                    SystemClosedLast24h = await _dbContext.Transactions
                        .CountAsync(t => t.StopTime >= since && systemReasons.Contains(t.StopReason)),
                    StaleThresholdMinutes = StaleThresholdMinutes,
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(new TransactionMonitorResponseDto { Success = true, Message = "OK", Data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionMonitor: error building summary");
                return Ok(new TransactionMonitorResponseDto { Success = false, Message = "An error occurred while loading the transaction summary" });
            }
        }

        /// <summary>
        /// Paged transaction list. status: open | stale | system-closed | closed | all
        /// </summary>
        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] TransactionMonitorListRequestDto request)
        {
            try
            {
                int pageNumber = Math.Max(1, request.PageNumber);
                int pageSize = Math.Clamp(request.PageSize, 1, 200);
                string status = (request.Status ?? "open").Trim().ToLowerInvariant();

                IQueryable<Transaction> query = _dbContext.Transactions.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(request.ChargePointId))
                {
                    query = query.Where(t => t.ChargePointId == request.ChargePointId);
                }

                if (!string.IsNullOrWhiteSpace(request.Search))
                {
                    string s = request.Search.Trim();
                    int.TryParse(s, out int searchTxId);
                    query = query.Where(t => t.TransactionId == searchTxId ||
                                             t.ChargePointId.Contains(s) ||
                                             t.StartTagId.Contains(s));
                }

                var systemReasons = new[] { TransactionStopReasons.StaleSuperseded, TransactionStopReasons.StaleAutoClosed, TransactionStopReasons.AdminForceClosed };
                switch (status)
                {
                    case "open":
                    case "stale":
                        query = query.Where(t => !t.StopTime.HasValue);
                        break;
                    case "closed":
                        query = query.Where(t => t.StopTime.HasValue);
                        break;
                    case "system-closed":
                        query = query.Where(t => t.StopTime.HasValue && systemReasons.Contains(t.StopReason));
                        break;
                }

                List<TransactionMonitorItemDto> pageItems;
                int totalCount;

                if (status == "stale")
                {
                    // Staleness is derived from several tables => evaluate in memory (open transactions are few)
                    var open = await query.ToListAsync();
                    var stale = (await EnrichAsync(open))
                        .Where(i => i.IsStale)
                        .OrderBy(i => i.StartTime)
                        .ToList();
                    totalCount = stale.Count;
                    pageItems = stale.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
                }
                else
                {
                    totalCount = await query.CountAsync();
                    var page = await query
                        .OrderByDescending(t => t.TransactionId)
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();
                    pageItems = await EnrichAsync(page);
                }

                return Ok(new TransactionMonitorResponseDto
                {
                    Success = true,
                    Message = "OK",
                    Data = new
                    {
                        Items = pageItems,
                        TotalCount = totalCount,
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionMonitor: error loading list");
                return Ok(new TransactionMonitorResponseDto { Success = false, Message = "An error occurred while loading transactions" });
            }
        }

        /// <summary>
        /// Single transaction with its stale analysis
        /// </summary>
        [HttpGet("{transactionId:int}")]
        public async Task<IActionResult> GetTransaction(int transactionId)
        {
            try
            {
                var transaction = await _dbContext.Transactions.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
                if (transaction == null)
                {
                    return Ok(new TransactionMonitorResponseDto { Success = false, Message = "Transaction not found" });
                }

                var item = (await EnrichAsync(new List<Transaction> { transaction })).First();
                return Ok(new TransactionMonitorResponseDto { Success = true, Message = "OK", Data = item });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionMonitor: error loading transaction {TransactionId}", transactionId);
                return Ok(new TransactionMonitorResponseDto { Success = false, Message = "An error occurred while loading the transaction" });
            }
        }

        /// <summary>
        /// Sends a remote stop for exactly this transaction to the charger.
        /// The OCPP server refuses (status Superseded) if a newer transaction runs on the connector.
        /// </summary>
        [HttpPost("{transactionId:int}/remote-stop")]
        public async Task<IActionResult> RemoteStop(int transactionId)
        {
            try
            {
                var transaction = await _dbContext.Transactions.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
                if (transaction == null)
                {
                    return Ok(new TransactionMonitorResponseDto { Success = false, Message = "Transaction not found" });
                }
                if (transaction.StopTime.HasValue)
                {
                    return Ok(new TransactionMonitorResponseDto { Success = false, Message = "Transaction is already closed" });
                }

                var result = await OcppStopTransactionClient.StopAsync(_config, _logger,
                    transaction.ChargePointId, transaction.ConnectorId, transaction.TransactionId);

                _logger.LogInformation("TransactionMonitor: admin {AdminId} remote-stop of transaction {TransactionId} => {Status}",
                    CurrentUserId, transactionId, result.Status);

                return Ok(new TransactionMonitorResponseDto
                {
                    Success = result.Success,
                    Message = result.Success
                        ? "Stop accepted by the charger. The transaction closes when the charger reports StopTransaction."
                        : $"{result.Message}. If the charger cannot stop it, use Force close.",
                    Data = new { result.Status }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionMonitor: error on remote stop of {TransactionId}", transactionId);
                return Ok(new TransactionMonitorResponseDto { Success = false, Message = "An error occurred while stopping the transaction" });
            }
        }

        /// <summary>
        /// Closes the transaction in the database only (no message to the charger).
        /// A linked app session that is still active is closed and billed by the session
        /// monitor on its next run (it sees the transaction's StopTime).
        /// </summary>
        [HttpPost("{transactionId:int}/force-close")]
        public async Task<IActionResult> ForceClose(int transactionId, [FromBody] TransactionForceCloseRequestDto request)
        {
            try
            {
                var transaction = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
                if (transaction == null)
                {
                    return Ok(new TransactionMonitorResponseDto { Success = false, Message = "Transaction not found" });
                }
                if (transaction.StopTime.HasValue)
                {
                    return Ok(new TransactionMonitorResponseDto { Success = false, Message = "Transaction is already closed" });
                }

                double meterStop;
                if (request?.MeterStop.HasValue == true)
                {
                    meterStop = request.MeterStop.Value;
                }
                else
                {
                    // Superseded => the next transaction's start meter; otherwise the connector's last meter
                    var next = await _dbContext.Transactions.AsNoTracking()
                        .Where(t => t.ChargePointId == transaction.ChargePointId &&
                                    t.ConnectorId == transaction.ConnectorId &&
                                    t.TransactionId > transaction.TransactionId)
                        .OrderBy(t => t.TransactionId)
                        .FirstOrDefaultAsync();

                    if (next != null)
                    {
                        meterStop = next.MeterStart;
                    }
                    else
                    {
                        var connector = await _dbContext.ConnectorStatuses.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.ChargePointId == transaction.ChargePointId && c.ConnectorId == transaction.ConnectorId);
                        meterStop = connector?.LastMeter ?? transaction.MeterStart;
                    }
                }

                if (meterStop < transaction.MeterStart)
                {
                    meterStop = transaction.MeterStart;
                }

                transaction.StopTime = DateTime.UtcNow;
                transaction.MeterStop = meterStop;
                transaction.StopReason = TransactionStopReasons.AdminForceClosed;
                transaction.StopTagId = null;
                await _dbContext.SaveChangesAsync();

                _logger.LogWarning("TransactionMonitor: admin {AdminId} force-closed transaction {TransactionId} at {ChargePointId}/{ConnectorId} (meter {MeterStart:F3} -> {MeterStop:F3}). Note: {Note}",
                    CurrentUserId, transactionId, transaction.ChargePointId, transaction.ConnectorId, transaction.MeterStart, meterStop, request?.Note);

                return Ok(new TransactionMonitorResponseDto
                {
                    Success = true,
                    Message = "Transaction closed. Any linked active session will be closed and billed on the next session check.",
                    Data = new { transaction.TransactionId, transaction.StopTime, transaction.MeterStop }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransactionMonitor: error force-closing {TransactionId}", transactionId);
                return Ok(new TransactionMonitorResponseDto { Success = false, Message = "An error occurred while closing the transaction" });
            }
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// Adds station / session / user / connector data and the stale analysis to raw transactions.
        /// </summary>
        private async Task<List<TransactionMonitorItemDto>> EnrichAsync(List<Transaction> transactions)
        {
            var result = new List<TransactionMonitorItemDto>();
            if (transactions.Count == 0) return result;

            var now = DateTime.UtcNow;
            var txIds = transactions.Select(t => t.TransactionId).ToList();
            var cpIds = transactions.Select(t => t.ChargePointId).Distinct().ToList();

            var chargePoints = await _dbContext.ChargePoints.AsNoTracking()
                .Where(cp => cpIds.Contains(cp.ChargePointId))
                .ToDictionaryAsync(cp => cp.ChargePointId, cp => cp.Name);

            var stations = (await _dbContext.ChargingStations.AsNoTracking()
                    .Where(s => cpIds.Contains(s.ChargingPointId))
                    .ToListAsync())
                .GroupBy(s => s.ChargingPointId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.Active).First());

            var hubIds = stations.Values.Select(s => s.ChargingHubId).Distinct().ToList();
            var hubs = await _dbContext.ChargingHubs.AsNoTracking()
                .Where(h => hubIds.Contains(h.RecId))
                .ToDictionaryAsync(h => h.RecId, h => h.ChargingHubName);

            var connectors = (await _dbContext.ConnectorStatuses.AsNoTracking()
                    .Where(c => cpIds.Contains(c.ChargePointId))
                    .ToListAsync())
                .ToDictionary(c => $"{c.ChargePointId}|{c.ConnectorId}");

            var sessions = (await _dbContext.ChargingSessions.AsNoTracking()
                    .Where(s => s.TransactionId.HasValue && txIds.Contains(s.TransactionId.Value))
                    .ToListAsync())
                .GroupBy(s => s.TransactionId.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedOn).First());

            var userIds = sessions.Values.Select(s => s.UserId).Distinct().ToList();
            var users = await _dbContext.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.RecId))
                .ToDictionaryAsync(u => u.RecId, u => u);

            // Latest transaction per connector => anything older that's still open is superseded
            var latestPerConnector = (await _dbContext.Transactions.AsNoTracking()
                    .Where(t => cpIds.Contains(t.ChargePointId))
                    .GroupBy(t => new { t.ChargePointId, t.ConnectorId })
                    .Select(g => new { g.Key.ChargePointId, g.Key.ConnectorId, MaxId = g.Max(t => t.TransactionId) })
                    .ToListAsync())
                .ToDictionary(x => $"{x.ChargePointId}|{x.ConnectorId}", x => x.MaxId);

            // OCPI-hosted (roaming) transactions legitimately have no app session
            var ocpiActive = await _dbContext.OcpiHostedSessions.AsNoTracking()
                .Where(s => s.Status == "ACTIVE" && cpIds.Contains(s.ChargePointId))
                .Select(s => new { s.TransactionId, s.ChargePointId, s.ConnectorNumber })
                .ToListAsync();

            int staleMinutes = StaleThresholdMinutes;
            int graceMinutes = OrphanGraceMinutes;
            int longRunningHours = LongRunningHours;

            foreach (var t in transactions)
            {
                string connKey = $"{t.ChargePointId}|{t.ConnectorId}";
                stations.TryGetValue(t.ChargePointId, out var station);
                connectors.TryGetValue(connKey, out var connector);
                sessions.TryGetValue(t.TransactionId, out var session);
                Database.EVCDTO.Users user = null;
                if (session != null) users.TryGetValue(session.UserId, out user);

                bool isOpen = !t.StopTime.HasValue;
                DateTime end = t.StopTime ?? now;

                double energy;
                if (!isOpen)
                {
                    energy = Math.Max(0, (t.MeterStop ?? t.MeterStart) - t.MeterStart);
                }
                else
                {
                    energy = connector?.LastMeter.HasValue == true ? Math.Max(0, connector.LastMeter.Value - t.MeterStart) : 0;
                }

                var item = new TransactionMonitorItemDto
                {
                    TransactionId = t.TransactionId,
                    Uid = t.Uid,
                    ChargePointId = t.ChargePointId,
                    ChargePointName = chargePoints.TryGetValue(t.ChargePointId, out var cpName) ? cpName : null,
                    ConnectorId = t.ConnectorId,
                    ChargingStationId = station?.RecId,
                    ChargingHubId = station?.ChargingHubId,
                    ChargingHubName = station != null && station.ChargingHubId != null && hubs.TryGetValue(station.ChargingHubId, out var hubName) ? hubName : null,
                    StartTagId = t.StartTagId,
                    StartTime = t.StartTime,
                    MeterStart = t.MeterStart,
                    StopTime = t.StopTime,
                    MeterStop = t.MeterStop,
                    StopReason = t.StopReason,
                    IsOpen = isOpen,
                    ClosedBySystem = TransactionStopReasons.IsSystemClosed(t.StopReason),
                    DurationMinutes = Math.Round(Math.Max(0, (end - t.StartTime).TotalMinutes), 1),
                    EnergyKwh = Math.Round(energy, 3),
                    ConnectorStatus = connector?.LastStatus,
                    ConnectorLastMeter = connector?.LastMeter,
                    ConnectorLastMeterTime = connector?.LastMeterTime,
                    MinutesSinceLastMeter = connector?.LastMeterTime.HasValue == true
                        ? Math.Round((now - connector.LastMeterTime.Value).TotalMinutes, 1)
                        : null,
                    SessionId = session?.RecId,
                    SessionActive = session == null ? null : session.Active == 1 && session.EndTime == DateTime.MinValue,
                    SessionEndTime = session == null || session.EndTime == DateTime.MinValue ? null : session.EndTime,
                    UserId = session?.UserId,
                    UserName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null,
                    UserPhone = user?.PhoneNumber,
                    IsOcpiSession = ocpiActive.Any(o => o.TransactionId == t.TransactionId ||
                                                        (o.ChargePointId == t.ChargePointId && o.ConnectorNumber == t.ConnectorId))
                };

                if (isOpen)
                {
                    if (latestPerConnector.TryGetValue(connKey, out int maxId) && maxId > t.TransactionId)
                    {
                        item.SupersededByTransactionId = maxId;
                        item.StaleReasons.Add("Superseded");
                    }

                    if (item.SessionEndTime.HasValue && item.SessionEndTime.Value <= now.AddMinutes(-1))
                    {
                        item.StaleReasons.Add("SessionEnded");
                    }

                    if (session == null && !item.IsOcpiSession && t.StartTime <= now.AddMinutes(-graceMinutes))
                    {
                        item.StaleReasons.Add("NoSession");
                    }

                    // No meter data from this connector for a while (or ever, after the threshold)
                    DateTime lastSignal = connector?.LastMeterTime ?? t.StartTime;
                    if (lastSignal < t.StartTime) lastSignal = t.StartTime;
                    if (lastSignal <= now.AddMinutes(-staleMinutes))
                    {
                        item.StaleReasons.Add("NoMeterUpdates");
                    }

                    if (t.StartTime <= now.AddHours(-longRunningHours))
                    {
                        item.StaleReasons.Add("LongRunning");
                    }

                    item.IsStale = item.StaleReasons.Count > 0;
                }

                result.Add(item);
            }

            return result;
        }
    }
}
