using Microsoft.AspNetCore.Mvc;
using OCPP.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Hub Client Info Controller (Hub module)
    /// Reports connected eMSP clients to a Hub operator.
    /// OCPI 2.2.1 §16 hubclientinfo
    /// </summary>
    [OcpiEndpoint(OcpiModule.HubClientInfo, "Receiver", "2.2.1")]
    [Route("2.2.1/hubclientinfo")]
    [OcpiAuthorize]
    public class OcpiHubClientInfoController : OcpiController
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiHubClientInfoController> _logger;

        public OcpiHubClientInfoController(
            OCPPCoreContext dbContext,
            ILogger<OcpiHubClientInfoController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// GET hub client info — returns all active OCPI partner connections.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHubClientInfo()
        {
            _logger.LogInformation("GET hub client info requested");

            var partners = await _dbContext.OcpiPartnerCredentials
                .Where(p => p.IsActive)
                .Select(p => new HubClientInfoDto
                {
                    PartyId        = p.PartyId,
                    CountryCode    = p.CountryCode,
                    Role           = p.Role,
                    Status         = "CONNECTED",
                    LastUpdated    = p.LastSyncOn ?? p.CreatedOn
                })
                .ToListAsync();

            return OcpiOk(partners);
        }

        /// <summary>
        /// PUT hub client info — receive status update from Hub about a client.
        /// </summary>
        [HttpPut("{countryCode}/{partyId}")]
        public async Task<IActionResult> PutHubClientInfo(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromBody] HubClientInfoUpdateDto update)
        {
            _logger.LogInformation("PUT hub client info for {CountryCode}/{PartyId}: status={Status}",
                countryCode, partyId, update.Status);

            var partner = await _dbContext.OcpiPartnerCredentials
                .FirstOrDefaultAsync(p => p.CountryCode == countryCode
                    && p.PartyId == partyId && p.IsActive);

            if (partner == null)
                throw OcpiException.UnknownLocation($"Partner {countryCode}/{partyId} not found");

            // Update last sync based on hub-reported status
            if (update.Status == "OFFLINE")
                partner.IsActive = false;

            partner.LastSyncOn = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return OcpiOk(new { countryCode, partyId, status = update.Status });
        }
    }

    public class HubClientInfoDto
    {
        public string? PartyId     { get; set; }
        public string? CountryCode { get; set; }
        public string? Role        { get; set; }
        public string? Status      { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class HubClientInfoUpdateDto
    {
        public string? Status { get; set; }
    }
}
