using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Tokens Receiver Controller - Receives and authorizes tokens from partners
    /// </summary>
    [OcpiEndpoint(OcpiModule.Tokens, "Receiver", "2.2.1")]
    [Route("2.2.1/tokens")]
    [OcpiAuthorize]
    public class OcpiTokensController : OcpiController
    {
        private readonly IOcpiTokenService _tokenService;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly ILogger<OcpiTokensController> _logger;

        public OcpiTokensController(
            IOcpiTokenService tokenService,
            IOcpiCredentialsService credentialsService,
            ILogger<OcpiTokensController> logger)
        {
            _tokenService = tokenService;
            _credentialsService = credentialsService;
            _logger = logger;
        }
        /// <summary>
        /// Real-time authorization request
        /// </summary>
        [HttpPost("{countryCode}/{partyId}/{tokenUid}/authorize")]
        public async Task<IActionResult> AuthorizeToken(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tokenUid,
            [FromBody] OcpiLocationReferences? locationReferences = null)
        {
            // Check if token is valid in database
            var authInfo = await _tokenService.AuthorizeTokenAsync(tokenUid, locationReferences);

            _logger.LogInformation("Token authorization request for {TokenUid}: {Allowed}", 
                tokenUid, authInfo.Allowed);

            return OcpiOk(authInfo);
        }

        /// <summary>
        /// Receive token information from partner
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{tokenUid}")]
        public async Task<IActionResult> PutToken(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tokenUid,
            [FromBody] OcpiToken token)
        {
            // Validate token data
            OcpiValidate(token);

            // Get partner credential from Authorization header
            var authToken = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(authToken);

            if (partner == null)
                throw OcpiException.InvalidParameters("Invalid partner credentials");

            // Store/update token in database
            await _tokenService.StorePartnerTokenAsync(partner.Id, token);

            _logger.LogInformation("Stored partner token {TokenUid}", tokenUid);

            return OcpiOk(token);
        }

        /// <summary>
        /// Partially update token information
        /// </summary>
        [HttpPatch("{countryCode}/{partyId}/{tokenUid}")]
        public async Task<IActionResult> PatchToken(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tokenUid,
            [FromBody] OcpiToken token)
        {
            // Validate token data
            OcpiValidate(token);

            // Partially update token in database
            var existing = await _tokenService.GetPartnerTokenAsync(tokenUid);
            if (existing == null)
                throw OcpiException.UnknownLocation($"Token not found: {tokenUid}");

            await _tokenService.UpdatePartnerTokenAsync(tokenUid, token);

            _logger.LogInformation("Updated partner token {TokenUid}", tokenUid);

            return OcpiOk(token);
        }
    }
}
