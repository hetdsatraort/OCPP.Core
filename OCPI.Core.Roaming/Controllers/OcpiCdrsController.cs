using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;

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
        /// <summary>
        /// Receive and store a Charge Detail Record
        /// </summary>
        [HttpPost]
        public IActionResult PostCdr([FromBody] OcpiCdr cdr)
        {
            // Validate CDR data
            OcpiValidate(cdr);

            // TODO: Store CDR in database
            // _dbContext.Cdrs.Add(MapToDbCdr(cdr));
            // await _dbContext.SaveChangesAsync();

            // Generate CDR ID or URL where it can be retrieved
            var cdrLocation = $"/2.2.1/cdrs/{cdr.Id}";
            
            Response.Headers.Append("Location", cdrLocation);
            return OcpiOk(cdr);
        }

        /// <summary>
        /// Get a specific CDR by ID
        /// </summary>
        [HttpGet("{cdrId}")]
        public IActionResult GetCdr([FromRoute] string cdrId)
        {
            // TODO: Fetch from database
            // var cdr = await _dbContext.Cdrs.FindAsync(cdrId);
            // if (cdr == null) throw OcpiException.UnknownLocation($"CDR not found: {cdrId}");

            var sampleCdr = CreateSampleCdr(cdrId);
            return OcpiOk(sampleCdr);
        }

        private OcpiCdr CreateSampleCdr(string id)
        {
            return new OcpiCdr
            {
                CountryCode = CountryCode.India,
                PartyId = "CPO",
                Id = id,
                StartDateTime = DateTime.UtcNow.AddHours(-2),
                EndDateTime = DateTime.UtcNow,
                AuthorizationReference = "AUTH123",
                AuthMethod = AuthMethodType.AuthRequest,
                CdrLocation = new Contracts.OcpiCdrLocation
                {
                    Id = "LOC001",
                    Name = "Main Charging Hub",
                    Address = "123 Main Street",
                    City = "Los Angeles",
                    PostalCode = "90001",
                    Country = "India",
                    Coordinates = new OcpiGeolocation
                    {
                        Latitude = "34.0522",
                        Longitude = "-118.2437"
                    },
                    EvseUid = "EVSE-001",
                    EvseId = "IN*CPO*EVSE-001",
                    ConnectorId = "1",
                    ConnectorStandard = ConnectorType.IEC_62196_T2_Combo,
                    ConnectorFormat = ConnectorFormat.Socket,
                    ConnectorPowerType = PowerType.Ac3Phase
                },
                Currency = CurrencyCode.IndianRupee,
                TotalCost = new OcpiPrice { ExclVat = 15.50m },
                TotalEnergy = 50.0m,
                TotalTime = 2.0m,
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}
