using Microsoft.AspNetCore.Mvc;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Versions endpoint — OCPI 2.2.1 §4
    /// Uses our own IOcpiVersionService (OCPI.Core.Roaming.Services) which reflects
    /// over the assembly's [OcpiEndpoint]-decorated controllers at runtime.
    /// This replaces the NuGet IOcpiVersionService entirely.
    /// </summary>
    [Route("versions")]
    public class OcpiVersionsController : OcpiController
    {
        // Fully-qualified to resolve ambiguity with the OCPI.Net NuGet IOcpiVersionService
        private readonly OCPI.Core.Roaming.Services.IOcpiVersionService _versionService;

        public OcpiVersionsController(OCPI.Core.Roaming.Services.IOcpiVersionService versionService)
        {
            _versionService = versionService;
        }

        /// <summary>Get all supported OCPI versions</summary>
        [HttpGet]
        public IActionResult GetVersions()
        {
            var versions = _versionService.GetVersions();
            return OcpiOk(versions);
        }

        /// <summary>Get detailed endpoint information for a specific OCPI version</summary>
        [HttpGet("{version}")]
        public IActionResult GetVersionDetails([FromRoute] string version)
        {
            var details = _versionService.GetVersionDetails(version);

            if (details == null)
                return OcpiOk(new { status_code = 3000, status_message = $"OCPI version '{version}' is not supported" });

            return OcpiOk(details);
        }
    }
}
