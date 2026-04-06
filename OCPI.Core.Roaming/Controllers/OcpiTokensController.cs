using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;

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
        /// <summary>
        /// Real-time authorization request
        /// </summary>
        [HttpPost("{countryCode}/{partyId}/{tokenUid}/authorize")]
        public IActionResult AuthorizeToken(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tokenUid,
            [FromBody] OcpiLocationReferences? locationReferences = null)
        {
            // TODO: Check if token is valid in database
            // var token = await _dbContext.Tokens.FirstOrDefaultAsync(t => t.Uid == tokenUid);
            // if (token == null || token.Valid == false)
            // {
            //     return OcpiOk(new OcpiAuthorizationInfo { Allowed = OcpiAllowed.NotAllowed });
            // }

            // Return authorization decision
            var authInfo = new OcpiAuthorizationInfo
            {
                Allowed = AllowedType.Allowed, 
                LocationReferences = locationReferences,
                Info = new OcpiDisplayText
                {
                    Language = "en",
                    Text = "Authorized successfully"
                }
            };

            return OcpiOk(authInfo);
        }

        /// <summary>
        /// Receive token information from partner
        /// </summary>
        [HttpPut("{countryCode}/{partyId}/{tokenUid}")]
        public IActionResult PutToken(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tokenUid,
            [FromBody] OcpiToken token)
        {
            // Validate token data
            OcpiValidate(token);

            // TODO: Store/update token in database
            // _dbContext.Tokens.Update(MapToDbToken(token));
            // await _dbContext.SaveChangesAsync();

            return OcpiOk(token);
        }

        /// <summary>
        /// Partially update token information
        /// </summary>
        [HttpPatch("{countryCode}/{partyId}/{tokenUid}")]
        public IActionResult PatchToken(
            [FromRoute] string countryCode,
            [FromRoute] string partyId,
            [FromRoute] string tokenUid,
            [FromBody] OcpiToken token)
        {
            // Validate token data
            OcpiValidate(token);

            // TODO: Partially update token in database
            // var existing = await _dbContext.Tokens.FindAsync(tokenUid);
            // if (existing == null) throw OcpiException.UnknownLocation($"Token not found: {tokenUid}");
            // UpdatePartialToken(existing, token);
            // await _dbContext.SaveChangesAsync();

            return OcpiOk(token);
        }
    }
}
