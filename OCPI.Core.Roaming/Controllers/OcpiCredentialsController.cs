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
    [OcpiAuthorize]
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
        [HttpGet]
        public IActionResult Get()
        {
            // Return your platform's credentials
            var credentials = GetPlatformCredentials();
            return OcpiOk(credentials);
        }

        /// <summary>
        /// Register new partner credentials
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] OcpiCredentials partnerCredentials)
        {
            // Validate incoming credentials
            OcpiValidate(partnerCredentials);

            var firstRole = partnerCredentials.Roles?.FirstOrDefault();
            if (firstRole == null)
                throw OcpiException.InvalidParameters("At least one role is required");

            // Check if platform is already registered
            var existing = await _credentialsService.GetPartnerByCountryAndPartyAsync(
                firstRole.CountryCode.ToString(),
                firstRole.PartyId);

            if (existing != null)
                throw OcpiException.InvalidParameters("Platform is already registered");

            // Store partner credentials in database
            await _credentialsService.CreateOrUpdatePartnerAsync(
                partnerCredentials.Token,
                partnerCredentials.Url,
                firstRole.CountryCode.ToString(),
                firstRole.PartyId,
                firstRole.BusinessDetails?.Name,
                firstRole.Role.ToString(),
                "2.2.1"
            );

            _logger.LogInformation("Registered new OCPI partner: {BusinessName}", 
                firstRole.BusinessDetails?.Name);

            // Return your platform's credentials
            var credentials = GetPlatformCredentials();
            return OcpiOk(credentials);
        }

        /// <summary>
        /// Update existing partner credentials
        /// </summary>
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

            // Update partner credentials in database
            await _credentialsService.CreateOrUpdatePartnerAsync(
                partnerCredentials.Token,
                partnerCredentials.Url,
                firstRole.CountryCode.ToString(),
                firstRole.PartyId,
                firstRole.BusinessDetails?.Name,
                firstRole.Role.ToString(),
                "2.2.1"
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

        private OcpiCredentials GetPlatformCredentials()
        {
            return new OcpiCredentials
            {
                Token = _configuration["OCPI:Token"] ?? Guid.NewGuid().ToString(),
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
