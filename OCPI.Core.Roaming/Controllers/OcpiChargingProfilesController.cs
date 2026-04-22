using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts.ChargingProfiles;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Charging Profiles Sender Controller (CPO role)
    /// Allows eMSPs to set, get, and clear smart-charging profiles for active sessions.
    /// Routes follow OCPI 2.2.1 §12 chargingprofiles/{sessionId}
    /// </summary>
    [OcpiEndpoint(OcpiModule.ChargingProfiles, "Sender", "2.2.1")]
    [Route("2.2.1/chargingprofiles")]
    [OcpiAuthorize]
    public class OcpiChargingProfilesController : OcpiController
    {
        private readonly IOcpiChargingProfileService _profileService;
        private readonly ILogger<OcpiChargingProfilesController> _logger;

        public OcpiChargingProfilesController(
            IOcpiChargingProfileService profileService,
            ILogger<OcpiChargingProfilesController> logger)
        {
            _profileService = profileService;
            _logger = logger;
        }

        /// <summary>GET the active charging profile for a session</summary>
        [HttpGet("{sessionId}")]
        public async Task<IActionResult> GetActiveChargingProfile([FromRoute] string sessionId)
        {
            _logger.LogInformation("GET active charging profile for session {SessionId}", sessionId);

            var profile = await _profileService.GetActiveChargingProfileAsync(sessionId);
            if (profile == null)
                throw OcpiException.UnknownLocation($"No active charging profile found for session {sessionId}");

            return OcpiOk(profile);
        }

        /// <summary>PUT (set) a new charging profile for a session</summary>
        [HttpPut("{sessionId}")]
        public async Task<IActionResult> SetChargingProfile(
            [FromRoute] string sessionId,
            [FromBody] OcpiSetChargingProfileRequest request)
        {
            OcpiValidate(request);
            _logger.LogInformation("PUT charging profile for session {SessionId}", sessionId);

            var result = await _profileService.SetChargingProfileAsync(sessionId, request);

            return OcpiOk(new OcpiChargingProfileResponse
            {
                Result  = result,
                Timeout = 30
            });
        }

        /// <summary>DELETE (clear) the charging profile for a session</summary>
        [HttpDelete("{sessionId}")]
        public async Task<IActionResult> ClearChargingProfile(
            [FromRoute] string sessionId,
            [FromQuery] string? responseUrl = null)
        {
            _logger.LogInformation("DELETE (clear) charging profile for session {SessionId}", sessionId);

            var result = await _profileService.ClearChargingProfileAsync(sessionId, responseUrl);

            return OcpiOk(new OcpiChargingProfileResponse
            {
                Result  = result,
                Timeout = 30
            });
        }
    }
}
