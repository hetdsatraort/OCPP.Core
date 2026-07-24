using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.Auth;
using OCPP.Core.Management.Models.ChargingHub;
using OCPP.Core.Management.Models.ChargingSession;
using OCPP.Core.Management.Models.UnifiedCharging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    /// <summary>
    /// Facade over the local OCPP charging system (<see cref="ChargingHubController"/>,
    /// <see cref="ChargingSessionController"/>) and the OCPI partner roaming system
    /// (<see cref="OcpiPartnerHubController"/>), exposing one set of routes and DTOs that can
    /// address either network via a composite id (see <see cref="UnifiedId"/>).
    ///
    /// This controller does not duplicate any business logic (wallet billing, zombie-session
    /// cleanup, SoC caching, OCPI proxy calls). All reads/writes are delegated in-process to the
    /// existing controllers by constructing them via <see cref="ActivatorUtilities"/> and sharing
    /// this request's <see cref="ControllerBase.ControllerContext"/>, then reshaping their results
    /// into the unified DTOs. The delegated controllers are never modified.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class UnifiedChargingController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<UnifiedChargingController> _logger;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public UnifiedChargingController(
            OCPPCoreContext dbContext,
            ILogger<UnifiedChargingController> logger,
            IConfiguration config,
            IHttpClientFactory httpClientFactory)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        // ── Locations ─────────────────────────────────────────────────────────

        /// <summary>
        /// Merged geo-radius search across local hubs and partner hubs, sorted by distance.
        /// </summary>
        [HttpPost("search-locations")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchLocations([FromBody] UnifiedLocationSearchDto request)
        {
            try
            {
                if (request == null)
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid search parameters" });

                var hubCtl = CreateDelegate<ChargingHubController>();
                var partnerCtl = CreateDelegate<OcpiPartnerHubController>();

                var merged = new List<UnifiedLocationDto>();

                var localResult = await hubCtl.SearchChargingHubsByLocation(new ChargingHubSearchDto
                {
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    RadiusKm = request.RadiusKm
                });
                var (_, localValue) = ExtractResult(localResult);
                if (localValue is ChargingHubListResponseDto localDto && localDto.Success && localDto.Hubs != null)
                {
                    merged.AddRange(localDto.Hubs.Select(h => new UnifiedLocationDto
                    {
                        Id = UnifiedId.Encode(ProviderType.Local, h.RecId),
                        ProviderType = ProviderType.Local,
                        Name = h.ChargingHubName,
                        AddressLine1 = h.AddressLine1,
                        City = h.City,
                        State = h.State,
                        Pincode = h.Pincode,
                        Latitude = h.Latitude,
                        Longitude = h.Longitude,
                        DistanceKm = h.DistanceKm,
                        AverageRating = h.AverageRating,
                        TotalStations = h.StationCount,
                        AvailableStations = h.StationCount
                    }));
                }

                var partnerResult = await partnerCtl.SearchPartnerHubs(new OcpiPartnerHubController.PartnerHubSearchDto
                {
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    RadiusKm = request.RadiusKm,
                    MaxResults = 100
                });
                var (_, partnerValue) = ExtractResult(partnerResult);
                var partnerJson = ToJsonElement(partnerValue);
                if (GetBool(partnerJson, "success") == true)
                {
                    foreach (var entry in GetArray(partnerJson, "data"))
                    {
                        var hubJson = GetObj(entry, "hub");
                        merged.Add(new UnifiedLocationDto
                        {
                            Id = UnifiedId.Encode(ProviderType.Partner, GetInt(hubJson, "id")?.ToString()),
                            ProviderType = ProviderType.Partner,
                            Name = GetString(hubJson, "chargingHubName"),
                            AddressLine1 = GetString(hubJson, "addressLine1"),
                            City = GetString(hubJson, "city"),
                            State = GetString(hubJson, "state"),
                            Pincode = GetString(hubJson, "pincode"),
                            Latitude = GetString(hubJson, "latitude"),
                            Longitude = GetString(hubJson, "longitude"),
                            DistanceKm = GetDouble(entry, "distanceKm"),
                            TotalStations = GetInt(hubJson, "stationCount") ?? 0,
                            AvailableStations = GetInt(hubJson, "availableStationCount") ?? 0,
                            PartnerName = GetString(hubJson, "partnerName")
                        });
                    }
                }

                var sorted = merged.OrderBy(m => m.DistanceKm ?? double.MaxValue).ToList();

                return Ok(new UnifiedLocationListResponseDto
                {
                    Success = true,
                    Message = $"Found {sorted.Count} location(s) within {request.RadiusKm}km",
                    Locations = sorted,
                    TotalCount = sorted.Count,
                    Page = 1,
                    PageSize = sorted.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching unified locations");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while searching locations" });
            }
        }

        /// <summary>
        /// Merged, paginated, nested hub→station→connector listing across both networks.
        /// </summary>
        [HttpPost("comprehensive-list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComprehensiveList([FromBody] UnifiedLocationComprehensiveSearchDto request)
        {
            try
            {
                request ??= new UnifiedLocationComprehensiveSearchDto();

                var hubCtl = CreateDelegate<ChargingHubController>();
                var partnerCtl = CreateDelegate<OcpiPartnerHubController>();

                var merged = new List<UnifiedLocationDto>();

                var localResult = await hubCtl.GetComprehensiveList(new ChargingHubComprehensiveSearchDto
                {
                    PageNumber = 1,
                    PageSize = 200,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    RadiusKm = request.RadiusKm,
                    SearchTerm = request.SearchTerm,
                    City = request.City,
                    State = request.State,
                    SortBy = "Distance",
                    SortOrder = "Asc"
                });
                var (_, localValue) = ExtractResult(localResult);
                if (localValue is ChargingHubComprehensiveResponseDto localDto && localDto.Success && localDto.Hubs != null)
                {
                    await CorrectOfflineAvailabilityAsync(localDto.Hubs);
                    merged.AddRange(localDto.Hubs.Select(MapLocalHubWithStationsToUnified));
                }

                var partnerResult = await partnerCtl.GetPartnerComprehensiveList(new OcpiPartnerHubController.PartnerHubComprehensiveSearchDto
                {
                    Page = 1,
                    PageSize = 200,
                    City = request.City,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude
                });
                var (_, partnerValue) = ExtractResult(partnerResult);
                var partnerJson = ToJsonElement(partnerValue);
                if (GetBool(partnerJson, "success") == true)
                {
                    foreach (var hubJson in GetArray(GetObj(partnerJson, "data"), "hubs"))
                        merged.Add(MapPartnerComprehensiveEntryToUnified(hubJson));
                }

                if (request.RadiusKm.HasValue)
                    merged = merged.Where(m => !m.DistanceKm.HasValue || m.DistanceKm <= request.RadiusKm.Value).ToList();

                merged = merged.OrderBy(m => m.DistanceKm ?? double.MaxValue).ToList();

                var totalCount = merged.Count;
                var pageSize = request.PageSize > 0 ? request.PageSize : 10;
                var page = request.Page > 0 ? request.Page : 1;
                var paged = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new UnifiedLocationListResponseDto
                {
                    Success = true,
                    Message = $"Found {totalCount} location(s) matching criteria",
                    Locations = paged,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unified comprehensive list");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while retrieving the comprehensive list" });
            }
        }

        /// <summary>
        /// Single location detail (hub or partner location), with nested stations/connectors.
        /// </summary>
        [HttpGet("location-details/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLocationDetails(string id)
        {
            try
            {
                if (!UnifiedId.TryParse(id, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid location id" });

                if (provider == ProviderType.Local)
                {
                    var hubCtl = CreateDelegate<ChargingHubController>();
                    var result = await hubCtl.GetChargingHubDetails(nativeId);
                    var (_, value) = ExtractResult(result);

                    if (!(value is ChargingHubDetailsResponseDto dto) || !dto.Success)
                        return Ok(new UnifiedChargingResponseDto
                        {
                            Success = false,
                            Message = (value as ChargingHubDetailsResponseDto)?.Message ?? "Location not found"
                        });

                    var stations = new List<UnifiedStationDto>();
                    foreach (var st in dto.Stations ?? new List<ChargingStationDto>())
                    {
                        var chargerResult = await hubCtl.GetChargerList(st.RecId);
                        var (_, chargerValue) = ExtractResult(chargerResult);
                        var chargers = (chargerValue as ChargerListResponseDto)?.Chargers ?? new List<ChargerDto>();

                        stations.Add(new UnifiedStationDto
                        {
                            Id = UnifiedId.Encode(ProviderType.Local, st.RecId),
                            ProviderType = ProviderType.Local,
                            Name = st.ChargePointName ?? st.ChargingPointId,
                            TotalConnectors = chargers.Count,
                            AvailableConnectors = chargers.Count(c =>
                                string.Equals(c.LastStatus ?? c.ChargerStatus, "Available", StringComparison.OrdinalIgnoreCase)),
                            Connectors = chargers.Select(MapLocalChargerToUnified).ToList()
                        });
                    }

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = true,
                        Message = dto.Message,
                        Data = new UnifiedLocationDto
                        {
                            Id = UnifiedId.Encode(ProviderType.Local, dto.Hub.RecId),
                            ProviderType = ProviderType.Local,
                            Name = dto.Hub.ChargingHubName,
                            AddressLine1 = dto.Hub.AddressLine1,
                            City = dto.Hub.City,
                            State = dto.Hub.State,
                            Pincode = dto.Hub.Pincode,
                            Latitude = dto.Hub.Latitude,
                            Longitude = dto.Hub.Longitude,
                            AverageRating = dto.AverageRating,
                            TotalStations = stations.Count,
                            AvailableStations = stations.Count(s => s.AvailableConnectors > 0),
                            TotalConnectors = stations.Sum(s => s.TotalConnectors),
                            AvailableConnectors = stations.Sum(s => s.AvailableConnectors),
                            Stations = stations
                        }
                    });
                }
                else
                {
                    if (!int.TryParse(nativeId, out int locationDbId))
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid partner location id" });

                    var partnerCtl = CreateDelegate<OcpiPartnerHubController>();
                    var result = await partnerCtl.GetPartnerHubDetails(locationDbId);
                    var (_, value) = ExtractResult(result);
                    var json = ToJsonElement(value);

                    if (GetBool(json, "success") != true)
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = GetString(json, "message") ?? "Partner location not found" });

                    var dataObj = GetObj(json, "data");
                    var hubJson = GetObj(dataObj, "hub");
                    var stations = GetArray(dataObj, "stations").Select(MapPartnerStationJsonToUnified).ToList();

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = true,
                        Message = "Partner location details retrieved successfully",
                        Data = new UnifiedLocationDto
                        {
                            Id = UnifiedId.Encode(ProviderType.Partner, GetInt(hubJson, "id")?.ToString()),
                            ProviderType = ProviderType.Partner,
                            Name = GetString(hubJson, "chargingHubName"),
                            AddressLine1 = GetString(hubJson, "addressLine1"),
                            City = GetString(hubJson, "city"),
                            State = GetString(hubJson, "state"),
                            Pincode = GetString(hubJson, "pincode"),
                            Latitude = GetString(hubJson, "latitude"),
                            Longitude = GetString(hubJson, "longitude"),
                            PartnerName = GetString(hubJson, "partnerName"),
                            TotalStations = stations.Count,
                            AvailableStations = stations.Count(s => s.AvailableConnectors > 0),
                            TotalConnectors = stations.Sum(s => s.TotalConnectors),
                            AvailableConnectors = stations.Sum(s => s.AvailableConnectors),
                            Stations = stations
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unified location details for {Id}", id);
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while retrieving location details" });
            }
        }

        /// <summary>
        /// Live connector status for Local connectors; last-synced snapshot for Partner connectors
        /// (there is no live-poll endpoint on the partner side today).
        /// </summary>
        [HttpGet("connector-status/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetConnectorStatus(string id)
        {
            try
            {
                if (!UnifiedId.TryParse(id, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid connector id" });

                if (provider == ProviderType.Local)
                {
                    var sessionCtl = CreateDelegate<ChargingSessionController>();
                    var result = await sessionCtl.GetChargingGunStatus(nativeId);
                    var (_, value) = ExtractResult(result);
                    var dto = value as ChargingSessionResponseDto;

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = dto?.Success ?? false,
                        Message = dto?.Message,
                        Data = new { ProviderType = ProviderType.Local, Live = true, Status = dto?.Data }
                    });
                }
                else
                {
                    if (!int.TryParse(nativeId, out int connectorDbId))
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid partner connector id" });

                    var connector = await _dbContext.OcpiPartnerConnectors.FirstOrDefaultAsync(c => c.Id == connectorDbId);
                    if (connector == null)
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Partner connector not found" });

                    var evse = await _dbContext.OcpiPartnerEvses.FirstOrDefaultAsync(e => e.Id == connector.PartnerEvseId);

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = true,
                        Message = "Partner connector status is a last-synced snapshot, not a live poll",
                        Data = new
                        {
                            ProviderType = ProviderType.Partner,
                            Live = false,
                            Status = evse?.Status ?? "UNKNOWN",
                            LastUpdated = connector.LastUpdated
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unified connector status for {Id}", id);
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while retrieving connector status" });
            }
        }

        // ── Sessions ──────────────────────────────────────────────────────────

        /// <summary>
        /// Start a charging session on either network, resolved from the composite connector id.
        /// </summary>
        [HttpPost("start-session")]
        [Authorize]
        public async Task<IActionResult> StartSession([FromBody] UnifiedStartSessionRequestDto request)
        {
            try
            {
                if (!UnifiedId.TryParse(request?.ConnectorId, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid or missing ConnectorId" });

                if (provider == ProviderType.Local)
                {
                    var gun = await _dbContext.ChargingGuns.FirstOrDefaultAsync(g => g.RecId == nativeId && g.Active == 1);
                    if (gun == null || !int.TryParse(gun.ConnectorId, out int connNum))
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Local connector not found" });

                    var sessionCtl = CreateDelegate<ChargingSessionController>();
                    var result = await sessionCtl.StartChargingSession(new StartChargingSessionRequestDto
                    {
                        ChargingGunId = gun.RecId,
                        ChargingStationId = gun.ChargingStationId,
                        ChargeTagId = request.ChargeTagId,
                        ConnectorId = connNum,
                        StartMeterReading = "0",
                        EnergyLimit = request.EnergyLimit,
                        CostLimit = request.CostLimit,
                        TimeLimit = request.TimeLimit,
                        BatteryIncreaseLimit = request.BatteryIncreaseLimit
                    });

                    var (_, value) = ExtractResult(result);
                    var respDto = value as ChargingSessionResponseDto;

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = respDto?.Success ?? false,
                        Message = respDto?.Message,
                        Data = respDto?.Success == true ? MapLocalStartResultToUnified(respDto.Data) : null
                    });
                }
                else
                {
                    if (!int.TryParse(nativeId, out int connectorDbId))
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid partner connector id" });

                    var connector = await _dbContext.OcpiPartnerConnectors.FirstOrDefaultAsync(c => c.Id == connectorDbId);
                    if (connector == null)
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Partner connector not found" });

                    var partnerCtl = CreateDelegate<OcpiPartnerHubController>();
                    var result = await partnerCtl.StartPartnerSession(new OcpiPartnerHubController.PartnerSessionStartDto
                    {
                        EvseDbId = connector.PartnerEvseId,
                        ConnectorId = connector.ConnectorId,
                        TokenUid = request.TokenUid,
                        ChargeTagId = request.ChargeTagId,
                        EnergyLimit = request.EnergyLimit,
                        CostLimit = request.CostLimit,
                        TimeLimit = request.TimeLimit,
                        BatteryIncreaseLimit = request.BatteryIncreaseLimit
                    });

                    var (_, value) = ExtractResult(result);
                    var json = ToJsonElement(value);

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = GetBool(json, "success") ?? false,
                        Message = GetString(json, "message"),
                        Data = new
                        {
                            ProviderType = ProviderType.Partner,
                            ConnectorId = request.ConnectorId,
                            // The OCPI roaming service assigns the real session id asynchronously
                            // (see OcpiAdminController.EmspStartSession) — its exact response schema
                            // isn't owned by this facade, so it's passed through untouched here.
                            Raw = value
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting unified session");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while starting the charging session" });
            }
        }

        /// <summary>
        /// Stop a charging session on either network, resolved from the composite session id.
        /// </summary>
        [HttpPost("stop-session")]
        [Authorize]
        public async Task<IActionResult> StopSession([FromBody] UnifiedStopSessionRequestDto request)
        {
            try
            {
                if (!UnifiedId.TryParse(request?.SessionId, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid or missing SessionId" });

                if (provider == ProviderType.Local)
                {
                    var sessionCtl = CreateDelegate<ChargingSessionController>();
                    var result = await sessionCtl.EndChargingSession(new EndChargingSessionRequestDto
                    {
                        SessionId = nativeId,
                        EndMeterReading = request.EndMeterReading
                    });

                    var (_, value) = ExtractResult(result);
                    var respDto = value as ChargingSessionResponseDto;

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = respDto?.Success ?? false,
                        Message = respDto?.Message,
                        Data = respDto?.Success == true ? MapLocalStopResultToUnified(respDto.Data, nativeId) : null
                    });
                }
                else
                {
                    var partnerCtl = CreateDelegate<OcpiPartnerHubController>();
                    var result = await partnerCtl.StopPartnerSession(new OcpiPartnerHubController.PartnerSessionStopDto { SessionId = nativeId });
                    var (_, value) = ExtractResult(result);
                    var json = ToJsonElement(value);

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = GetBool(json, "success") ?? false,
                        Message = GetString(json, "message"),
                        Data = new { ProviderType = ProviderType.Partner, SessionId = request.SessionId, Raw = value }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping unified session");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while stopping the charging session" });
            }
        }

        /// <summary>
        /// Poll the resolution status of a Partner session started via <see cref="StartSession"/>,
        /// keyed by the authorization_reference handed back in that response's <c>Data.Raw</c>
        /// payload (before the partner CPO has pushed back a real session_id). Local sessions have
        /// no such pending state — their id is known synchronously at start — so this only ever
        /// resolves against the Partner network. Once resolved, <c>sessionId</c> is a composite
        /// <c>P:{sessionId}</c> id usable directly with <see cref="StopSession"/> / <see cref="GetSessionDetails"/>.
        /// </summary>
        [HttpGet("by-reference/{authorizationReference}")]
        [Authorize]
        public async Task<IActionResult> GetSessionByReference(string authorizationReference)
        {
            try
            {
                var partnerCtl = CreateDelegate<OcpiPartnerHubController>();
                var result = await partnerCtl.GetPartnerSessionByReference(authorizationReference);
                var (_, value) = ExtractResult(result);
                var json = ToJsonElement(value);

                if (GetBool(json, "success") != true)
                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = false,
                        Message = GetString(json, "message") ?? "No session found for this authorization reference"
                    });

                var dataJson = GetObj(json, "data");
                var nativeSessionId = GetString(dataJson, "sessionId");
                var resolved = GetBool(dataJson, "resolved") ?? false;

                return Ok(new UnifiedChargingResponseDto
                {
                    Success = true,
                    Message = "Partner session reference status retrieved successfully",
                    Data = new
                    {
                        ProviderType = ProviderType.Partner,
                        AuthorizationReference = GetString(dataJson, "authorizationReference"),
                        SessionId = resolved && !string.IsNullOrEmpty(nativeSessionId)
                            ? UnifiedId.Encode(ProviderType.Partner, nativeSessionId)
                            : string.Empty,
                        Status = GetString(dataJson, "status"),
                        Resolved = resolved
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling unified session by authorization reference {AuthorizationReference}", authorizationReference);
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while polling the session status" });
            }
        }

        /// <summary>
        /// Unified rich session detail — same shape for both networks, including a LimitProgress
        /// block (computed here for Local sessions, which don't expose it today) and best-effort
        /// BatteryStateOfCharge for Partner sessions (which only report a flat current percentage).
        /// </summary>
        [HttpGet("session-details/{id}")]
        [Authorize]
        public async Task<IActionResult> GetSessionDetails(string id)
        {
            try
            {
                if (!UnifiedId.TryParse(id, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid session id" });

                if (provider == ProviderType.Local)
                {
                    var sessionCtl = CreateDelegate<ChargingSessionController>();
                    var result = await sessionCtl.GetChargingSessionDetails(nativeId);
                    var (_, value) = ExtractResult(result);
                    var respDto = value as ChargingSessionResponseDto;

                    if (respDto?.Success != true)
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = respDto?.Message ?? "Session not found" });

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = true,
                        Message = respDto.Message,
                        Data = MapLocalDetailsToUnified(respDto.Data, nativeId)
                    });
                }
                else
                {
                    var partnerCtl = CreateDelegate<OcpiPartnerHubController>();
                    // GetPartnerSessions has no single-session filter. Rather than re-deriving its
                    // projection/limit-progress logic here, page through with a generous size and
                    // locate the matching row — acceptable for typical per-user session volumes.
                    var result = await partnerCtl.GetPartnerSessions(status: null, userId: null, page: 1, pageSize: 500);
                    var (_, value) = ExtractResult(result);
                    var json = ToJsonElement(value);

                    if (GetBool(json, "success") != true)
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = GetString(json, "message") ?? "Session not found" });

                    var sessionsArr = GetArray(GetObj(json, "data"), "sessions");
                    var match = sessionsArr.FirstOrDefault(s => GetString(s, "sessionId") == nativeId);

                    if (match.ValueKind != JsonValueKind.Object)
                        return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Session not found" });

                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = true,
                        Message = "Partner session details retrieved successfully",
                        Data = MapPartnerListItemToUnified(match)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unified session details for {Id}", id);
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while retrieving session details" });
            }
        }

        /// <summary>
        /// Merged, paginated list of the caller's own sessions across both networks.
        /// </summary>
        [HttpGet("sessions")]
        [Authorize]
        public async Task<IActionResult> GetSessions([FromQuery] string status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var sessionCtl = CreateDelegate<ChargingSessionController>();
                var partnerCtl = CreateDelegate<OcpiPartnerHubController>();

                var merged = new List<UnifiedSessionDto>();

                var localResult = await sessionCtl.GetChargingSessions(stationId: null, status: status, pageSize: 200, page: 1);
                var (_, localValue) = ExtractResult(localResult);
                if (localValue is ChargingSessionResponseDto localDto && localDto.Success)
                {
                    var dataJson = ToJsonElement(localDto.Data);
                    foreach (var s in GetArray(dataJson, "Sessions"))
                        merged.Add(MapLocalListItemToUnified(s));
                }

                var partnerStatus = string.IsNullOrEmpty(status) ? null : status.ToUpperInvariant();
                var partnerResult = await partnerCtl.GetPartnerSessions(status: partnerStatus, userId: null, page: 1, pageSize: 200);
                var (_, partnerValue) = ExtractResult(partnerResult);
                var partnerJson = ToJsonElement(partnerValue);
                if (GetBool(partnerJson, "success") == true)
                {
                    foreach (var s in GetArray(GetObj(partnerJson, "data"), "sessions"))
                        merged.Add(MapPartnerListItemToUnified(s));
                }

                var sorted = merged.OrderByDescending(s => s.StartTime).ToList();
                var totalCount = sorted.Count;
                var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new UnifiedSessionListResponseDto
                {
                    Success = true,
                    Message = "Sessions retrieved successfully",
                    Sessions = paged,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving unified sessions");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while retrieving sessions" });
            }
        }

        /// <summary>
        /// Unlock a Local connector. Partner (OCPI) connectors are not supported — the eMSP-role
        /// UNLOCK_CONNECTOR command isn't wired up anywhere in this codebase today.
        /// </summary>
        [HttpPost("unlock-connector")]
        [Authorize]
        public async Task<IActionResult> UnlockConnector([FromBody] UnifiedUnlockConnectorRequestDto request)
        {
            try
            {
                if (!UnifiedId.TryParse(request?.ConnectorId, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid connector id" });

                if (provider == ProviderType.Partner)
                {
                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = false,
                        Message = "Unlock is not supported for partner (OCPI) connectors in this deployment."
                    });
                }

                var gun = await _dbContext.ChargingGuns.FirstOrDefaultAsync(g => g.RecId == nativeId && g.Active == 1);
                if (gun == null || !int.TryParse(gun.ConnectorId, out int connNum))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Local connector not found" });

                var sessionCtl = CreateDelegate<ChargingSessionController>();
                var result = await sessionCtl.UnlockConnector(new UnlockConnectorRequestDto
                {
                    ChargingStationId = gun.ChargingStationId,
                    ConnectorId = connNum
                });

                var (_, value) = ExtractResult(result);
                var dto = value as ChargingSessionResponseDto;

                return Ok(new UnifiedChargingResponseDto
                {
                    Success = dto?.Success ?? false,
                    Message = dto?.Message,
                    Data = dto?.Data
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking unified connector");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while unlocking the connector" });
            }
        }

        /// <summary>
        /// Link a user vehicle to a Local charging session. Partner (OCPI) sessions are not
        /// supported — ChargingSessions.VehicleId has no equivalent column on the OCPI session
        /// tables, so there's nowhere to persist the link for a partner session.
        /// </summary>
        [HttpPost("link-session-vehicle")]
        [Authorize]
        public async Task<IActionResult> LinkSessionVehicle([FromBody] UnifiedSessionVehicleLinkRequestDto request)
        {
            try
            {
                if (!UnifiedId.TryParse(request?.SessionId, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid or missing SessionId" });

                if (provider == ProviderType.Partner)
                {
                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = false,
                        Message = "Linking a vehicle is not supported for partner (OCPI) sessions in this deployment."
                    });
                }

                var userCtl = CreateDelegate<UserController>();
                var result = await userCtl.LinkSessionVehicle(new SessionVehicleLinkDto
                {
                    SessionId = nativeId,
                    VehicleId = request.VehicleId
                });

                var (_, value) = ExtractResult(result);
                var dto = value as SessionVehicleResponseDto;

                return Ok(new UnifiedChargingResponseDto
                {
                    Success = dto?.Success ?? false,
                    Message = dto?.Message,
                    Data = dto?.Success == true
                        ? new { SessionId = request.SessionId, Vehicle = dto.Vehicle }
                        : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking vehicle to unified session");
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while linking the vehicle to the session" });
            }
        }

        /// <summary>
        /// Get the vehicle linked to a Local charging session (Local only — see <see cref="LinkSessionVehicle"/>).
        /// </summary>
        [HttpGet("session-vehicle/{id}")]
        [Authorize]
        public async Task<IActionResult> GetSessionVehicle(string id)
        {
            try
            {
                if (!UnifiedId.TryParse(id, out var provider, out var nativeId))
                    return Ok(new UnifiedChargingResponseDto { Success = false, Message = "Invalid session id" });

                if (provider == ProviderType.Partner)
                {
                    return Ok(new UnifiedChargingResponseDto
                    {
                        Success = false,
                        Message = "Partner (OCPI) sessions do not support vehicle association in this deployment."
                    });
                }

                var userCtl = CreateDelegate<UserController>();
                var result = await userCtl.GetSessionVehicle(nativeId);

                var (_, value) = ExtractResult(result);
                var dto = value as SessionVehicleResponseDto;

                return Ok(new UnifiedChargingResponseDto
                {
                    Success = dto?.Success ?? false,
                    Message = dto?.Message,
                    Data = dto?.Success == true
                        ? new { SessionId = id, Vehicle = dto.Vehicle }
                        : null
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vehicle for unified session {Id}", id);
                return Ok(new UnifiedChargingResponseDto { Success = false, Message = "An error occurred while retrieving the linked vehicle" });
            }
        }

        // ── Delegation helpers ────────────────────────────────────────────────

        /// <summary>
        /// Constructs an existing controller in-process, resolving its constructor dependencies
        /// from this request's scoped service provider and sharing this request's ControllerContext
        /// (so User/claims/ModelState behave identically to a normal request). No HTTP round-trip.
        /// </summary>
        private T CreateDelegate<T>() where T : ControllerBase
        {
            var controller = ActivatorUtilities.CreateInstance<T>(HttpContext.RequestServices);
            controller.ControllerContext = ControllerContext;
            return controller;
        }

        private static (int StatusCode, object Value) ExtractResult(IActionResult result)
        {
            switch (result)
            {
                case ObjectResult obj:
                    return (obj.StatusCode ?? 200, obj.Value);
                case UnauthorizedResult:
                    return (401, null);
                case ForbidResult:
                    return (403, null);
                case NotFoundResult:
                    return (404, null);
                case StatusCodeResult sc:
                    return (sc.StatusCode, null);
                default:
                    return (200, null);
            }
        }

        private static JsonElement ToJsonElement(object obj)
        {
            if (obj is JsonElement je) return je;
            if (obj == null) return default;
            var json = JsonSerializer.Serialize(obj);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private static bool TryGetProp(JsonElement el, string name, out JsonElement value)
        {
            value = default;
            if (el.ValueKind != JsonValueKind.Object || string.IsNullOrEmpty(name))
                return false;

            if (el.TryGetProperty(name, out value))
                return true;

            var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
            if (el.TryGetProperty(camel, out value))
                return true;

            var pascal = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return el.TryGetProperty(pascal, out value);
        }

        private static string GetString(JsonElement el, string name) =>
            TryGetProp(el, name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static bool? GetBool(JsonElement el, string name) =>
            TryGetProp(el, name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                ? v.GetBoolean() : (bool?)null;

        private static double? GetDouble(JsonElement el, string name) =>
            TryGetProp(el, name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null;

        private static int? GetInt(JsonElement el, string name) =>
            TryGetProp(el, name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : (int?)null;

        private static DateTime? GetDateTime(JsonElement el, string name) =>
            TryGetProp(el, name, out var v) && v.ValueKind == JsonValueKind.String && DateTime.TryParse(v.GetString(), out var dt)
                ? dt : (DateTime?)null;

        private static JsonElement GetObj(JsonElement el, string name) =>
            TryGetProp(el, name, out var v) ? v : default;

        private static List<JsonElement> GetArray(JsonElement el, string name)
        {
            if (!TryGetProp(el, name, out var v) || v.ValueKind != JsonValueKind.Array)
                return new List<JsonElement>();
            return v.EnumerateArray().ToList();
        }

        // ── Live availability correction — Local ──────────────────────────────

        /// <summary>
        /// ChargingHubController's availability counts trust ChargingGuns.ChargerStatus /
        /// ConnectorStatus.LastStatus, which are only corrected by the periodic GunStatusService
        /// background sync (every ~10 minutes per GunStatus:CheckIntervalMinutes) — a charge point
        /// that just went offline keeps showing its last-known "Available" status until that next
        /// tick. This does a live connectivity check (deduped per ChargePointId, same
        /// ConnectionStatus endpoint GunStatusSyncService uses) and zeroes out availability for any
        /// station whose charge point is confirmed offline right now, before mapping to the unified
        /// shape — mutates the delegated controller's response in place, not the DB or the
        /// delegated controller itself.
        /// </summary>
        private async Task CorrectOfflineAvailabilityAsync(List<ChargingHubWithStationsDto> hubs)
        {
            var chargePointIds = hubs
                .SelectMany(h => h.Stations ?? new List<ChargingStationWithChargersDto>())
                .Select(s => s.ChargingPointId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            if (chargePointIds.Count == 0)
                return;

            // Checked concurrently — sequential checks would multiply badly (5s timeout each)
            // if several charge points happen to be offline at once.
            var checks = await Task.WhenAll(chargePointIds.Select(async id => (Id: id, Online: await IsChargePointOnlineAsync(id))));
            var onlineByChargePointId = checks.ToDictionary(x => x.Id, x => x.Online);

            foreach (var hub in hubs)
            {
                if (hub.Stations == null)
                    continue;

                foreach (var station in hub.Stations)
                {
                    if (string.IsNullOrEmpty(station.ChargingPointId) ||
                        !onlineByChargePointId.TryGetValue(station.ChargingPointId, out var online) || online)
                        continue;

                    // Charge point is confirmed offline right now — no connector on it is actually
                    // available, regardless of what its last-synced status says.
                    station.AvailableChargers = 0;
                    if (station.Chargers != null)
                    {
                        foreach (var charger in station.Chargers)
                        {
                            charger.LastStatus = "Offline";
                            charger.ChargerStatus = "Offline";
                        }
                    }
                }

                hub.AvailableChargers = hub.Stations.Sum(s => s.AvailableChargers);
            }
        }

        /// <summary>
        /// Same live connectivity check GunStatusSyncService uses, but deliberately fails OPEN
        /// (treats as online) on any error or missing config — unlike the per-gun sync service,
        /// this drives a whole listing endpoint, so a transient OCPP-server hiccup must not make
        /// every local charger in the response look unavailable.
        /// </summary>
        private async Task<bool> IsChargePointOnlineAsync(string chargePointId)
        {
            string serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            if (string.IsNullOrEmpty(serverApiUrl))
                return true;

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                var uri = new Uri(new Uri(serverApiUrl), $"ConnectionStatus/{Uri.EscapeDataString(chargePointId)}");

                using var client = _httpClientFactory.CreateClient("GunStatus");
                client.Timeout = TimeSpan.FromSeconds(5);

                string apiKey = _config.GetValue<string>("ApiKey");
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

                var response = await client.GetAsync(uri);
                if (!response.IsSuccessStatusCode)
                    return true;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return GetBool(json, "isOnline") ?? true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UnifiedChargingController: connection status check failed for {ChargePointId}", chargePointId);
                return true;
            }
        }

        // ── Mapping helpers — Local ───────────────────────────────────────────

        private static UnifiedConnectorDto MapLocalChargerToUnified(ChargerDto c) => new UnifiedConnectorDto
        {
            Id = UnifiedId.Encode(ProviderType.Local, c.RecId),
            ProviderType = ProviderType.Local,
            ConnectorId = c.ConnectorId,
            ChargerTypeName = c.ChargerTypeName,
            PowerOutput = c.PowerOutput,
            Tariff = c.ChargerTariff,
            Status = c.LastStatus ?? c.ChargerStatus,
            LastUpdated = c.LastStatusTime ?? c.UpdatedOn
        };

        private static UnifiedStationDto MapLocalStationWithChargersToUnified(ChargingStationWithChargersDto st) => new UnifiedStationDto
        {
            Id = UnifiedId.Encode(ProviderType.Local, st.RecId),
            ProviderType = ProviderType.Local,
            Name = st.ChargePointName ?? st.ChargingPointId,
            TotalConnectors = st.TotalChargers,
            AvailableConnectors = st.AvailableChargers,
            Connectors = st.Chargers?.Select(MapLocalChargerToUnified).ToList() ?? new List<UnifiedConnectorDto>()
        };

        private static UnifiedLocationDto MapLocalHubWithStationsToUnified(ChargingHubWithStationsDto hub) => new UnifiedLocationDto
        {
            Id = UnifiedId.Encode(ProviderType.Local, hub.RecId),
            ProviderType = ProviderType.Local,
            Name = hub.ChargingHubName,
            AddressLine1 = hub.AddressLine1,
            City = hub.City,
            State = hub.State,
            Pincode = hub.Pincode,
            Latitude = hub.Latitude,
            Longitude = hub.Longitude,
            DistanceKm = hub.DistanceKm,
            AverageRating = hub.AverageRating,
            TotalStations = hub.TotalStations,
            AvailableStations = hub.Stations?.Count(s => s.AvailableChargers > 0) ?? 0,
            TotalConnectors = hub.TotalChargers,
            AvailableConnectors = hub.AvailableChargers,
            Stations = hub.Stations?.Select(MapLocalStationWithChargersToUnified).ToList() ?? new List<UnifiedStationDto>()
        };

        private static UnifiedBatteryStateOfChargeDto MapBatterySoc(JsonElement batteryJson)
        {
            if (batteryJson.ValueKind != JsonValueKind.Object) return null;
            return new UnifiedBatteryStateOfChargeDto
            {
                StartSoC = GetDouble(batteryJson, "StartSoC"),
                EndSoC = GetDouble(batteryJson, "EndSoC"),
                CurrentSoC = GetDouble(batteryJson, "CurrentSoC"),
                SoCGain = GetDouble(batteryJson, "SoCGain"),
                LastUpdate = GetDateTime(batteryJson, "LastUpdate"),
                Unit = GetString(batteryJson, "Unit") ?? "%",
                IsRealtime = GetBool(batteryJson, "IsRealtime") ?? false,
                DataSource = GetString(batteryJson, "DataSource")
            };
        }

        private static UnifiedSessionDto MapLocalStartResultToUnified(object data)
        {
            var json = ToJsonElement(data);
            var sessionJson = GetObj(json, "Session");

            return new UnifiedSessionDto
            {
                Id = UnifiedId.Encode(ProviderType.Local, GetString(sessionJson, "RecId")),
                ProviderType = ProviderType.Local,
                Status = "Active",
                IsActive = true,
                StartTime = GetDateTime(json, "StartTime") ?? GetDateTime(sessionJson, "StartTime") ?? DateTime.UtcNow,
                MeterStart = GetDouble(json, "MeterStart"),
                EnergyLimit = GetDouble(sessionJson, "EnergyLimit"),
                CostLimit = GetDouble(sessionJson, "CostLimit"),
                TimeLimit = GetInt(sessionJson, "TimeLimit"),
                BatteryIncreaseLimit = GetDouble(sessionJson, "BatteryIncreaseLimit"),
                StationId = UnifiedId.Encode(ProviderType.Local, GetString(sessionJson, "ChargingStationId")),
                ConnectorId = UnifiedId.Encode(ProviderType.Local, GetString(sessionJson, "ChargingGunId")),
                BatteryStateOfCharge = MapBatterySoc(GetObj(json, "BatteryStateOfCharge")),
                Raw = data
            };
        }

        private static UnifiedSessionDto MapLocalStopResultToUnified(object data, string sessionRecId)
        {
            var json = ToJsonElement(data);
            var sessionJson = GetObj(json, "Session");
            var walletJson = GetObj(json, "WalletTransaction");

            return new UnifiedSessionDto
            {
                Id = UnifiedId.Encode(ProviderType.Local, sessionRecId),
                ProviderType = ProviderType.Local,
                Status = "Completed",
                IsActive = false,
                StartTime = GetDateTime(sessionJson, "StartTime") ?? DateTime.UtcNow,
                EndTime = GetDateTime(sessionJson, "EndTime"),
                MeterStart = GetDouble(json, "MeterStart"),
                MeterCurrent = GetDouble(json, "MeterStop"),
                EnergyDelivered = GetDouble(json, "EnergyConsumed") ?? 0,
                Cost = (decimal)(GetDouble(json, "Cost") ?? 0),
                BatteryStateOfCharge = MapBatterySoc(GetObj(json, "BatteryStateOfCharge")),
                // EndChargingSession's WalletTransaction shape is { TransactionId, PreviousBalance,
                // AmountDebited, NewBalance } — different from GetChargingSessionDetails' richer
                // WalletTransactionDto (which has RecId). Always populated on success, so just
                // pass it through.
                WalletTransaction = walletJson.ValueKind == JsonValueKind.Object ? (object)walletJson : null,
                Raw = data
            };
        }

        private static UnifiedSessionDto MapLocalDetailsToUnified(object data, string sessionRecId)
        {
            var json = ToJsonElement(data);
            var sessionJson = GetObj(json, "Session");
            var meterReadings = GetObj(json, "MeterReadings");
            var costDetails = GetObj(json, "CostDetails");
            var timing = GetObj(json, "Timing");
            var walletJson = GetObj(json, "WalletTransaction");

            double? energyLimit = GetDouble(sessionJson, "EnergyLimit");
            double? costLimit = GetDouble(sessionJson, "CostLimit");
            int? timeLimit = GetInt(sessionJson, "TimeLimit");
            double? totalEnergy = GetDouble(GetObj(json, "EnergyConsumption"), "TotalEnergy");
            double? totalCost = GetDouble(costDetails, "TotalCost");
            double elapsedMinutes = GetDouble(GetObj(timing, "Duration"), "TotalMinutes") ?? 0;

            var limitProgress = new UnifiedLimitProgressDto
            {
                EnergyPct = (energyLimit.HasValue && energyLimit > 0 && totalEnergy.HasValue)
                    ? Math.Round(totalEnergy.Value / energyLimit.Value * 100, 1) : (double?)null,
                CostPct = (costLimit.HasValue && costLimit > 0 && totalCost.HasValue)
                    ? Math.Round(totalCost.Value / costLimit.Value * 100, 1) : (double?)null,
                TimePct = (timeLimit.HasValue && timeLimit > 0)
                    ? Math.Round(elapsedMinutes / timeLimit.Value * 100, 1) : (double?)null
            };

            return new UnifiedSessionDto
            {
                Id = UnifiedId.Encode(ProviderType.Local, sessionRecId),
                ProviderType = ProviderType.Local,
                Status = GetString(json, "Status"),
                IsActive = GetBool(json, "IsActive") ?? false,
                StartTime = GetDateTime(timing, "StartTime") ?? GetDateTime(sessionJson, "StartTime") ?? DateTime.UtcNow,
                EndTime = GetDateTime(timing, "EndTime"),
                MeterStart = GetDouble(meterReadings, "StartReading"),
                MeterCurrent = GetDouble(meterReadings, "CurrentReading"),
                EnergyDelivered = totalEnergy ?? 0,
                Cost = (decimal)(totalCost ?? 0),
                Currency = GetString(costDetails, "Currency") ?? "INR",
                LocationName = GetString(sessionJson, "ChargingHubName"),
                StationId = UnifiedId.Encode(ProviderType.Local, GetString(sessionJson, "ChargingStationId")),
                ConnectorId = UnifiedId.Encode(ProviderType.Local, GetString(sessionJson, "ChargingGunId")),
                EnergyLimit = energyLimit,
                CostLimit = costLimit,
                TimeLimit = timeLimit,
                BatteryIncreaseLimit = GetDouble(sessionJson, "BatteryIncreaseLimit"),
                LimitProgress = limitProgress,
                BatteryStateOfCharge = MapBatterySoc(GetObj(json, "BatteryStateOfCharge")),
                WalletTransaction = GetString(walletJson, "RecId") != null ? (object)walletJson : null,
                Raw = data
            };
        }

        private static UnifiedSessionDto MapLocalListItemToUnified(JsonElement s)
        {
            double.TryParse(GetString(s, "EnergyTransmitted"), out var energy);
            decimal.TryParse(GetString(s, "ChargingTotalFee"), out var fee);

            double? socStart = GetDouble(s, "SoCStart");
            double? socEnd = GetDouble(s, "SoCEnd");

            return new UnifiedSessionDto
            {
                Id = UnifiedId.Encode(ProviderType.Local, GetString(s, "RecId")),
                ProviderType = ProviderType.Local,
                Status = GetString(s, "Status"),
                IsActive = string.Equals(GetString(s, "Status"), "Active", StringComparison.OrdinalIgnoreCase),
                StartTime = GetDateTime(s, "StartTime") ?? DateTime.UtcNow,
                EndTime = GetDateTime(s, "EndTime"),
                EnergyDelivered = energy,
                Cost = fee,
                LocationName = GetString(s, "ChargingHubName"),
                StationId = UnifiedId.Encode(ProviderType.Local, GetString(s, "ChargingStationId")),
                ConnectorId = UnifiedId.Encode(ProviderType.Local, GetString(s, "ChargingGunId")),
                EnergyLimit = GetDouble(s, "EnergyLimit"),
                CostLimit = GetDouble(s, "CostLimit"),
                TimeLimit = GetInt(s, "TimeLimit"),
                BatteryIncreaseLimit = GetDouble(s, "BatteryIncreaseLimit"),
                BatteryStateOfCharge = (socStart.HasValue || socEnd.HasValue) ? new UnifiedBatteryStateOfChargeDto
                {
                    StartSoC = socStart,
                    EndSoC = socEnd,
                    LastUpdate = GetDateTime(s, "SoCLastUpdate"),
                    IsRealtime = false,
                    DataSource = "Database (Historical)"
                } : null,
                Raw = s
            };
        }

        // ── Mapping helpers — Partner ─────────────────────────────────────────

        private static UnifiedConnectorDto MapPartnerConnectorJsonToUnified(JsonElement c) => new UnifiedConnectorDto
        {
            Id = UnifiedId.Encode(ProviderType.Partner, GetInt(c, "id")?.ToString()),
            ProviderType = ProviderType.Partner,
            ConnectorId = GetString(c, "connectorId"),
            ChargerTypeName = GetString(c, "chargerTypeName"),
            PowerOutput = GetDouble(c, "powerOutputKw")?.ToString("F2"),
            Status = GetString(c, "chargerStatus"),
            LastUpdated = GetDateTime(c, "lastUpdated")
        };

        private static UnifiedStationDto MapPartnerStationJsonToUnified(JsonElement st)
        {
            int gunCount = GetInt(st, "chargingGunCount") ?? 0;
            bool available = string.Equals(GetString(st, "status"), "AVAILABLE", StringComparison.OrdinalIgnoreCase);

            return new UnifiedStationDto
            {
                Id = UnifiedId.Encode(ProviderType.Partner, GetInt(st, "id")?.ToString()),
                ProviderType = ProviderType.Partner,
                Name = GetString(st, "chargePointName") ?? GetString(st, "chargingPointId"),
                TotalConnectors = gunCount,
                AvailableConnectors = available ? gunCount : 0,
                Connectors = GetArray(st, "chargers").Select(MapPartnerConnectorJsonToUnified).ToList()
            };
        }

        private static UnifiedLocationDto MapPartnerComprehensiveEntryToUnified(JsonElement entry)
        {
            var hubJson = GetObj(entry, "hub");
            return new UnifiedLocationDto
            {
                Id = UnifiedId.Encode(ProviderType.Partner, GetInt(hubJson, "id")?.ToString()),
                ProviderType = ProviderType.Partner,
                Name = GetString(hubJson, "chargingHubName"),
                AddressLine1 = GetString(hubJson, "addressLine1"),
                City = GetString(hubJson, "city"),
                State = GetString(hubJson, "state"),
                Pincode = GetString(hubJson, "pincode"),
                Latitude = GetString(hubJson, "latitude"),
                Longitude = GetString(hubJson, "longitude"),
                DistanceKm = GetDouble(entry, "distanceKm"),
                TotalStations = GetInt(entry, "totalStations") ?? 0,
                AvailableStations = GetInt(entry, "availableStations") ?? 0,
                TotalConnectors = GetInt(entry, "totalConnectors") ?? 0,
                AvailableConnectors = GetInt(entry, "availableConnectors") ?? 0,
                PartnerName = GetString(hubJson, "partnerName"),
                Stations = GetArray(entry, "stations").Select(MapPartnerStationJsonToUnified).ToList()
            };
        }

        private static UnifiedSessionDto MapPartnerListItemToUnified(JsonElement s)
        {
            var status = GetString(s, "status");
            var limitProgressJson = GetObj(s, "limitProgress");
            double? currentSoC = GetDouble(s, "currentStateOfCharge");

            return new UnifiedSessionDto
            {
                Id = UnifiedId.Encode(ProviderType.Partner, GetString(s, "sessionId")),
                ProviderType = ProviderType.Partner,
                Status = status,
                IsActive = string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase),
                StartTime = GetDateTime(s, "startDateTime") ?? DateTime.UtcNow,
                EndTime = GetDateTime(s, "endDateTime"),
                EnergyDelivered = GetDouble(s, "totalEnergyKwh") ?? 0,
                Cost = (decimal)(GetDouble(s, "totalCost") ?? 0),
                Currency = GetString(s, "currency") ?? "INR",
                LocationName = GetString(s, "locationName"),
                PartnerName = GetString(s, "partnerName"),
                ConnectorId = GetString(s, "evseUid"),
                EnergyLimit = GetDouble(s, "energyLimit"),
                CostLimit = GetDouble(s, "costLimit"),
                TimeLimit = GetInt(s, "timeLimit"),
                BatteryIncreaseLimit = GetDouble(s, "batteryIncreaseLimit"),
                LimitProgress = limitProgressJson.ValueKind == JsonValueKind.Object ? new UnifiedLimitProgressDto
                {
                    EnergyPct = GetDouble(limitProgressJson, "energyPct"),
                    CostPct = GetDouble(limitProgressJson, "costPct"),
                    TimePct = GetDouble(limitProgressJson, "timePct")
                } : null,
                BatteryStateOfCharge = currentSoC.HasValue ? new UnifiedBatteryStateOfChargeDto
                {
                    CurrentSoC = currentSoC,
                    LastUpdate = GetDateTime(s, "stateOfChargeLastUpdate"),
                    IsRealtime = false,
                    DataSource = "Partner CPO Report"
                } : null,
                Raw = s
            };
        }
    }
}
