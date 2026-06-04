using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Credentials Controller - Handles credential registration and updates
    /// </summary>
    [OcpiEndpoint(OcpiModule.Credentials, "Receiver", "2.2.1")]
    [Route("2.2.1/credentials")]
    public class OcpiCredentialsController : OcpiController
    {
        private readonly IConfiguration _configuration;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly ILogger<OcpiCredentialsController> _logger;

        public OcpiCredentialsController(
            IConfiguration configuration,
            IOcpiCredentialsService credentialsService,
            ILogger<OcpiCredentialsController> logger)
        {
            _configuration = configuration;
            _credentialsService = credentialsService;
            _logger = logger;
        }

        /// <summary>
        /// Get current credentials
        /// </summary>
        [OcpiAuthorize]
        [HttpGet]
        public IActionResult Get()
        {
            // Return your platform's credentials
            var credentials = GetPlatformCredentials();
            return OcpiOk(credentials);
        }

        /// <summary>
        /// Register new partner credentials.
        /// Accepts either:
        ///   (a) An A-token issued via POST /admin/partners/issue-token — completes the
        ///       inbound handshake and returns a permanent B-token for the partner to use.
        ///   (b) An already-registered partner token — rejected (already registered).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] OcpiCredentials partnerCredentials)
        {
            // Manually extract the bearer token — [OcpiAuthorize] is intentionally absent
            // on POST so that brand-new partners can authenticate with an A-token.
            var authHeader = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader))
                return Unauthorized(new { status_code = 2001, status_message = "Authorization header missing" });

            var incomingToken = authHeader.Split(' ').Last();

            // 1. Check if this is a valid A-token (pending registration)
            var pending = await _credentialsService.GetPendingRegistrationByTokenAsync(incomingToken);

            // 2. If not an A-token, check if it belongs to an already-registered partner
            //    (e.g. they're re-trying POST instead of using PUT)
            if (pending == null)
            {
                var existingPartner = await _credentialsService.GetPartnerByTokenAsync(incomingToken);
                if (existingPartner == null)
                    return Unauthorized(new { status_code = 2001, status_message = "Unknown or expired token" });

                // Already registered — they should use PUT to update credentials
                return Conflict(new { status_code = 2001, status_message = "Platform is already registered. Use PUT to update credentials." });
            }

            OcpiValidate(partnerCredentials);

            var firstRole = partnerCredentials.Roles?.FirstOrDefault();
            if (firstRole == null)
                throw OcpiException.InvalidParameters("At least one role is required");

            // 3. Generate a permanent B-token the partner will use for all future calls to us
            var bToken = Guid.NewGuid().ToString("N");

            // 4. Persist the partner:
            //    token         = bToken     — the token THEY send US going forward (inbound auth)
            //    outboundToken = partnerCredentials.Token — the token WE send THEM (outbound auth)
            var partner = await _credentialsService.CreateOrUpdatePartnerAsync(
                bToken,
                partnerCredentials.Url,
                firstRole.CountryCode.ToString(),
                firstRole.PartyId,
                firstRole.BusinessDetails?.Name,
                firstRole.Role.ToString(),
                "2.2.1",
                outboundToken: partnerCredentials.Token
            );

            // 5. Mark the A-token as consumed
            await _credentialsService.MarkATokenUsedAsync(pending.Id, partner.Id);

            _logger.LogInformation(
                "Completed OCPI handshake for new partner: {BusinessName} ({CountryCode}-{PartyId}). B-token issued.",
                firstRole.BusinessDetails?.Name, firstRole.CountryCode, firstRole.PartyId);

            // 6. Return OUR credentials — the B-token is embedded so the partner knows
            //    what token to send in Authorization headers when calling us going forward.
            var ourCredentials = GetPlatformCredentials(bToken);
            return OcpiOk(ourCredentials);
        }

        /// <summary>
        /// Update existing partner credentials
        /// </summary>
        [OcpiAuthorize]
        [HttpPut]
        public async Task<IActionResult> Put([FromBody] OcpiCredentials partnerCredentials)
        {
            // Validate incoming credentials
            OcpiValidate(partnerCredentials);

            var firstRole = partnerCredentials.Roles?.FirstOrDefault();
            if (firstRole == null)
                throw OcpiException.InvalidParameters("At least one role is required");

            // Check if platform is registered
            var existing = await _credentialsService.GetPartnerByCountryAndPartyAsync(
                firstRole.CountryCode.ToString(),
                firstRole.PartyId);

            if (existing == null)
                throw OcpiException.InvalidParameters("Platform must be registered first");

            // Update partner credentials:
            //    Keep the existing inbound token (token = existing.Token) unchanged.
            //    The new outboundToken from the PUT body is what WE use when calling THEM.
            await _credentialsService.CreateOrUpdatePartnerAsync(
                existing.Token,
                partnerCredentials.Url,
                firstRole.CountryCode.ToString(),
                firstRole.PartyId,
                firstRole.BusinessDetails?.Name,
                firstRole.Role.ToString(),
                "2.2.1",
                outboundToken: partnerCredentials.Token
            );

            _logger.LogInformation("Updated OCPI partner credentials: {BusinessName}", 
                firstRole.BusinessDetails?.Name);

            // Return your platform's credentials
            var credentials = GetPlatformCredentials();
            return OcpiOk(credentials);
        }

        /// <summary>
        /// Unregister partner
        /// </summary>
        [OcpiAuthorize]
        [HttpDelete]
        public async Task<IActionResult> Delete()
        {
            // Get partner token from Authorization header
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");

            var existing = await _credentialsService.GetPartnerByTokenAsync(token);
            
            if (existing == null)
                throw OcpiException.InvalidParameters("Platform must be registered first");

            // Remove partner credentials from database
            await _credentialsService.DeletePartnerAsync(token);

            _logger.LogInformation("Unregistered OCPI partner: {CountryCode}-{PartyId}", 
                existing.CountryCode, existing.PartyId);

            return OcpiOk("Successfully unregistered");
        }

        /// <param name="bToken">
        /// When supplied (A-token flow) this is the newly-generated B-token.
        /// The partner stores this token and sends it in Authorization headers going forward.
        /// When null the static configured token is used (for GET / PUT flows).
        /// </param>
        private OcpiCredentials GetPlatformCredentials(string? bToken = null)
        {
            return new OcpiCredentials
            {
                Token = bToken ?? _configuration["OCPI:Token"] ?? Guid.NewGuid().ToString(),
                Url = _configuration["OCPI:BaseUrl"] ?? "https://localhost:5001/versions",
                Roles = new List<OcpiCredentialsRole>
                {
                    new OcpiCredentialsRole
                    {
                        CountryCode = CountryCode.India,
                        PartyId = _configuration["OCPI:PartyId"] ?? "CPO",
                        Role = PartyRole.Cpo,
                        BusinessDetails = new OcpiBusinessDetails
                        {
                            Name = _configuration["OCPI:BusinessName"] ?? "EV Charging Platform",
                            Website = _configuration["OCPI:Website"] ?? "https://evcharging.com"
                        }
                    }
                }
            };
        }
    }
}
