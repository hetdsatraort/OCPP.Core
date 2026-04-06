using Microsoft.AspNetCore.Mvc;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Versions endpoint
    /// This controller uses the built-in IOcpiVersionService which automatically scans
    /// all controllers with [OcpiEndpoint] attribute and generates version information
    /// </summary>
    [Route("versions")]
    public class OcpiVersionsController : OcpiController
    {
        private readonly IOcpiVersionService _versionService;

        public OcpiVersionsController(IOcpiVersionService versionService)
        {
            _versionService = versionService;
        }

        /// <summary>
        /// Get all supported OCPI versions
        /// </summary>
        [HttpGet]
        public IActionResult GetVersions()
        {
            var versions = _versionService.GetVersions();
            return OcpiOk(versions);
        }

        /// <summary>
        /// Get detailed endpoint information for a specific OCPI version
        /// </summary>
        [HttpGet("{version}")]
        public IActionResult GetVersionDetails([FromRoute] string version)
        {
            var details = _versionService.GetVersionDetails(version);
            return OcpiOk(details);
        }
    }
}
