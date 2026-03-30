using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppVersionInfoController : ControllerBase
    {
        private readonly ILogger<AppVersionInfoController> _logger;
        private readonly IConfiguration _config;

        public AppVersionInfoController(ILogger<AppVersionInfoController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetAppVersionInfo()
        {
            var section = _config.GetSection("AppVersionInfo");

            var data = new AppVersionInfoDto
            {
                latest_version_android = section["latest_version_android"],
                latest_version_ios = section["latest_version_ios"],
                force_update = section.GetValue<bool>("force_update"),
                message = section["message"],
                android_store_url = section["android_store_url"],
                ios_store_url = section["ios_store_url"]
            };

            return Ok(new AppVersionInfoResponseDto { Status = true, Data = data });
        }
    }
}
