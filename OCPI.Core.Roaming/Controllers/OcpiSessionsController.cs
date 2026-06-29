using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;
using OCPI;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Sessions Receiver Controller - Receives session data from partners (eMSP role)
    /// </summary>
    [OcpiEndpoint(OcpiModule.Sessions, "Receiver", "2.2.1")]
    [Route("2.2.1/sessions")]
    [OcpiAuthorize]
    public class OcpiSessionsController : OcpiController
    {
        private readonly IOcpiSessionService _sessionService;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly ILogger<OcpiSessionsController> _logger;

        public OcpiSessionsController(
            IOcpiSessionService sessionService,
            IOcpiCredentialsService credentialsService,
            ILogger<OcpiSessionsController> logger)
        {
            _sessionService = sessionService;
            _credentialsService = credentialsService;
            _logger = logger;
        }
        /// <summary>
        /// Get a stored partner session (CPO reads back what is stored on eMSP side)
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{sessionId}")]
        public async Task<IActionResult> GetSession(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string sessionId)
        {
            var stored = await _sessionService.GetPartnerSessionAsync(sessionId);

            if (stored == null)
                throw OcpiException.UnknownLocation($"Session not found: {sessionId}");

            var session = new OcpiSession
            {
                CountryCode            = OcpiEnumMemberHelper.ParseMemberValue<CountryCode>(stored.CountryCode), //, true, out var cc) ? cc : (CountryCode?)null,
                PartyId                = stored.PartyId,
                Id                     = stored.SessionId,
                StartDateTime          = stored.StartDateTime,
                EndDateTime            = stored.EndDateTime,
                Kwh                    = stored.TotalEnergy,
                AuthorizationReference = stored.AuthorizationReference,
                LocationId             = stored.LocationId,
                EvseId                 = stored.EvseUid,
                ConnectorId            = stored.ConnectorId,
                Currency               = OcpiEnumMemberHelper.ParseMemberValue<CurrencyCode>(stored.Currency), //, true, out var cur) ? cur : (CurrencyCode?)null,
                TotalCost              = stored.TotalCost.HasValue ? new OcpiPrice { ExclVat = stored.TotalCost.Value } : null,
                Status                 = OcpiEnumMemberHelper.ParseMemberValue<SessionStatus>(stored.Status), //, true, out var st) ? st : (SessionStatus?)null,
                LastUpdated            = stored.LastUpdated,
                CdrToken               = string.IsNullOrEmpty(stored.TokenUid) ? null : new OcpiCdrToken { Uid = stored.TokenUid }
            };

            return OcpiOk(session);
        }

        /// <summary>
        /// Update or create a charging session
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{sessionId}")]
        public async Task<IActionResult> PutSession(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string sessionId,
            [FromBody] OcpiSession session)
        {
            // Validate session data
            // OcpiValidate(session);

            // Get partner credential from Authorization header
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(token);

            if (partner == null)
                throw OcpiException.InvalidParameters("Invalid partner credentials");

            // Store/update session in database
            await _sessionService.StorePartnerSessionAsync(partner.Id, session);

            _logger.LogInformation("Stored partner session {SessionId}", sessionId);

            return OcpiOk(session);
        }

        /// <summary>
        /// Update charging session via partial PATCH
        /// </summary>
        [HttpPatch("{countryCode}/{partyId}/{sessionId}")]
        public async Task<IActionResult> PatchSession(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string sessionId,
            [FromBody] OcpiSession session)
        {
            // Validate session data
            // OcpiValidate(session);

            // Partially update session in database
            var existing = await _sessionService.GetPartnerSessionAsync(sessionId);
            if (existing == null)
                throw OcpiException.UnknownLocation($"Session not found: {sessionId}");

            await _sessionService.UpdatePartnerSessionAsync(sessionId, session);

            _logger.LogInformation("Updated partner session {SessionId}", sessionId);

            return OcpiOk(session);
        }
    }
}