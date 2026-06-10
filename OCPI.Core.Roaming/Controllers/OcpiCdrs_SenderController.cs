using BitzArt.Pagination;
using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI CDRs Sender Controller (CPO role) — OCPI 2.2.1 §10
    /// Serves paginated CDR lists and individual CDRs to eMSP partners.
    /// Route deliberately suffixed with /sender to avoid conflict with the
    /// Receiver controller's POST /2.2.1/cdrs endpoint.
    /// </summary>
    [OcpiEndpoint(OcpiModule.CDRs, "Sender", "2.2.1")]
    [Route("2.2.1/cdrs/sender")]
    [OcpiAuthorize]
    public class OcpiCdrs_SenderController : OcpiController
    {
        private readonly IOcpiCdrService _cdrService;
        private readonly ILogger<OcpiCdrs_SenderController> _logger;

        public OcpiCdrs_SenderController(
            IOcpiCdrService cdrService,
            ILogger<OcpiCdrs_SenderController> logger)
        {
            _cdrService = cdrService;
            _logger     = logger;
        }

        /// <summary>
        /// GET paginated list of CDRs — CPO Sender interface.
        /// Supports filtering by date_from / date_to (UTC).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCdrs(
            [FromQuery] OcpiPageRequest pageRequest,
            [FromQuery(Name = "date_from")] DateTime? dateFrom = null,
            [FromQuery(Name = "date_to")]   DateTime? dateTo   = null)
        {
            SetMaxLimit(pageRequest, 100);
            pageRequest.Offset ??= 0;
            pageRequest.Limit ??= 100;

            var cdrs  = await _cdrService.GetCdrsAsync(dateFrom, dateTo, pageRequest.Offset.Value, pageRequest.Limit.Value);
            var total = await _cdrService.GetCdrCountAsync(dateFrom, dateTo);

            _logger.LogInformation(
                "GET CDRs (sender): offset={Offset}, limit={Limit}, dateFrom={DateFrom}, dateTo={DateTo}, total={Total}",
                pageRequest.Offset.Value, pageRequest.Limit.Value, dateFrom, dateTo, total);

            var result = new PageResult<OcpiCdr, OcpiPageRequest>(cdrs, pageRequest, total);
            return OcpiOk(result);
        }

        /// <summary>
        /// GET a single CDR by compound key (countryCode / partyId / cdrId) — CPO Sender interface.
        /// </summary>
        [HttpGet("{countryCode}/{partyId}/{cdrId}")]
        public async Task<IActionResult> GetCdr(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string cdrId)
        {
            _logger.LogInformation(
                "GET CDR (sender) {CdrId} for partner {CountryCode}/{PartyId}", cdrId, countryCode, partyId);

            var cdr = await _cdrService.GetCdrAsync(cdrId);

            if (cdr == null)
                throw OcpiException.UnknownLocation($"CDR not found: {cdrId}");

            return OcpiOk(cdr);
        }
    }
}
