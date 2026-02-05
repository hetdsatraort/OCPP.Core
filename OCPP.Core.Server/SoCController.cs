using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using System;

namespace OCPP.Core.Server
{
    [ApiController]
    [Route("API/[controller]")]
    public class SoCController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        private readonly ILogger _logger;

        public SoCController(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(typeof(SoCController));
        }

        /// <summary>
        /// Get cached State of Charge for a specific connector
        /// </summary>
        [HttpGet("GetSoC")]
        public IActionResult GetSoC([FromQuery] string chargePointId, [FromQuery] int connectorId, [FromQuery] int maxAgeMinutes = 5)
        {
            try
            {
                if (string.IsNullOrEmpty(chargePointId))
                {
                    return new BadRequestObjectResult(new { Success = false, Message = "ChargePointId is required" });
                }

                _logger.LogTrace("GetSoC => ChargePointId={0}, ConnectorId={1}, MaxAgeMinutes={2}",
                    chargePointId, connectorId, maxAgeMinutes);

                var socData = SoCCache.GetIfRecent(chargePointId, connectorId, maxAgeMinutes);

                if (socData != null)
                {
                    _logger.LogInformation("GetSoC => Found cached SoC: {0}% for {1}/{2}",
                        socData.Value, chargePointId, connectorId);

                    return new OkObjectResult(new
                    {
                        Success = true,
                        Data = new
                        {
                            ChargePointId = chargePointId,
                            ConnectorId = connectorId,
                            SoC = Math.Round(socData.Value, 1),
                            Timestamp = socData.Timestamp,
                            TransactionId = socData.TransactionId,
                            AgeSeconds = (DateTime.UtcNow - socData.Timestamp).TotalSeconds
                        }
                    });
                }
                else
                {
                    _logger.LogInformation("GetSoC => No recent SoC data for {0}/{1}", chargePointId, connectorId);

                    return new OkObjectResult(new
                    {
                        Success = true,
                        Data = (object)null,
                        Message = "No recent SoC data available"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSoC => Exception: {0}", ex.Message);
                return new OkObjectResult(new { Success = false, Message = ex.Message });
            }
        }

        /// <summary>
        /// Clear cached SoC for a specific connector
        /// </summary>
        [HttpPost("ClearSoC")]
        public IActionResult ClearSoC([FromBody] ClearSoCRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request?.ChargePointId))
                {
                    return new BadRequestObjectResult(new { Success = false, Message = "ChargePointId is required" });
                }

                _logger.LogInformation("ClearSoC => ChargePointId={0}, ConnectorId={1}",
                    request.ChargePointId, request.ConnectorId);

                SoCCache.Clear(request.ChargePointId, request.ConnectorId);

                return new OkObjectResult(new { Success = true, Message = "SoC cache cleared" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClearSoC => Exception: {0}", ex.Message);
                return new OkObjectResult(new { Success = false, Message = ex.Message });
            }
        }

        public class ClearSoCRequest
        {
            public string ChargePointId { get; set; }
            public int ConnectorId { get; set; }
        }
    }
}