using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Sessions Sender Controller (CPO role)
    /// Provides eMSPs with access to our live charging session data.
    /// </summary>
    [OcpiEndpoint(OcpiModule.Sessions, "Sender", "2.2.1")]
    [Route("2.2.1/sessions/sender")]
    [OcpiAuthorize]
    public class OcpiSessions_SenderController : OcpiController
    {
        private readonly IChargingSessionService _sessionService;
        private readonly ILogger<OcpiSessions_SenderController> _logger;

        public OcpiSessions_SenderController(
            IChargingSessionService sessionService,
            ILogger<OcpiSessions_SenderController> logger)
        {
            _sessionService = sessionService;
            _logger = logger;
        }

        /// <summary>List all active sessions</summary>
        [HttpGet]
        public async Task<IActionResult> GetActiveSessions()
        {
            _logger.LogInformation("GET active sessions requested");
            var sessions = await _sessionService.GetActiveOcpiSessionsAsync();
            return OcpiOk(sessions);
        }

        /// <summary>Get a specific session by ID</summary>
        [HttpGet("{countryCode}/{partyId}/{sessionId}")]
        public async Task<IActionResult> GetSession(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string sessionId)
        {
            _logger.LogInformation("GET session {SessionId}", sessionId);
            var session = await _sessionService.GetOcpiSessionAsync(sessionId);

            if (session == null)
                throw OcpiException.UnknownLocation($"Session not found: {sessionId}");

            return OcpiOk(session);
        }
    }
}
