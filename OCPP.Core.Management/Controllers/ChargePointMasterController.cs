using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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

                var query = _dbContext.MessageLogs.Where(m => m.ChargePointId == chargePointId);

                if (connectorId.HasValue)
                    query = query.Where(m => m.ConnectorId == connectorId.Value);
                if (messages != null && messages.Count > 0)
                    query = query.Where(m => messages.Contains(m.Message));
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
    }
}
