using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    /// <summary>
    /// Flat, OCPP-level "master data" view of chargepoints/connectors — distinct from the
    /// ChargingHub/Station/Gun business layer (<see cref="ChargingHubController"/>), which wraps
    /// these same entities with hub/pricing/review metadata. Operates directly on
    /// <see cref="ChargePoint"/>/<see cref="ConnectorStatus"/>/<see cref="MessageLog"/>, keyed by
    /// the raw OCPP ChargePointId, since that's what message logging and the GetConfiguration /
    /// ChangeConfiguration commands (OCPP.Core.Server, ControllerOCPP16.Configuration.cs) use.
    ///
    /// The configuration proxy actions reuse the same ServerApiUrl/ApiKey HttpClient pattern as
    /// <see cref="ApiController"/>'s Reset/UnlockConnector actions.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ChargePointMasterController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ChargePointMasterController> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public ChargePointMasterController(
            OCPPCoreContext dbContext,
            ILogger<ChargePointMasterController> logger,
            IConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        private bool IsAdmin() => User != null && User.IsInRole(Constants.AdminRoleName);

        // ── Master list ──────────────────────────────────────────────────────

        /// <summary>All registered chargepoints with a connector-count/last-activity summary.</summary>
        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> GetChargePointList()
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            try
            {
                var chargePoints = await _dbContext.ChargePoints
                    .OrderBy(c => c.Name ?? c.ChargePointId)
                    .ToListAsync();

                var connectorSummary = await _dbContext.ConnectorStatuses
                    .GroupBy(c => c.ChargePointId)
                    .Select(g => new
                    {
                        ChargePointId = g.Key,
                        Count = g.Count(),
                        LastActivity = g.Max(c => c.LastStatusTime)
                    })
                    .ToDictionaryAsync(x => x.ChargePointId, x => x);

                var result = chargePoints.Select(cp =>
                {
                    connectorSummary.TryGetValue(cp.ChargePointId, out var summary);
                    return new
                    {
                        chargePointId = cp.ChargePointId,
                        name = cp.Name,
                        comment = cp.Comment,
                        connectorCount = summary?.Count ?? 0,
                        lastActivity = summary?.LastActivity
                    };
                });

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetChargePointList: Error loading chargepoints");
                return Ok(new { success = false, message = "Error retrieving chargepoint list" });
            }
        }

        /// <summary>Connector master data (raw ConnectorStatus rows) for one chargepoint.</summary>
        [HttpGet("{chargePointId}/connectors")]
        [Authorize]
        public async Task<IActionResult> GetConnectors(string chargePointId)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            try
            {
                var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
                if (chargePoint == null)
                    return Ok(new { success = false, message = "Chargepoint not found" });

                var connectors = await _dbContext.ConnectorStatuses
                    .Where(c => c.ChargePointId == chargePointId)
                    .OrderBy(c => c.ConnectorId)
                    .Select(c => new
                    {
                        connectorId = c.ConnectorId,
                        connectorName = c.ConnectorName,
                        lastStatus = c.LastStatus,
                        lastStatusTime = c.LastStatusTime,
                        lastMeter = c.LastMeter,
                        lastMeterTime = c.LastMeterTime
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = connectors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetConnectors: Error loading connectors for '{ChargePointId}'", chargePointId);
                return Ok(new { success = false, message = "Error retrieving connectors" });
            }
        }

        /// <summary>Live reachability check — same ConnectionStatus call GunStatusSyncService uses.</summary>
        [HttpGet("{chargePointId}/online-status")]
        [Authorize]
        public async Task<IActionResult> GetOnlineStatus(string chargePointId)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
            if (chargePoint == null)
                return Ok(new { success = false, message = "Chargepoint not found" });

            // OCPP.Core.Server's in-memory chargepoint dictionary is keyed case-sensitively, unlike
            // FindAsync above (case-insensitive under SQL Server's default collation) — always use
            // the DB's own canonical casing for the outbound call, not whatever casing the caller
            // happened to pass in, or a same-chargepoint request can spuriously come back "offline".
            chargePointId = chargePoint.ChargePointId;

            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            if (string.IsNullOrEmpty(serverApiUrl))
                return Ok(new { success = false, message = "OCPP server API is not configured (ServerApiUrl missing)" });

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                var uri = new Uri(new Uri(serverApiUrl), $"ConnectionStatus/{Uri.EscapeDataString(chargePointId)}");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(5);

                string apiKeyConfig = _config.GetValue<string>("ApiKey");
                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKeyConfig);

                var response = await httpClient.GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                    return Ok(new { success = true, data = new { isOnline = false } });

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Ok(new { success = true, data = json });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetOnlineStatus: connection status check failed for '{ChargePointId}'", chargePointId);
                return Ok(new { success = true, data = new { isOnline = false } });
            }
        }

        // ── Message logs ─────────────────────────────────────────────────────

        /// <summary>
        /// Paginated OCPP message log for one chargepoint — the readable machine log of every
        /// OCPP action processed for it (BootNotification, StatusNotification, GetConfiguration,
        /// ChangeConfiguration, etc; see MessageLog / WriteMessageLog call sites throughout
        /// OCPP.Core.Server). Fields are already human-readable text, not raw payloads.
        /// </summary>
        [HttpGet("{chargePointId}/logs")]
        [Authorize]
        public async Task<IActionResult> GetLogs(
            string chargePointId,
            [FromQuery] int? connectorId = null,
            [FromQuery] List<string> messages = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? hours = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            try
            {
                var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
                if (chargePoint == null)
                    return Ok(new { success = false, message = "Chargepoint not found" });

                var query = FilterLogs(chargePointId, connectorId, messages, hours);
                if (from.HasValue)
                    query = query.Where(m => m.LogTime >= from.Value);
                if (to.HasValue)
                    query = query.Where(m => m.LogTime <= to.Value);

                var totalCount = await query.CountAsync();
                pageSize = pageSize > 0 ? pageSize : 50;
                page = page > 0 ? page : 1;

                var logs = await query
                    .OrderByDescending(m => m.LogTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(m => new
                    {
                        logTime = m.LogTime,
                        connectorId = m.ConnectorId,
                        message = m.Message,
                        result = m.Result,
                        errorCode = m.ErrorCode
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        totalCount,
                        page,
                        pageSize,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                        logs
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLogs: Error loading message logs for '{ChargePointId}'", chargePointId);
                return Ok(new { success = false, message = "Error retrieving message logs" });
            }
        }

        /// <summary>Longest window the log export accepts — 30 days.</summary>
        private const int MaxExportHours = 24 * 30;

        /// <summary>
        /// Row cap for one export. A chatty charger (MeterValues every few seconds across several
        /// connectors) can log a lot in 30 days; past this, the newest rows are kept and the Info
        /// sheet says the export was truncated.
        /// </summary>
        private const int MaxExportRows = 200_000;

        /// <summary>
        /// Excel (.xlsx) export of the message log for the last <paramref name="hours"/> hours
        /// (1..720), honouring the same connector / message-type filters as <see cref="GetLogs"/>.
        /// The window is computed here against UTC — LogTime is written with DateTime.UtcNow by
        /// OCPP.Core.Server — so the client's clock and timezone don't affect which rows are
        /// included. <paramref name="tzOffsetMinutes"/> (minutes east of UTC, same convention as
        /// ReportsController) only drives the local-time column in the sheet.
        /// </summary>
        [HttpGet("{chargePointId}/logs/export")]
        [Authorize]
        public async Task<IActionResult> ExportLogs(
            string chargePointId,
            [FromQuery] int hours = 24,
            [FromQuery] int? connectorId = null,
            [FromQuery] List<string> messages = null,
            [FromQuery] int tzOffsetMinutes = 330)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            if (hours < 1 || hours > MaxExportHours)
                return BadRequest(new { success = false, message = $"hours must be between 1 and {MaxExportHours}" });
            if (Math.Abs(tzOffsetMinutes) > 14 * 60)
                return BadRequest(new { success = false, message = "tzOffsetMinutes is out of range" });

            try
            {
                var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
                if (chargePoint == null)
                    return NotFound(new { success = false, message = "Chargepoint not found" });

                var nowUtc = DateTime.UtcNow;
                var fromUtc = nowUtc.AddHours(-hours);

                var query = FilterLogs(chargePoint.ChargePointId, connectorId, messages, hours);
                var totalCount = await query.CountAsync();
                var logs = await query
                    .OrderByDescending(m => m.LogTime)
                    .Take(MaxExportRows)
                    .Select(m => new { m.LogTime, m.ConnectorId, m.Message, m.Result, m.ErrorCode })
                    .ToListAsync();

                using var workbook = new XLWorkbook();

                // ── Logs sheet ──
                var ws = workbook.Worksheets.Add("Logs");
                string[] headers = { $"Time ({FormatOffset(tzOffsetMinutes)})", "Time (UTC)", "Connector", "Message", "Result", "Error Code" };
                for (int c = 0; c < headers.Length; c++)
                    ws.Cell(1, c + 1).Value = headers[c];

                int row = 2;
                foreach (var log in logs)
                {
                    ws.Cell(row, 1).Value = log.LogTime.AddMinutes(tzOffsetMinutes);
                    ws.Cell(row, 2).Value = log.LogTime;
                    if (log.ConnectorId.HasValue)
                        ws.Cell(row, 3).Value = log.ConnectorId.Value;
                    ws.Cell(row, 4).Value = log.Message ?? string.Empty;
                    ws.Cell(row, 5).Value = log.Result ?? string.Empty;
                    ws.Cell(row, 6).Value = log.ErrorCode ?? string.Empty;
                    row++;
                }

                var header = ws.Range(1, 1, 1, headers.Length);
                header.Style.Font.Bold = true;
                header.Style.Font.FontColor = XLColor.White;
                header.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
                ws.Range(1, 1, Math.Max(row - 1, 1), headers.Length).SetAutoFilter();
                ws.SheetView.FreezeRows(1);
                ws.Column(1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                ws.Column(2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                // Fixed widths rather than AdjustToContents(), which is slow on 100k+ rows.
                ws.Column(1).Width = 21;
                ws.Column(2).Width = 21;
                ws.Column(3).Width = 11;
                ws.Column(4).Width = 28;
                ws.Column(5).Width = 60;
                ws.Column(6).Width = 22;
                ws.Column(5).Style.Alignment.WrapText = false;

                // ── Info sheet ──
                var info = workbook.Worksheets.Add("Info");
                var infoRows = new List<(string Label, XLCellValue Value)>
                {
                    ("Chargepoint ID", chargePoint.ChargePointId),
                    ("Name", chargePoint.Name ?? string.Empty),
                    ("Range", DescribeHours(hours)),
                    ($"From ({FormatOffset(tzOffsetMinutes)})", fromUtc.AddMinutes(tzOffsetMinutes)),
                    ($"To ({FormatOffset(tzOffsetMinutes)})", nowUtc.AddMinutes(tzOffsetMinutes)),
                    ("Connector filter", connectorId.HasValue ? connectorId.Value.ToString() : "All"),
                    ("Message filter", messages != null && messages.Count > 0 ? string.Join(", ", messages) : "All"),
                    ("Rows exported", logs.Count),
                    ("Rows matching", totalCount),
                };
                if (totalCount > logs.Count)
                    infoRows.Add(("Note", $"Truncated to the newest {MaxExportRows:N0} rows — narrow the range or filters for a complete export."));

                for (int i = 0; i < infoRows.Count; i++)
                {
                    info.Cell(i + 1, 1).Value = infoRows[i].Label;
                    info.Cell(i + 1, 2).Value = infoRows[i].Value;
                    info.Cell(i + 1, 1).Style.Font.Bold = true;
                }
                info.Range(4, 2, 5, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                info.Column(1).Width = 22;
                info.Column(2).Width = 60;

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);

                string safeId = string.Concat(chargePoint.ChargePointId.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
                string fileName = $"logs-{safeId}-{DescribeHours(hours).Replace(' ', '-').ToLowerInvariant()}-{nowUtc.AddMinutes(tzOffsetMinutes):yyyyMMdd-HHmm}.xlsx";

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportLogs: Error exporting message logs for '{ChargePointId}'", chargePointId);
                return StatusCode((int)HttpStatusCode.InternalServerError, new { success = false, message = "Error exporting message logs" });
            }
        }

        /// <summary>Shared connector / message-type / last-N-hours filtering for <see cref="GetLogs"/> and <see cref="ExportLogs"/>.</summary>
        private IQueryable<MessageLog> FilterLogs(string chargePointId, int? connectorId, List<string> messages, int? hours)
        {
            var query = _dbContext.MessageLogs.Where(m => m.ChargePointId == chargePointId);

            if (connectorId.HasValue)
                query = query.Where(m => m.ConnectorId == connectorId.Value);
            if (messages != null && messages.Count > 0)
                query = query.Where(m => messages.Contains(m.Message));
            if (hours.HasValue && hours.Value > 0)
            {
                var sinceUtc = DateTime.UtcNow.AddHours(-hours.Value);
                query = query.Where(m => m.LogTime >= sinceUtc);
            }

            return query;
        }

        private static string FormatOffset(int tzOffsetMinutes)
        {
            var span = TimeSpan.FromMinutes(Math.Abs(tzOffsetMinutes));
            return $"UTC{(tzOffsetMinutes < 0 ? "-" : "+")}{span:hh\\:mm}";
        }

        private static string DescribeHours(int hours) => hours switch
        {
            _ when hours % (24 * 7) == 0 => $"{hours / (24 * 7)} week{(hours / (24 * 7) == 1 ? "" : "s")}",
            _ when hours % 24 == 0 => $"{hours / 24} day{(hours / 24 == 1 ? "" : "s")}",
            _ => $"{hours} hour{(hours == 1 ? "" : "s")}",
        };

        /// <summary>
        /// Distinct OCPP action names (Heartbeat, MeterValues, StatusNotification, ...) actually
        /// logged for this chargepoint — drives the message-type multi-select filter on <see cref="GetLogs"/>.
        /// Derived from the data itself rather than a hard-coded list, since the set of possible
        /// <see cref="MessageLog.Message"/> values differs across OCPP 1.6/2.0/2.1 and evolves with
        /// OCPP.Core.Server's own action set.
        /// </summary>
        [HttpGet("{chargePointId}/log-message-types")]
        [Authorize]
        public async Task<IActionResult> GetLogMessageTypes(string chargePointId)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            try
            {
                var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
                if (chargePoint == null)
                    return Ok(new { success = false, message = "Chargepoint not found" });

                var messageTypes = await _dbContext.MessageLogs
                    .Where(m => m.ChargePointId == chargePointId && m.Message != null)
                    .Select(m => m.Message)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync();

                return Ok(new { success = true, data = messageTypes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLogMessageTypes: Error loading message types for '{ChargePointId}'", chargePointId);
                return Ok(new { success = false, message = "Error retrieving message types" });
            }
        }

        // ── Faulted monitor ──────────────────────────────────────────────────

        /// <summary>Row cap for the faulted monitor — keeps a fault storm from loading an unbounded set.</summary>
        private const int MaxFaultedRows = 20_000;

        /// <summary>Most recent Faulted notifications returned per chargepoint (the count still covers all of them).</summary>
        private const int MaxFaultedEventsPerChargePoint = 50;

        private const string StatusLogKeys = "Status|Info|VendorErrorCode|VendorId|Timestamp";

        /// <summary>
        /// Pulls "Key=Value" parts out of a StatusNotification MessageLog.Result. Handles both the
        /// original OCPP.Core layout ("Info=… / Status=Faulted / ") and the current one written by
        /// ControllerOCPP16.StatusNotification.cs ("Status=… / Info=… / VendorErrorCode=… / VendorId=…
        /// / Timestamp=…"), plus OCPP 2.x's bare "Status=…". A value runs up to the next " / Key=" (or the
        /// end / a trailing " / "), so a "/" inside the charger's free-text Info doesn't split it.
        /// </summary>
        private static readonly Regex StatusLogPartRegex = new Regex(
            $@"(?:^|\s/\s)(?<key>{StatusLogKeys})=(?<value>.*?)(?=\s/\s(?:{StatusLogKeys})=|\s*/\s*$|$)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static Dictionary<string, string> ParseStatusLog(string result)
        {
            var parts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(result))
                return parts;

            foreach (Match m in StatusLogPartRegex.Matches(result))
            {
                string value = m.Groups["value"].Value.Trim();
                if (value.Length > 0 && !parts.ContainsKey(m.Groups["key"].Value))
                    parts[m.Groups["key"].Value] = value;
            }
            return parts;
        }

        private static string NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Overview of every chargepoint that reported Status=Faulted in a StatusNotification during the
        /// last <paramref name="hours"/> hours (default 48, max 720): how many Faulted notifications reached
        /// us, on which connectors, the reasons the charger gave (Info / ErrorCode / VendorErrorCode), and
        /// whether a connector is still Faulted now (ConnectorStatus.LastStatus).
        ///
        /// Needs DbMessageLog &gt; 1 on OCPP.Core.Server — at 1, StatusNotifications aren't written to MessageLog.
        /// </summary>
        [HttpGet("faulted-summary")]
        [Authorize]
        public async Task<IActionResult> GetFaultedSummary([FromQuery] int hours = 48)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            if (hours < 1 || hours > MaxExportHours)
                return BadRequest(new { success = false, message = $"hours must be between 1 and {MaxExportHours}" });

            try
            {
                var nowUtc = DateTime.UtcNow;
                var sinceUtc = nowUtc.AddHours(-hours);

                // Narrow in SQL with a LIKE, then confirm the parsed Status in memory.
                var rows = await _dbContext.MessageLogs
                    .Where(m => m.LogTime >= sinceUtc
                                && m.Message == "StatusNotification"
                                && m.Result != null
                                && m.Result.Contains("Status=Faulted"))
                    .OrderByDescending(m => m.LogTime)
                    .Take(MaxFaultedRows + 1)
                    .Select(m => new { m.ChargePointId, m.ConnectorId, m.LogTime, m.Result, m.ErrorCode })
                    .ToListAsync();

                bool truncated = rows.Count > MaxFaultedRows;
                if (truncated)
                    rows.RemoveAt(rows.Count - 1);

                var events = rows
                    .Select(r =>
                    {
                        var parts = ParseStatusLog(r.Result);
                        parts.TryGetValue("Status", out var status);
                        parts.TryGetValue("Info", out var info);
                        parts.TryGetValue("VendorErrorCode", out var vendorErrorCode);
                        parts.TryGetValue("VendorId", out var vendorId);
                        parts.TryGetValue("Timestamp", out var chargerTimestamp);
                        return new
                        {
                            r.ChargePointId,
                            r.ConnectorId,
                            r.LogTime,
                            Status = status,
                            Info = info,
                            ErrorCode = NullIfBlank(r.ErrorCode),
                            VendorErrorCode = vendorErrorCode,
                            VendorId = vendorId,
                            ChargerTimestamp = chargerTimestamp,
                        };
                    })
                    .Where(e => string.Equals(e.Status, "Faulted", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var chargePointIds = events.Select(e => e.ChargePointId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var names = (await _dbContext.ChargePoints
                        .Where(cp => chargePointIds.Contains(cp.ChargePointId))
                        .Select(cp => new { cp.ChargePointId, cp.Name })
                        .ToListAsync())
                    .GroupBy(cp => cp.ChargePointId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

                // Connectors whose latest reported status is still Faulted right now.
                var stillFaulted = (await _dbContext.ConnectorStatuses
                        .Where(c => chargePointIds.Contains(c.ChargePointId) && c.LastStatus == "Faulted")
                        .Select(c => new { c.ChargePointId, c.ConnectorId })
                        .ToListAsync())
                    .GroupBy(c => c.ChargePointId, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(c => c.ConnectorId).OrderBy(id => id).ToList(), StringComparer.OrdinalIgnoreCase);

                var chargePoints = events
                    .GroupBy(e => e.ChargePointId, StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var ordered = g.OrderByDescending(e => e.LogTime).ToList();
                        var latest = ordered[0];
                        stillFaulted.TryGetValue(g.Key, out var faultedNow);
                        names.TryGetValue(g.Key, out var name);

                        return new
                        {
                            chargePointId = latest.ChargePointId,
                            name,
                            count = ordered.Count,
                            firstSeen = ordered[^1].LogTime,
                            lastSeen = latest.LogTime,
                            connectorIds = ordered.Where(e => e.ConnectorId.HasValue).Select(e => e.ConnectorId.Value).Distinct().OrderBy(id => id).ToList(),
                            stillFaultedConnectorIds = faultedNow ?? new List<int>(),
                            reasons = ordered
                                .GroupBy(e => new { e.Info, e.ErrorCode, e.VendorErrorCode })
                                .Select(r => new
                                {
                                    info = r.Key.Info,
                                    errorCode = r.Key.ErrorCode,
                                    vendorErrorCode = r.Key.VendorErrorCode,
                                    count = r.Count(),
                                    lastSeen = r.Max(e => e.LogTime)
                                })
                                .OrderByDescending(r => r.count)
                                .ThenByDescending(r => r.lastSeen)
                                .ToList(),
                            events = ordered
                                .Take(MaxFaultedEventsPerChargePoint)
                                .Select(e => new
                                {
                                    logTime = e.LogTime,
                                    connectorId = e.ConnectorId,
                                    info = e.Info,
                                    errorCode = e.ErrorCode,
                                    vendorErrorCode = e.VendorErrorCode,
                                    vendorId = e.VendorId,
                                    chargerTimestamp = e.ChargerTimestamp
                                })
                                .ToList()
                        };
                    })
                    .OrderByDescending(cp => cp.stillFaultedConnectorIds.Count > 0)
                    .ThenByDescending(cp => cp.count)
                    .ThenByDescending(cp => cp.lastSeen)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        hours,
                        since = sinceUtc,
                        totalEvents = events.Count,
                        chargePointCount = chargePoints.Count,
                        stillFaultedCount = chargePoints.Count(cp => cp.stillFaultedConnectorIds.Count > 0),
                        truncated,
                        chargePoints
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFaultedSummary: Error building faulted summary for last {Hours}h", hours);
                return Ok(new { success = false, message = "Error retrieving faulted chargepoints" });
            }
        }

        // ── Configuration (OCPP GetConfiguration / ChangeConfiguration) ─────────

        /// <summary>
        /// Proxies to OCPP.Core.Server's GetConfiguration command (ControllerOCPP16.Configuration.cs).
        /// <paramref name="keys"/> is an optional comma-separated key list; omit for the full
        /// configuration key list. Per the server's actual routing (OCPPMiddleware.cs), the key
        /// list occupies the URL's connector-id slot — /API/GetConfiguration/{chargePointId}/{keys}.
        /// </summary>
        [HttpGet("{chargePointId}/configuration")]
        [Authorize]
        public async Task<IActionResult> GetConfiguration(string chargePointId, [FromQuery] string keys = null)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
            if (chargePoint == null)
                return Ok(new { success = false, message = "Chargepoint not found" });

            // See GetOnlineStatus — the OCPP server's connection dictionary is case-sensitive.
            chargePointId = chargePoint.ChargePointId;

            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            if (string.IsNullOrEmpty(serverApiUrl))
                return Ok(new { success = false, message = "OCPP server API is not configured (ServerApiUrl missing)" });

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                string path = string.IsNullOrWhiteSpace(keys)
                    ? $"GetConfiguration/{Uri.EscapeDataString(chargePointId)}"
                    : $"GetConfiguration/{Uri.EscapeDataString(chargePointId)}/{Uri.EscapeDataString(keys)}";
                var uri = new Uri(new Uri(serverApiUrl), path);

                using var httpClient = _httpClientFactory.CreateClient();
                // The server waits up to 60s (TimoutWaitForCharger) for the charger's own OCPP
                // response before giving up — this must stay comfortably above that.
                httpClient.Timeout = TimeSpan.FromSeconds(70);

                string apiKeyConfig = _config.GetValue<string>("ApiKey");
                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKeyConfig);

                var response = await httpClient.GetAsync(uri);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return Ok(new { success = false, message = "Chargepoint is offline" });

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("GetConfiguration: API request for '{ChargePointId}' => httpStatus={StatusCode}", chargePointId, response.StatusCode);
                    return Ok(new { success = false, message = "Error requesting configuration from chargepoint" });
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Ok(new { success = true, data = json });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetConfiguration: Error for chargepoint '{ChargePointId}'", chargePointId);
                return Ok(new { success = false, message = "Error retrieving configuration" });
            }
        }

        /// <summary>
        /// Proxies to OCPP.Core.Server's ChangeConfiguration command. Returns
        /// {status: Accepted|Rejected|RebootRequired|NotSupported|Timeout|Error}.
        /// </summary>
        [HttpPost("{chargePointId}/configuration")]
        [Authorize]
        public async Task<IActionResult> ChangeConfiguration(string chargePointId, [FromBody] ChangeConfigurationRequestDto request)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            if (request == null || string.IsNullOrWhiteSpace(request.Key))
                return Ok(new { success = false, message = "Key is required" });

            var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
            if (chargePoint == null)
                return Ok(new { success = false, message = "Chargepoint not found" });

            // See GetOnlineStatus — the OCPP server's connection dictionary is case-sensitive.
            chargePointId = chargePoint.ChargePointId;

            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            if (string.IsNullOrEmpty(serverApiUrl))
                return Ok(new { success = false, message = "OCPP server API is not configured (ServerApiUrl missing)" });

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                string path = $"ChangeConfiguration/{Uri.EscapeDataString(chargePointId)}/{Uri.EscapeDataString(request.Key)}/{Uri.EscapeDataString(request.Value ?? string.Empty)}";
                var uri = new Uri(new Uri(serverApiUrl), path);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(70); // see GetConfiguration for why

                string apiKeyConfig = _config.GetValue<string>("ApiKey");
                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKeyConfig);

                var response = await httpClient.GetAsync(uri);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return Ok(new { success = false, message = "Chargepoint is offline" });

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("ChangeConfiguration: API request for '{ChargePointId}' => httpStatus={StatusCode}", chargePointId, response.StatusCode);
                    return Ok(new { success = false, message = "Error sending configuration change to chargepoint" });
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Ok(new { success = true, data = json });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ChangeConfiguration: Error for chargepoint '{ChargePointId}' key '{Key}'", chargePointId, request.Key);
                return Ok(new { success = false, message = "Error changing configuration" });
            }
        }

        public class ChangeConfigurationRequestDto
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }

        // ── TriggerMessage (e.g. ask the chargepoint to send a Heartbeat) ───────

        /// <summary>
        /// Proxies to OCPP.Core.Server's TriggerMessage command. A central system can't send a
        /// Heartbeat itself — it asks the chargepoint to send one. Returns
        /// {status: Accepted|Rejected|NotImplemented|Timeout|Error}.
        /// </summary>
        [HttpPost("{chargePointId}/trigger-message")]
        [Authorize]
        public async Task<IActionResult> TriggerMessage(string chargePointId, [FromBody] TriggerMessageRequestDto request)
        {
            if (!IsAdmin())
                return StatusCode((int)HttpStatusCode.Unauthorized);

            if (request == null || string.IsNullOrWhiteSpace(request.RequestedMessage))
                return Ok(new { success = false, message = "Requested message is required" });

            var chargePoint = await _dbContext.ChargePoints.FindAsync(chargePointId);
            if (chargePoint == null)
                return Ok(new { success = false, message = "Chargepoint not found" });

            // See GetOnlineStatus — the OCPP server's connection dictionary is case-sensitive.
            chargePointId = chargePoint.ChargePointId;

            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            if (string.IsNullOrEmpty(serverApiUrl))
                return Ok(new { success = false, message = "OCPP server API is not configured (ServerApiUrl missing)" });

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                string path = $"TriggerMessage/{Uri.EscapeDataString(chargePointId)}/{Uri.EscapeDataString(request.RequestedMessage)}";
                if (request.ConnectorId.HasValue)
                    path += $"/{request.ConnectorId.Value}";
                var uri = new Uri(new Uri(serverApiUrl), path);

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(70); // see GetConfiguration for why

                string apiKeyConfig = _config.GetValue<string>("ApiKey");
                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKeyConfig);

                var response = await httpClient.GetAsync(uri);
                if (response.StatusCode == HttpStatusCode.NotFound)
                    return Ok(new { success = false, message = "Chargepoint is offline" });

                if (response.StatusCode == HttpStatusCode.NotImplemented)
                    return Ok(new { success = false, message = "TriggerMessage is only supported for OCPP 1.6 chargepoints" });

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    _logger.LogError("TriggerMessage: API request for '{ChargePointId}' => httpStatus={StatusCode}", chargePointId, response.StatusCode);
                    return Ok(new { success = false, message = "Error sending trigger message to chargepoint" });
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return Ok(new { success = true, data = json });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TriggerMessage: Error for chargepoint '{ChargePointId}' message '{RequestedMessage}'", chargePointId, request.RequestedMessage);
                return Ok(new { success = false, message = "Error triggering message" });
            }
        }

        public class TriggerMessageRequestDto
        {
            public string RequestedMessage { get; set; }
            public int? ConnectorId { get; set; }
        }
    }
}
