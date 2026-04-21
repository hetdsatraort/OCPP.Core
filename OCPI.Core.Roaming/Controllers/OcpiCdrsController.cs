using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI CDRs (Charge Detail Records) Receiver Controller
    /// Receives CDRs from partners for billing/settlement
    /// </summary>
    [OcpiEndpoint(OcpiModule.CDRs, "Receiver", "2.2.1")]
    [Route("2.2.1/cdrs")]
    [OcpiAuthorize]
    public class OcpiCdrsController : OcpiController
    {
        private readonly IOcpiCdrService _cdrService;
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly ILogger<OcpiCdrsController> _logger;

        public OcpiCdrsController(
            IOcpiCdrService cdrService,
            IOcpiCredentialsService credentialsService,
            ILogger<OcpiCdrsController> logger)
        {
            _cdrService = cdrService;
            _credentialsService = credentialsService;
            _logger = logger;
        }
        /// <summary>
        /// Receive and store a Charge Detail Record
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PostCdr([FromBody] OcpiCdr cdr)
        {
            // Validate CDR data
            OcpiValidate(cdr);

            // Get partner credential from Authorization header
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Token ", "");
            var partner = await _credentialsService.GetPartnerByTokenAsync(token);

            // Store CDR in database
            var cdrId = await _cdrService.CreateCdrAsync(cdr, partner?.Id);

            _logger.LogInformation("Stored CDR {CdrId} from partner", cdrId);

            // Generate CDR ID or URL where it can be retrieved
            var cdrLocation = $"/2.2.1/cdrs/{cdr.Id}";
            
            Response.Headers.Append("Location", cdrLocation);
            return OcpiOk(cdr);
        }

        /// <summary>
        /// Get a specific CDR by ID
        /// </summary>
        [HttpGet("{cdrId}")]
        public async Task<IActionResult> GetCdr([FromRoute] string cdrId)
        {
            var cdr = await _cdrService.GetCdrAsync(cdrId);
            
            if (cdr == null)
                throw OcpiException.UnknownLocation($"CDR not found: {cdrId}");

            return OcpiOk(cdr);
        }
    }
}
