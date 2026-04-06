using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OCPI.Contracts;

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

        public OcpiCredentialsController(IConfiguration configuration)
        {
            _configuration = configuration;
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
        public IActionResult Post([FromBody] OcpiCredentials partnerCredentials)
        {
            // Validate incoming credentials
            OcpiValidate(partnerCredentials);

            // TODO: Check if platform is already registered
            // if (IsAlreadyRegistered(partnerCredentials.Token))
            //     throw OcpiException.MethodNotAllowed("Platform is already registered");

            // TODO: Store partner credentials in database
            // SavePartnerCredentials(partnerCredentials);

            // Return your platform's credentials
            var credentials = GetPlatformCredentials();
            return OcpiOk(credentials);
        }

        /// <summary>
        /// Update existing partner credentials
        /// </summary>
        [HttpPut]
        public IActionResult Put([FromBody] OcpiCredentials partnerCredentials)
        {
            // Validate incoming credentials
            OcpiValidate(partnerCredentials);

            // TODO: Check if platform is registered
            // if (!IsRegistered(partnerCredentials.Token))
            //     throw OcpiException.MethodNotAllowed("Platform must be registered first");

            // TODO: Update partner credentials in database
            // UpdatePartnerCredentials(partnerCredentials);

            // Return your platform's credentials (can be updated/rotated)
            var credentials = GetPlatformCredentials();
            return OcpiOk(credentials);
        }

        /// <summary>
        /// Unregister partner
        /// </summary>
        [HttpDelete]
        public IActionResult Delete()
        {
            // TODO: Check if platform is registered
            // if (!IsRegistered())
            //     throw OcpiException.MethodNotAllowed("Platform must be registered first");

            // TODO: Remove partner credentials from database
            // DeletePartnerCredentials();

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
