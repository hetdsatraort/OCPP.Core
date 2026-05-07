using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;
using OCPP.Core.Database;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// Admin controller for the OCPI Roaming front-end panel.
    /// Provides unauthenticated (internal-network) access for managing our chargers
    /// and viewing partner networks.  Secure this endpoint at the API-gateway level
    /// before exposing it publicly.
    /// </summary>
    [ApiController]
    [Route("admin")]
    public class OcpiAdminController : ControllerBase
    {
        private readonly IOcpiLocationService _locationService;
        private readonly IOcpiCommandService _commandService;
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiAdminController> _logger;

        public OcpiAdminController(
            IOcpiLocationService locationService,
            IOcpiCommandService commandService,
            OCPPCoreContext dbContext,
            ILogger<OcpiAdminController> logger)
        {
            _locationService = locationService;
            _commandService = commandService;
            _dbContext = dbContext;
            _logger = logger;
        }

        // ── Our Locations ──────────────────────────────────────────────────────

        /// <summary>Get all our OCPI locations (hubs → stations → guns)</summary>
        [HttpGet("locations")]
        public async Task<IActionResult> GetOurLocations(
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 100)
        {
            var locations = await _locationService.GetOurLocationsAsync(offset, limit);

            var result = locations.Select(l => new
            {
                id          = l.Id,
                name        = l.Name,
                address     = l.Address,
                city        = l.City,
                latitude    = l.Coordinates?.Latitude,
                longitude   = l.Coordinates?.Longitude,
                evses = (l.Evses ?? Enumerable.Empty<OcpiEvse>()).Select(e => new
                {
                    uid    = e.Uid,
                    evseId = e.EvseId,
                    status = e.Status.ToString(),
                    physicalReference = e.PhysicalReference,
                    connectors = (e.Connectors ?? Enumerable.Empty<OcpiConnector>()).Select(c => new
                    {
                        id         = c.Id,
                        standard   = c.Standard.ToString(),
                        format     = c.Format.ToString(),
                        powerType  = c.PowerType.ToString(),
                        maxVoltage  = c.MaxVoltage,
                        maxAmperage = c.MaxAmperage,
                        maxElectricPower = c.MaxElectricPower
                    })
                })
            });

            return Ok(new { success = true, data = result });
        }

        // ── Partners ───────────────────────────────────────────────────────────

        /// <summary>List all active OCPI partner credentials (tokens are never returned)</summary>
        [HttpGet("partners")]
        public async Task<IActionResult> GetPartners()
        {
            var partners = await _dbContext.OcpiPartnerCredentials
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    id           = p.Id,
                    countryCode  = p.CountryCode,
                    partyId      = p.PartyId,
                    businessName = p.BusinessName,
                    role         = p.Role,
                    version      = p.Version,
                    url          = p.Url,
                    createdOn    = p.CreatedOn
                })
                .OrderBy(p => p.businessName)
                .ToListAsync();

            return Ok(new { success = true, data = partners });
        }

        /// <summary>Get a partner's synced locations (from our local DB copy)</summary>
        [HttpGet("partners/{partnerId:int}/locations")]
        public async Task<IActionResult> GetPartnerLocations([FromRoute] int partnerId)
        {
            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.Id == partnerId && p.IsActive);

            if (partner == null)
                return NotFound(new { success = false, message = "Partner not found" });

            var locations = await _dbContext.OcpiPartnerLocations
                .Where(l => l.PartnerCredentialId == partnerId)
                .ToListAsync();

            var locationIds = locations.Select(l => l.Id).ToList();

            var evses = await _dbContext.OcpiPartnerEvses
                .Where(e => locationIds.Contains(e.PartnerLocationId))
                .ToListAsync();

            var result = locations.Select(l => new
            {
                id         = l.Id,
                locationId = l.LocationId,
                name       = l.Name,
                address    = l.Address,
                city       = l.City,
                country    = l.Country,
                latitude   = l.Latitude,
                longitude  = l.Longitude,
                evses = evses
                    .Where(e => e.PartnerLocationId == l.Id)
                    .Select(e => new
                    {
                        id          = e.Id,
                        evseUid     = e.EvseUid,
                        evseId      = e.EvseId,
                        status      = e.Status ?? "UNKNOWN",
                        lastUpdated = e.LastUpdated
                    })
            });

            return Ok(new { success = true, data = result });
        }

        // ── Commands (Our Chargers) ────────────────────────────────────────────

        /// <summary>Start a charging session on one of our chargers via OCPI command</summary>
        [HttpPost("commands/start")]
        public async Task<IActionResult> StartSession([FromBody] AdminStartRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LocationId))
                return BadRequest(new { success = false, message = "LocationId is required" });

            var command = new OcpiStartSessionCommand
            {
                ResponseUrl = "https://noop.local/callback",
                Token       = new OcpiToken { Uid = request.TagUid },
                LocationId  = request.LocationId,
                EvseUid     = request.EvseUid,
                ConnectorId = request.ConnectorId
            };

            _logger.LogInformation(
                "[Admin] START_SESSION location={LocationId} evse={EvseUid} connector={ConnectorId} tag={Tag}",
                request.LocationId, request.EvseUid, request.ConnectorId, request.TagUid);

            var (result, sessionId) = await _commandService.HandleStartSessionAsync(command);
            return Ok(new
            {
                success   = result == CommandResponseType.Accepted,
                result    = result.ToString(),
                sessionId = result == CommandResponseType.Accepted ? sessionId : null
            });
        }

        /// <summary>Stop an active charging session on one of our chargers</summary>
        [HttpPost("commands/stop")]
        public async Task<IActionResult> StopSession([FromBody] AdminStopRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
                return BadRequest(new { success = false, message = "SessionId is required" });

            var command = new OcpiStopSessionCommand
            {
                ResponseUrl = "https://noop.local/callback",
                SessionId   = request.SessionId
            };

            _logger.LogInformation("[Admin] STOP_SESSION sessionId={SessionId}", request.SessionId);

            var result = await _commandService.HandleStopSessionAsync(command);
            return Ok(new
            {
                success = result == CommandResponseType.Accepted,
                result  = result.ToString()
            });
        }

        /// <summary>Send an unlock connector command to one of our chargers</summary>
        [HttpPost("commands/unlock")]
        public async Task<IActionResult> UnlockConnector([FromBody] AdminUnlockRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.LocationId) ||
                string.IsNullOrWhiteSpace(request.EvseUid) ||
                string.IsNullOrWhiteSpace(request.ConnectorId))
                return BadRequest(new { success = false, message = "LocationId, EvseUid and ConnectorId are all required" });

            var command = new OcpiUnlockConnectorCommand
            {
                ResponseUrl = "https://noop.local/callback",
                LocationId  = request.LocationId,
                EvseUid     = request.EvseUid,
                ConnectorId = request.ConnectorId
            };

            _logger.LogInformation(
                "[Admin] UNLOCK_CONNECTOR location={LocationId} evse={EvseUid} connector={ConnectorId}",
                request.LocationId, request.EvseUid, request.ConnectorId);

            var result = await _commandService.HandleUnlockConnectorAsync(command);
            return Ok(new
            {
                success = result == CommandResponseType.Accepted,
                result  = result.ToString()
            });
        }
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────

    public record AdminStartRequest(
        string  LocationId,
        string? EvseUid,
        string? ConnectorId,
        string  TagUid);

    public record AdminStopRequest(string SessionId);

    public record AdminUnlockRequest(
        string LocationId,
        string EvseUid,
        string ConnectorId);
}
