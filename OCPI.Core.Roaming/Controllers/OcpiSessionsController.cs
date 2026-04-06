using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;

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
        /// <summary>
        /// Update or create a charging session
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{sessionId}")]
        public IActionResult PutSession(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string sessionId,
            [FromBody] OcpiSession session)
        {
            // Validate session data
            OcpiValidate(session);

            // TODO: Store/update session in database
            // _dbContext.Sessions.Add(MapToDbSession(session));
            // await _dbContext.SaveChangesAsync();

            return OcpiOk(session);
        }

        /// <summary>
        /// Update charging session via partial PATCH
        /// </summary>
        [HttpPatch("{countryCode}/{partyId}/{sessionId}")]
        public IActionResult PatchSession(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string sessionId,
            [FromBody] OcpiSession session)
        {
            // Validate session data
            OcpiValidate(session);

            // TODO: Partially update session in database
            // var existing = await _dbContext.Sessions.FindAsync(sessionId);
            // if (existing == null) throw OcpiException.UnknownLocation($"Session not found: {sessionId}");
            // UpdatePartialSession(existing, session);
            // await _dbContext.SaveChangesAsync();

            return OcpiOk(session);
        }
    }
}