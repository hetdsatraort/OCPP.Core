using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Models.Reports;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(OCPPCoreContext dbContext, ILogger<ReportsController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // 1. SUMMARY  GET /api/Reports/summary
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Dashboard KPIs: totals and averages across energy, revenue, time and SoC.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetReportSummary(
            [FromQuery] string hubId = null,
            [FromQuery] string stationId = null,
            [FromQuery] string gunId = null,
            [FromQuery] string userId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                EnforceUserScope(ref userId);

                var query = await BuildBaseQueryAsync(hubId, stationId, gunId, userId, from, to, null);
                var sessions = await query.ToListAsync();

                var completed = sessions.Where(s => s.EndTime != DateTime.MinValue).ToList();
                var active    = sessions.Where(s => s.EndTime == DateTime.MinValue).ToList();

                ComputeAggregates(sessions, out double totalEnergy, out double totalRevenue,
                    out double totalDurationMin, out double avgSoCGain, out int socSessions);

                double totalHours = totalDurationMin / 60.0;
                double avgSpeed   = totalHours > 0 ? totalEnergy / totalHours : 0;

                int peakHour = sessions.Count > 0
                    ? sessions.GroupBy(s => s.StartTime.Hour)
                              .OrderByDescending(g => g.Count())
                              .First().Key
                    : -1;

                string rangeFrom = from?.ToString("yyyy-MM-dd")
                    ?? (sessions.Count > 0 ? sessions.Min(s => s.StartTime).ToString("yyyy-MM-dd") : null);
                string rangeTo   = to?.ToString("yyyy-MM-dd")
                    ?? (sessions.Count > 0 ? sessions.Max(s => s.StartTime).ToString("yyyy-MM-dd") : null);

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = "Summary retrieved successfully",
                    Data = new ReportSummaryDto
                    {
                        TotalSessions              = sessions.Count,
                        CompletedSessions          = completed.Count,
                        ActiveSessions             = active.Count,
                        TotalEnergyKwh             = Math.Round(totalEnergy, 3),
                        TotalRevenueInr            = Math.Round(totalRevenue, 2),
                        TotalChargingTimeHours     = Math.Round(totalHours, 2),
                        AvgSessionDurationMinutes  = sessions.Count > 0 ? Math.Round(totalDurationMin / sessions.Count, 1) : 0,
                        AvgEnergyPerSessionKwh     = sessions.Count > 0 ? Math.Round(totalEnergy / sessions.Count, 3) : 0,
                        AvgRevenuePerSessionInr    = sessions.Count > 0 ? Math.Round(totalRevenue / sessions.Count, 2) : 0,
                        AvgChargingSpeedKw         = Math.Round(avgSpeed, 2),
                        AvgSocGainPercent          = socSessions > 0 ? Math.Round(avgSoCGain, 1) : 0,
                        SessionsWithSocData        = socSessions,
                        PeakHourOfDay              = peakHour,
                        DateRangeFrom              = rangeFrom,
                        DateRangeTo                = rangeTo
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report summary");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error generating report summary" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. SESSION LIST  GET /api/Reports/sessions
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Paginated, fully enriched session list for an interactive table.
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessionReport(
            [FromQuery] string hubId = null,
            [FromQuery] string stationId = null,
            [FromQuery] string gunId = null,
            [FromQuery] string userId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                EnforceUserScope(ref userId);

                var query = await BuildBaseQueryAsync(hubId, stationId, gunId, userId, from, to, status);
                int totalRecords = await query.CountAsync();

                var sessions = await query
                    .OrderByDescending(s => s.StartTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var lookups = await LoadEnrichmentLookupsAsync(sessions);
                var rows    = sessions.Select(s => BuildReportRow(s, lookups)).ToList();

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = "Sessions retrieved successfully",
                    Data = new
                    {
                        TotalRecords = totalRecords,
                        Page         = page,
                        PageSize     = pageSize,
                        TotalPages   = (int)Math.Ceiling((double)totalRecords / pageSize),
                        Sessions     = rows
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session report");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error retrieving session report" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. ENERGY TREND  GET /api/Reports/energy-trend
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Time-series chart data bucketed by hour / day (default) / week / month.
        /// </summary>
        [HttpGet("energy-trend")]
        public async Task<IActionResult> GetEnergyTrend(
            [FromQuery] string hubId = null,
            [FromQuery] string stationId = null,
            [FromQuery] string gunId = null,
            [FromQuery] string userId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string granularity = "day")
        {
            try
            {
                EnforceUserScope(ref userId);

                var query = await BuildBaseQueryAsync(hubId, stationId, gunId, userId, from, to, null);

                // Project minimal fields needed for aggregation
                var raw = await query
                    .Select(s => new
                    {
                        s.StartTime,
                        s.EndTime,
                        s.EnergyTransmitted,
                        s.ChargingTotalFee
                    })
                    .ToListAsync();

                var groups = raw
                    .GroupBy(s => GetTrendPeriodKey(s.StartTime, granularity))
                    .OrderBy(g => g.Key.Start)
                    .Select(g =>
                    {
                        double energy = 0, revenue = 0, durationMin = 0;
                        int completed = 0;

                        foreach (var s in g)
                        {
                            if (double.TryParse(s.EnergyTransmitted, out double e)) energy  += e;
                            if (double.TryParse(s.ChargingTotalFee,  out double r)) revenue += r;

                            bool isActive = s.EndTime == DateTime.MinValue;
                            var dur = isActive ? DateTime.UtcNow - s.StartTime : s.EndTime - s.StartTime;
                            if (dur.TotalSeconds > 0) durationMin += dur.TotalMinutes;
                            if (!isActive) completed++;
                        }

                        int cnt       = g.Count();
                        double hours  = durationMin / 60.0;
                        double speed  = hours > 0 ? energy / hours : 0;

                        return new TrendPointDto
                        {
                            Period             = g.Key.Label,
                            PeriodStart        = g.Key.Start,
                            TotalSessions      = cnt,
                            CompletedSessions  = completed,
                            TotalEnergyKwh     = Math.Round(energy, 3),
                            TotalRevenueInr    = Math.Round(revenue, 2),
                            AvgDurationMinutes = cnt > 0 ? Math.Round(durationMin / cnt, 1) : 0,
                            AvgEnergyKwh       = cnt > 0 ? Math.Round(energy / cnt, 3) : 0,
                            AvgChargingSpeedKw = Math.Round(speed, 2)
                        };
                    })
                    .ToList();

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = $"Energy trend retrieved ({granularity} granularity)",
                    Data = new
                    {
                        Granularity = granularity,
                        Points      = groups
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating energy trend");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error generating energy trend" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. HUB PERFORMANCE  GET /api/Reports/hub-performance
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Per-hub breakdown: all active hubs, with session stats for the queried period.
        /// </summary>
        [HttpGet("hub-performance")]
        public async Task<IActionResult> GetHubPerformance(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                EnforceAdminOnly();

                // Load infrastructure
                var allHubs     = await _dbContext.ChargingHubs.Where(h => h.Active == 1).ToListAsync();
                var allStations = await _dbContext.ChargingStations.Where(cs => cs.Active == 1).ToListAsync();
                var allGuns     = await _dbContext.ChargingGuns.Where(g => g.Active == 1).ToListAsync();

                var stationsByHub = allStations.GroupBy(s => s.ChargingHubId)
                                               .ToDictionary(g => g.Key, g => g.ToList());
                var gunsByHub     = allGuns.GroupBy(g => g.ChargingHubId)
                                           .ToDictionary(g => g.Key, g => g.ToList());

                // Sessions for the period (no hub/station/user filter — we show all hubs)
                var sessionQuery = _dbContext.ChargingSessions.Where(s => s.Active == 1).AsQueryable();
                if (from.HasValue) sessionQuery = sessionQuery.Where(s => s.StartTime >= from.Value);
                if (to.HasValue)   sessionQuery = sessionQuery.Where(s => s.StartTime < to.Value.Date.AddDays(1));

                var allSessions = await sessionQuery.ToListAsync();

                // Map station → hub
                var stationHubMap = allStations.ToDictionary(s => s.RecId, s => s.ChargingHubId);
                var sessionsByHub = allSessions
                    .GroupBy(s => stationHubMap.TryGetValue(s.ChargingStationID, out var hid) ? hid : "")
                    .ToDictionary(g => g.Key, g => g.ToList());

                var result = allHubs.Select(hub =>
                {
                    var hubSessions  = sessionsByHub.TryGetValue(hub.RecId, out var hs) ? hs : new List<ChargingSession>();
                    var completed    = hubSessions.Where(s => s.EndTime != DateTime.MinValue).ToList();
                    var active       = hubSessions.Where(s => s.EndTime == DateTime.MinValue).ToList();
                    int stationCount = stationsByHub.TryGetValue(hub.RecId, out var sts) ? sts.Count : 0;
                    int gunCount     = gunsByHub.TryGetValue(hub.RecId, out var gns) ? gns.Count : 0;

                    ComputeAggregates(hubSessions, out double energy, out double revenue,
                        out double durationMin, out _, out _);

                    double hours = durationMin / 60.0;

                    return new HubPerformanceDto
                    {
                        HubId                     = hub.RecId,
                        HubName                   = hub.ChargingHubName,
                        City                      = hub.City,
                        State                     = hub.State,
                        TotalSessions             = hubSessions.Count,
                        CompletedSessions         = completed.Count,
                        ActiveSessions            = active.Count,
                        TotalEnergyKwh            = Math.Round(energy, 3),
                        TotalRevenueInr           = Math.Round(revenue, 2),
                        AvgSessionDurationMinutes = hubSessions.Count > 0 ? Math.Round(durationMin / hubSessions.Count, 1) : 0,
                        AvgEnergyPerSessionKwh    = hubSessions.Count > 0 ? Math.Round(energy / hubSessions.Count, 3) : 0,
                        AvgChargingSpeedKw        = hours > 0 ? Math.Round(energy / hours, 2) : 0,
                        TotalStations             = stationCount,
                        TotalGuns                 = gunCount
                    };
                })
                .OrderByDescending(h => h.TotalSessions)
                .ToList();

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = $"Hub performance retrieved for {result.Count} hub(s)",
                    Data    = result
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating hub performance report");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error generating hub performance report" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. STATION PERFORMANCE  GET /api/Reports/station-performance
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Per-station breakdown, optionally scoped to a single hub.
        /// </summary>
        [HttpGet("station-performance")]
        public async Task<IActionResult> GetStationPerformance(
            [FromQuery] string hubId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                EnforceAdminOnly();

                var stationQuery = _dbContext.ChargingStations.Where(cs => cs.Active == 1).AsQueryable();
                if (!string.IsNullOrEmpty(hubId))
                    stationQuery = stationQuery.Where(cs => cs.ChargingHubId == hubId);

                var stations    = await stationQuery.ToListAsync();
                var stationIds  = stations.Select(s => s.RecId).ToList();

                var hubIds   = stations.Select(s => s.ChargingHubId).Distinct().ToList();
                var hubs     = await _dbContext.ChargingHubs
                                   .Where(h => hubIds.Contains(h.RecId))
                                   .ToDictionaryAsync(h => h.RecId);

                var allGuns  = await _dbContext.ChargingGuns
                                   .Where(g => stationIds.Contains(g.ChargingStationId) && g.Active == 1)
                                   .ToListAsync();
                var gunCountByStation = allGuns.GroupBy(g => g.ChargingStationId)
                                               .ToDictionary(g => g.Key, g => g.Count());

                var sessionQuery = _dbContext.ChargingSessions
                    .Where(s => s.Active == 1 && stationIds.Contains(s.ChargingStationID))
                    .AsQueryable();
                if (from.HasValue) sessionQuery = sessionQuery.Where(s => s.StartTime >= from.Value);
                if (to.HasValue)   sessionQuery = sessionQuery.Where(s => s.StartTime < to.Value.Date.AddDays(1));

                var allSessions      = await sessionQuery.ToListAsync();
                var sessionsByStation = allSessions.GroupBy(s => s.ChargingStationID)
                                                   .ToDictionary(g => g.Key, g => g.ToList());

                var result = stations.Select(st =>
                {
                    var stSessions = sessionsByStation.TryGetValue(st.RecId, out var ss) ? ss : new List<ChargingSession>();
                    var completed  = stSessions.Where(s => s.EndTime != DateTime.MinValue).ToList();
                    var active     = stSessions.Where(s => s.EndTime == DateTime.MinValue).ToList();
                    hubs.TryGetValue(st.ChargingHubId, out var hub);
                    int gunCount   = gunCountByStation.TryGetValue(st.RecId, out var gc) ? gc : 0;

                    ComputeAggregates(stSessions, out double energy, out double revenue,
                        out double durationMin, out _, out _);

                    double hours = durationMin / 60.0;

                    return new StationPerformanceDto
                    {
                        StationRecId              = st.RecId,
                        ChargePointId             = st.ChargingPointId,
                        HubId                     = st.ChargingHubId,
                        HubName                   = hub?.ChargingHubName,
                        HubCity                   = hub?.City,
                        TotalSessions             = stSessions.Count,
                        CompletedSessions         = completed.Count,
                        ActiveSessions            = active.Count,
                        TotalEnergyKwh            = Math.Round(energy, 3),
                        TotalRevenueInr           = Math.Round(revenue, 2),
                        AvgSessionDurationMinutes = stSessions.Count > 0 ? Math.Round(durationMin / stSessions.Count, 1) : 0,
                        AvgEnergyPerSessionKwh    = stSessions.Count > 0 ? Math.Round(energy / stSessions.Count, 3) : 0,
                        AvgChargingSpeedKw        = hours > 0 ? Math.Round(energy / hours, 2) : 0,
                        GunCount                  = gunCount
                    };
                })
                .OrderByDescending(s => s.TotalSessions)
                .ToList();

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = $"Station performance retrieved for {result.Count} station(s)",
                    Data    = result
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating station performance report");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error generating station performance report" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. GUN UTILIZATION  GET /api/Reports/gun-utilization
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Per-gun utilization, optionally scoped to a single station.
        /// Utilization % = total charging hours ÷ period window hours × 100.
        /// </summary>
        [HttpGet("gun-utilization")]
        public async Task<IActionResult> GetGunUtilization(
            [FromQuery] string stationId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            try
            {
                EnforceAdminOnly();

                var gunQuery = _dbContext.ChargingGuns.Where(g => g.Active == 1).AsQueryable();
                if (!string.IsNullOrEmpty(stationId))
                    gunQuery = gunQuery.Where(g => g.ChargingStationId == stationId);

                var guns       = await gunQuery.ToListAsync();
                var stationIds = guns.Select(g => g.ChargingStationId).Distinct().ToList();

                var stations = await _dbContext.ChargingStations
                                   .Where(cs => stationIds.Contains(cs.RecId))
                                   .ToDictionaryAsync(cs => cs.RecId);

                var hubIds = stations.Values.Select(s => s.ChargingHubId).Distinct().ToList();
                var hubs   = await _dbContext.ChargingHubs
                                 .Where(h => hubIds.Contains(h.RecId))
                                 .ToDictionaryAsync(h => h.RecId);

                var chargerTypeIds = guns.Select(g => g.ChargerTypeId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
                var chargerTypes   = await _dbContext.ChargerTypeMasters
                                         .Where(ct => chargerTypeIds.Contains(ct.RecId))
                                         .ToDictionaryAsync(ct => ct.RecId);

                var sessionQuery = _dbContext.ChargingSessions
                    .Where(s => s.Active == 1 && stationIds.Contains(s.ChargingStationID))
                    .AsQueryable();
                if (from.HasValue) sessionQuery = sessionQuery.Where(s => s.StartTime >= from.Value);
                if (to.HasValue)   sessionQuery = sessionQuery.Where(s => s.StartTime < to.Value.Date.AddDays(1));

                var allSessions    = await sessionQuery.ToListAsync();
                var sessionsByGun  = allSessions.GroupBy(s => s.ChargingGunId)
                                                .ToDictionary(g => g.Key, g => g.ToList());

                // Period window for utilisation calculation
                DateTime periodStart  = from ?? (allSessions.Count > 0 ? allSessions.Min(s => s.StartTime) : DateTime.UtcNow.AddDays(-30));
                DateTime periodEnd    = to?.Date.AddDays(1) ?? DateTime.UtcNow;
                double periodHours    = Math.Max(1, (periodEnd - periodStart).TotalHours);

                var result = guns.Select(gun =>
                {
                    var gunSessions   = sessionsByGun.TryGetValue(gun.RecId, out var gs) ? gs : new List<ChargingSession>();
                    var completed     = gunSessions.Where(s => s.EndTime != DateTime.MinValue).ToList();
                    var active        = gunSessions.Where(s => s.EndTime == DateTime.MinValue).ToList();
                    stations.TryGetValue(gun.ChargingStationId, out var station);
                    hubs.TryGetValue(station?.ChargingHubId ?? "", out var hub);
                    chargerTypes.TryGetValue(gun.ChargerTypeId ?? "", out var ct);

                    ComputeAggregates(gunSessions, out double energy, out double revenue,
                        out double durationMin, out _, out _);

                    double chargingHours = durationMin / 60.0;
                    double utilPct       = Math.Round(Math.Min(100.0, chargingHours / periodHours * 100.0), 1);

                    return new GunUtilizationDto
                    {
                        GunId                     = gun.RecId,
                        ConnectorId               = gun.ConnectorId,
                        ChargerType               = ct?.ChargerType ?? "Standard",
                        PowerOutputKw             = gun.PowerOutput,
                        StationRecId              = gun.ChargingStationId,
                        ChargePointId             = station?.ChargingPointId,
                        HubName                   = hub?.ChargingHubName,
                        HubCity                   = hub?.City,
                        CurrentStatus             = gun.ChargerStatus,
                        TotalSessions             = gunSessions.Count,
                        CompletedSessions         = completed.Count,
                        TotalEnergyKwh            = Math.Round(energy, 3),
                        TotalRevenueInr           = Math.Round(revenue, 2),
                        TotalChargingHours        = Math.Round(chargingHours, 2),
                        AvgSessionDurationMinutes = gunSessions.Count > 0 ? Math.Round(durationMin / gunSessions.Count, 1) : 0,
                        AvgEnergyPerSessionKwh    = gunSessions.Count > 0 ? Math.Round(energy / gunSessions.Count, 3) : 0,
                        UtilizationPct            = utilPct
                    };
                })
                .OrderByDescending(g => g.UtilizationPct)
                .ToList();

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = $"Gun utilization retrieved for {result.Count} connector(s) over {Math.Round(periodHours, 0)}h window",
                    Data    = result
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating gun utilization report");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error generating gun utilization report" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. USER ACTIVITY  GET /api/Reports/user-activity  [Admin only]
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Per-user activity breakdown. Administrator only.
        /// </summary>
        [HttpGet("user-activity")]
        public async Task<IActionResult> GetUserActivity(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                EnforceAdminOnly();

                var sessionQuery = _dbContext.ChargingSessions.Where(s => s.Active == 1).AsQueryable();
                if (from.HasValue) sessionQuery = sessionQuery.Where(s => s.StartTime >= from.Value);
                if (to.HasValue)   sessionQuery = sessionQuery.Where(s => s.StartTime < to.Value.Date.AddDays(1));

                var allSessions = await sessionQuery.ToListAsync();

                // Group by user
                var sessionsByUser = allSessions
                    .GroupBy(s => s.UserId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var userIds = sessionsByUser.Keys.ToList();
                var users   = await _dbContext.Users
                                  .Where(u => userIds.Contains(u.RecId))
                                  .ToDictionaryAsync(u => u.RecId);

                // Favourite hub: most frequent hub per user
                var stationIds    = allSessions.Select(s => s.ChargingStationID).Distinct().ToList();
                var stations      = await _dbContext.ChargingStations
                                        .Where(cs => stationIds.Contains(cs.RecId))
                                        .ToDictionaryAsync(cs => cs.RecId);
                var hubIds        = stations.Values.Select(s => s.ChargingHubId).Distinct().ToList();
                var hubs          = await _dbContext.ChargingHubs
                                        .Where(h => hubIds.Contains(h.RecId))
                                        .ToDictionaryAsync(h => h.RecId);

                var userRows = sessionsByUser
                    .Select(kvp =>
                    {
                        var uid        = kvp.Key;
                        var uSessions  = kvp.Value;
                        var completed  = uSessions.Where(s => s.EndTime != DateTime.MinValue).ToList();
                        users.TryGetValue(uid, out var user);

                        ComputeAggregates(uSessions, out double energy, out double revenue,
                            out double durationMin, out _, out _);

                        // Favourite hub determination
                        string favHub = uSessions
                            .GroupBy(s =>
                            {
                                stations.TryGetValue(s.ChargingStationID, out var st);
                                hubs.TryGetValue(st?.ChargingHubId ?? "", out var h);
                                return h?.ChargingHubName ?? "";
                            })
                            .OrderByDescending(g => g.Count())
                            .Select(g => g.Key)
                            .FirstOrDefault();

                        return new UserActivityDto
                        {
                            UserId                    = uid,
                            UserName                  = user != null ? $"{user.FirstName} {user.LastName}".Trim() : uid,
                            PhoneNumber               = user?.PhoneNumber,
                            Email                     = user?.EMailID,
                            TotalSessions             = uSessions.Count,
                            CompletedSessions         = completed.Count,
                            TotalEnergyKwh            = Math.Round(energy, 3),
                            TotalSpentInr             = Math.Round(revenue, 2),
                            AvgSessionDurationMinutes = uSessions.Count > 0 ? Math.Round(durationMin / uSessions.Count, 1) : 0,
                            AvgEnergyPerSessionKwh    = uSessions.Count > 0 ? Math.Round(energy / uSessions.Count, 3) : 0,
                            LastSessionTime           = uSessions.Max(s => (DateTime?)s.StartTime),
                            FavoriteHubName           = favHub
                        };
                    })
                    .OrderByDescending(u => u.TotalSpentInr)
                    .ToList();

                int total = userRows.Count;
                var paged = userRows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new ReportResponseDto
                {
                    Success = true,
                    Message = "User activity retrieved successfully",
                    Data = new
                    {
                        TotalRecords = total,
                        Page         = page,
                        PageSize     = pageSize,
                        TotalPages   = (int)Math.Ceiling((double)total / pageSize),
                        Users        = paged
                    }
                });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating user activity report");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error generating user activity report" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. CSV EXPORT  GET /api/Reports/export
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Download all filtered sessions as a CSV file (UTF-8 BOM for Excel compatibility).
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportCsv(
            [FromQuery] string hubId = null,
            [FromQuery] string stationId = null,
            [FromQuery] string gunId = null,
            [FromQuery] string userId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string status = null)
        {
            try
            {
                EnforceUserScope(ref userId);

                var query    = await BuildBaseQueryAsync(hubId, stationId, gunId, userId, from, to, status);
                var sessions = await query.OrderByDescending(s => s.StartTime).ToListAsync();
                var lookups  = await LoadEnrichmentLookupsAsync(sessions);
                var rows     = sessions.Select(s => BuildReportRow(s, lookups));

                // UTF-8 BOM so Excel opens it correctly
                var preamble = Encoding.UTF8.GetPreamble();
                var csv      = Encoding.UTF8.GetBytes(BuildCsvContent(rows));
                var bytes    = new byte[preamble.Length + csv.Length];
                Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
                Buffer.BlockCopy(csv, 0, bytes, preamble.Length, csv.Length);

                string filename = $"charging-sessions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(bytes, "text/csv", filename);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting sessions to CSV");
                return StatusCode(500, new ReportResponseDto { Success = false, Message = "Error exporting sessions to CSV" });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the base ChargingSession query with all common filters applied.
        /// Hub filtering is resolved by looking up station IDs that belong to the hub.
        /// </summary>
        private async Task<IQueryable<ChargingSession>> BuildBaseQueryAsync(
            string hubId, string stationId, string gunId, string userId,
            DateTime? from, DateTime? to, string status)
        {
            var query = _dbContext.ChargingSessions.Where(s => s.Active == 1).AsQueryable();

            if (!string.IsNullOrEmpty(stationId))
                query = query.Where(s => s.ChargingStationID == stationId);

            if (!string.IsNullOrEmpty(gunId))
                query = query.Where(s => s.ChargingGunId == gunId);

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(s => s.UserId == userId);

            if (from.HasValue)
                query = query.Where(s => s.StartTime >= from.Value);

            if (to.HasValue)
                query = query.Where(s => s.StartTime < to.Value.Date.AddDays(1));

            if (!string.IsNullOrEmpty(status))
            {
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(s => s.EndTime == DateTime.MinValue);
                else if (status.Equals("completed", StringComparison.OrdinalIgnoreCase))
                    query = query.Where(s => s.EndTime != DateTime.MinValue);
            }

            if (!string.IsNullOrEmpty(hubId))
            {
                var hubStationIds = await _dbContext.ChargingStations
                    .Where(cs => cs.ChargingHubId == hubId && cs.Active == 1)
                    .Select(cs => cs.RecId)
                    .ToListAsync();
                query = query.Where(s => hubStationIds.Contains(s.ChargingStationID));
            }

            return query;
        }

        /// <summary>
        /// Bulk-loads all lookup data needed to enrich a list of sessions (avoids N+1).
        /// </summary>
        private async Task<EnrichmentLookups> LoadEnrichmentLookupsAsync(List<ChargingSession> sessions)
        {
            var stationIds = sessions.Select(s => s.ChargingStationID).Distinct().ToList();
            var userIds    = sessions.Select(s => s.UserId).Distinct().ToList();
            var gunIds     = sessions.Select(s => s.ChargingGunId).Distinct().ToList();

            var stations = await _dbContext.ChargingStations
                .Where(cs => stationIds.Contains(cs.RecId))
                .ToDictionaryAsync(cs => cs.RecId);

            var hubIds = stations.Values.Select(s => s.ChargingHubId).Distinct().ToList();
            var hubs   = await _dbContext.ChargingHubs
                .Where(h => hubIds.Contains(h.RecId))
                .ToDictionaryAsync(h => h.RecId);

            var guns = await _dbContext.ChargingGuns
                .Where(g => stationIds.Contains(g.ChargingStationId) && gunIds.Contains(g.RecId))
                .ToDictionaryAsync(g => g.RecId);

            var chargerTypeIds = guns.Values.Select(g => g.ChargerTypeId)
                .Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var chargerTypes = await _dbContext.ChargerTypeMasters
                .Where(ct => chargerTypeIds.Contains(ct.RecId))
                .ToDictionaryAsync(ct => ct.RecId);

            var users = await _dbContext.Users
                .Where(u => userIds.Contains(u.RecId))
                .ToDictionaryAsync(u => u.RecId);

            var allVehicles = await _dbContext.UserVehicles
                .Where(v => userIds.Contains(v.UserId) && v.Active == 1)
                .ToListAsync();

            // Default vehicle per user; fall back to most recently created
            var vehicleByUser = allVehicles
                .GroupBy(v => v.UserId)
                .ToDictionary(g => g.Key,
                    g => g.FirstOrDefault(v => v.DefaultConfig == 1)
                          ?? g.OrderByDescending(v => v.CreatedOn).FirstOrDefault());

            var modelIds = vehicleByUser.Values
                .Where(v => v != null && !string.IsNullOrEmpty(v.CarModelID))
                .Select(v => v.CarModelID).Distinct().ToList();
            var models = await _dbContext.EVModelMasters
                .Where(m => modelIds.Contains(m.RecId))
                .ToDictionaryAsync(m => m.RecId);

            var manufacturerIds = vehicleByUser.Values
                .Where(v => v != null && !string.IsNullOrEmpty(v.EVManufacturerID))
                .Select(v => v.EVManufacturerID).Distinct().ToList();
            var manufacturers = await _dbContext.CarManufacturerMasters
                .Where(m => manufacturerIds.Contains(m.RecId))
                .ToDictionaryAsync(m => m.RecId);

            var capacityIds = vehicleByUser.Values
                .Where(v => v != null && !string.IsNullOrEmpty(v.BatteryCapacityId))
                .Select(v => v.BatteryCapacityId).Distinct().ToList();
            var capacities = await _dbContext.BatteryCapacityMasters
                .Where(c => capacityIds.Contains(c.RecId))
                .ToDictionaryAsync(c => c.RecId);

            return new EnrichmentLookups
            {
                Stations     = stations,
                Hubs         = hubs,
                Guns         = guns,
                ChargerTypes = chargerTypes,
                Users        = users,
                VehicleByUser = vehicleByUser,
                Models        = models,
                Manufacturers = manufacturers,
                Capacities    = capacities
            };
        }

        private SessionReportRowDto BuildReportRow(ChargingSession s, EnrichmentLookups e)
        {
            e.Stations.TryGetValue(s.ChargingStationID, out var station);
            e.Hubs.TryGetValue(station?.ChargingHubId ?? "", out var hub);
            e.Guns.TryGetValue(s.ChargingGunId ?? "", out var gun);
            e.ChargerTypes.TryGetValue(gun?.ChargerTypeId ?? "", out var ct);
            e.Users.TryGetValue(s.UserId, out var user);
            e.VehicleByUser.TryGetValue(s.UserId, out var vehicle);
            e.Models.TryGetValue(vehicle?.CarModelID ?? "", out var model);
            e.Manufacturers.TryGetValue(vehicle?.EVManufacturerID ?? "", out var manufacturer);
            e.Capacities.TryGetValue(vehicle?.BatteryCapacityId ?? "", out var capacity);

            bool isActive  = s.EndTime == DateTime.MinValue;
            DateTime? endTime = isActive ? (DateTime?)null : s.EndTime;
            var duration   = isActive ? DateTime.UtcNow - s.StartTime : (endTime.HasValue ? endTime.Value - s.StartTime : TimeSpan.Zero);
            if (duration.TotalSeconds < 0) duration = TimeSpan.Zero;

            double.TryParse(s.StartMeterReading, out double startMeter);
            double.TryParse(s.EndMeterReading,   out double endMeter);
            double.TryParse(s.EnergyTransmitted, out double energy);
            double.TryParse(s.ChargingTariff,    out double tariff);
            double.TryParse(s.ChargingTotalFee,  out double fee);

            double? batteryKwh = null;
            if (capacity != null && double.TryParse(capacity.BatteryCapcacity, out double cap))
                batteryKwh = cap;

            double? socGain = (s.SoCStart.HasValue && s.SoCEnd.HasValue)
                ? s.SoCEnd.Value - s.SoCStart.Value
                : (double?)null;

            return new SessionReportRowDto
            {
                SessionId            = s.RecId,
                HubId                = hub?.RecId,
                HubName              = hub?.ChargingHubName,
                HubCity              = hub?.City,
                StationRecId         = station?.RecId,
                ChargePointId        = station?.ChargingPointId,
                GunId                = s.ChargingGunId,
                ConnectorId          = gun?.ConnectorId,
                ChargerType          = ct?.ChargerType ?? "Standard",
                PowerOutputKw        = gun?.PowerOutput,
                UserId               = s.UserId,
                UserName             = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null,
                UserPhone            = user?.PhoneNumber,
                UserEmail            = user?.EMailID,
                VehicleManufacturer  = manufacturer?.ManufacturerName,
                VehicleModel         = model?.ModelName,
                VehicleRegistration  = vehicle?.CarRegistrationNumber,
                BatteryCapacityKwh   = batteryKwh,
                StartTime            = s.StartTime,
                EndTime              = endTime,
                DurationMinutes      = Math.Round(duration.TotalMinutes, 1),
                StartMeterKwh        = Math.Round(startMeter, 3),
                EndMeterKwh          = Math.Round(endMeter, 3),
                EnergyKwh            = Math.Round(energy, 3),
                TariffPerKwh         = Math.Round(tariff, 2),
                TotalFeeInr          = Math.Round(fee, 2),
                SoCStartPct          = s.SoCStart.HasValue ? Math.Round(s.SoCStart.Value, 1) : (double?)null,
                SoCEndPct            = s.SoCEnd.HasValue   ? Math.Round(s.SoCEnd.Value, 1)   : (double?)null,
                SoCGainPct           = socGain.HasValue     ? Math.Round(socGain.Value, 1)    : (double?)null,
                Status               = isActive ? "Active" : "Completed",
                TransactionId        = s.TransactionId
            };
        }

        private static void ComputeAggregates(
            List<ChargingSession> sessions,
            out double totalEnergy, out double totalRevenue,
            out double totalDurationMin, out double avgSoCGain, out int socSessions)
        {
            totalEnergy = 0; totalRevenue = 0; totalDurationMin = 0;
            double totalSoCGain = 0; socSessions = 0;

            foreach (var s in sessions)
            {
                if (double.TryParse(s.EnergyTransmitted, out double e)) totalEnergy  += e;
                if (double.TryParse(s.ChargingTotalFee,  out double r)) totalRevenue += r;

                bool isActive = s.EndTime == DateTime.MinValue;
                var dur = isActive ? DateTime.UtcNow - s.StartTime : s.EndTime - s.StartTime;
                if (dur.TotalSeconds > 0) totalDurationMin += dur.TotalMinutes;

                if (s.SoCStart.HasValue && s.SoCEnd.HasValue)
                {
                    totalSoCGain += s.SoCEnd.Value - s.SoCStart.Value;
                    socSessions++;
                }
            }

            avgSoCGain = socSessions > 0 ? totalSoCGain / socSessions : 0;
        }

        private static (string Label, DateTime Start) GetTrendPeriodKey(DateTime dt, string granularity)
        {
            return granularity?.ToLowerInvariant() switch
            {
                "hour"  => ($"{dt:yyyy-MM-dd HH}:00",
                            new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc)),
                "week"  => ($"{dt:yyyy}-W{ISOWeek.GetWeekOfYear(dt):D2}",
                            ISOWeek.ToDateTime(dt.Year, ISOWeek.GetWeekOfYear(dt), DayOfWeek.Monday)),
                "month" => ($"{dt:yyyy-MM}",
                            new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc)),
                _       => ($"{dt:yyyy-MM-dd}", dt.Date)    // "day" (default)
            };
        }

        private static string BuildCsvContent(IEnumerable<SessionReportRowDto> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(
                "Session ID,Hub,City,Charge Point,Connector,Charger Type,Power Output (kW)," +
                "User Name,Phone,Email," +
                "Vehicle Manufacturer,Vehicle Model,Registration,Battery Capacity (kWh)," +
                "Start Time,End Time,Duration (min)," +
                "Start Meter (kWh),End Meter (kWh),Energy (kWh)," +
                "Tariff (INR/kWh),Total Fee (INR)," +
                "SoC Start (%),SoC End (%),SoC Gain (%)," +
                "Status,Transaction ID");

            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    CsvEscape(r.SessionId),
                    CsvEscape(r.HubName),
                    CsvEscape(r.HubCity),
                    CsvEscape(r.ChargePointId),
                    CsvEscape(r.ConnectorId),
                    CsvEscape(r.ChargerType),
                    CsvEscape(r.PowerOutputKw),
                    CsvEscape(r.UserName),
                    CsvEscape(r.UserPhone),
                    CsvEscape(r.UserEmail),
                    CsvEscape(r.VehicleManufacturer),
                    CsvEscape(r.VehicleModel),
                    CsvEscape(r.VehicleRegistration),
                    r.BatteryCapacityKwh?.ToString("F1") ?? "",
                    r.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    r.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                    r.DurationMinutes.ToString("F1"),
                    r.StartMeterKwh.ToString("F3"),
                    r.EndMeterKwh.ToString("F3"),
                    r.EnergyKwh.ToString("F3"),
                    r.TariffPerKwh.ToString("F2"),
                    r.TotalFeeInr.ToString("F2"),
                    r.SoCStartPct?.ToString("F1") ?? "",
                    r.SoCEndPct?.ToString("F1")   ?? "",
                    r.SoCGainPct?.ToString("F1")  ?? "",
                    CsvEscape(r.Status),
                    r.TransactionId?.ToString() ?? ""
                }));
            }

            return sb.ToString();
        }

        /// <summary>RFC 4180 quoting with CSV injection prevention.</summary>
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            // Prevent formula injection in Excel
            if (value[0] == '=' || value[0] == '+' || value[0] == '-' || value[0] == '@' ||
                value[0] == '\t' || value[0] == '\r')
                value = "'" + value;

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";

            return value;
        }

        /// <summary>If the caller is not an admin, restricts the userId filter to the logged-in user.</summary>
        private void EnforceUserScope(ref string userId)
        {
            if (!User.IsInRole("Administrator"))
                userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>Throws if the caller is not an administrator.</summary>
        private void EnforceAdminOnly()
        {
            if (!User.IsInRole("Administrator"))
                throw new UnauthorizedAccessException();
        }

        // ── Inner helper class for bulk lookups ───────────────────────────────
        private class EnrichmentLookups
        {
            public Dictionary<string, ChargingStation>      Stations      { get; set; }
            public Dictionary<string, ChargingHub>          Hubs          { get; set; }
            public Dictionary<string, ChargingGuns>         Guns          { get; set; }
            public Dictionary<string, ChargerTypeMaster>    ChargerTypes  { get; set; }
            public Dictionary<string, Users>                Users         { get; set; }
            public Dictionary<string, UserVehicle>          VehicleByUser { get; set; }
            public Dictionary<string, EVModelMaster>        Models        { get; set; }
            public Dictionary<string, CarManufacturerMaster> Manufacturers { get; set; }
            public Dictionary<string, BatteryCapacityMaster> Capacities   { get; set; }
        }
    }
}
