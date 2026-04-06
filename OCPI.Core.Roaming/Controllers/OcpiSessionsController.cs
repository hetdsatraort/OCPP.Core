using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCPI.Core.Roaming.Models.OCPI;
using OCPI.Core.Roaming.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Sessions Controller - Manages charging session information
    /// </summary>
    [Route("ocpi/2.2.1/sessions")]
    [ApiController]
    public class OcpiSessionsController : ControllerBase
    {
        private readonly IOcpiSessionService _sessionService;
        private readonly ILogger<OcpiSessionsController> _logger;

        public OcpiSessionsController(
            IOcpiSessionService sessionService,
            ILogger<OcpiSessionsController> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        /// <summary>
        /// Get all sessions
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(OcpiPagedResponseDto<OcpiSessionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessions(
            [FromQuery] string countryCode = null,
            [FromQuery] string partyId = null,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 100)
        {
            try
            {
                _logger.LogInformation($"OCPI GetSessions called. CountryCode: {countryCode}, PartyId: {partyId}");

                var sessions = await _sessionService.GetSessionsAsync(countryCode, partyId);

                var response = new OcpiPagedResponseDto<OcpiSessionDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = sessions,
                    Timestamp = DateTime.UtcNow,
                    Limit = limit,
                    Offset = offset,
                    TotalCount = sessions.Count
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetSessions");

                var errorResponse = new OcpiPagedResponseDto<OcpiSessionDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Get a specific session by ID
        /// </summary>
        [HttpGet("{sessionId}")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiSessionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiSessionDto>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSession([FromRoute] string sessionId)
        {
            try
            {
                _logger.LogInformation($"OCPI GetSession called for: {sessionId}");

                var session = await _sessionService.GetSessionByIdAsync(sessionId);

                if (session == null)
                {
                    var notFoundResponse = new OcpiResponseDto<OcpiSessionDto>
                    {
                        StatusCode = 3001,
                        StatusMessage = $"Session not found: {sessionId}",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return NotFound(notFoundResponse);
                }

                var response = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = session,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetSession for: {sessionId}");

                var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Create or update a session
        /// </summary>
        [HttpPut("{sessionId}")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiSessionDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiSessionDto>), StatusCodes.Status201Created)]
        public async Task<IActionResult> PutSession([FromRoute] string sessionId, [FromBody] OcpiSessionDto session)
        {
            try
            {
                _logger.LogInformation($"OCPI PutSession called for: {sessionId}");

                if (!ModelState.IsValid)
                {
                    var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                    {
                        StatusCode = 2001,
                        StatusMessage = "Invalid or missing parameters",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return BadRequest(errorResponse);
                }

                session.Id = sessionId;
                var existingSession = await _sessionService.GetSessionByIdAsync(sessionId);
                var isNewSession = existingSession == null;

                var savedSession = await _sessionService.CreateOrUpdateSessionAsync(session);

                var response = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 1000,
                    StatusMessage = isNewSession ? "Session created" : "Session updated",
                    Data = savedSession,
                    Timestamp = DateTime.UtcNow
                };

                return isNewSession
                    ? StatusCode(StatusCodes.Status201Created, response)
                    : Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in PutSession for: {sessionId}");

                var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Start a new charging session (command)
        /// </summary>
        [HttpPost("start")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiSessionDto>), StatusCodes.Status201Created)]
        public async Task<IActionResult> StartSession([FromBody] StartSessionRequestDto request)
        {
            try
            {
                _logger.LogInformation($"OCPI StartSession called for location: {request.LocationId}");

                if (!ModelState.IsValid)
                {
                    var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                    {
                        StatusCode = 2001,
                        StatusMessage = "Invalid or missing parameters",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return BadRequest(errorResponse);
                }

                var session = await _sessionService.StartSessionAsync(request);

                var response = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Session started successfully",
                    Data = session,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid session start request");

                var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 3001,
                    StatusMessage = ex.Message,
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return BadRequest(errorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartSession");

                var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Stop an active charging session (command)
        /// </summary>
        [HttpPost("stop")]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiSessionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> StopSession([FromBody] StopSessionRequestDto request)
        {
            try
            {
                _logger.LogInformation($"OCPI StopSession called for session: {request.SessionId}");

                if (!ModelState.IsValid)
                {
                    var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                    {
                        StatusCode = 2001,
                        StatusMessage = "Invalid or missing parameters",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return BadRequest(errorResponse);
                }

                var session = await _sessionService.StopSessionAsync(request);

                var response = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Session stopped successfully",
                    Data = session,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"Invalid session stop request for: {request.SessionId}");

                var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 3001,
                    StatusMessage = ex.Message,
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return BadRequest(errorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in StopSession for: {request.SessionId}");

                var errorResponse = new OcpiResponseDto<OcpiSessionDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }
    }
}
