using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCPI.Core.Roaming.Models.OCPI;
using OCPI.Core.Roaming.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Credentials Controller - Handles credential registration and updates
    /// </summary>
    [Route("ocpi/2.2.1/credentials")]
    [ApiController]
    public class OcpiCredentialsController : ControllerBase
    {
        private readonly IOcpiCredentialsService _credentialsService;
        private readonly ILogger<OcpiCredentialsController> _logger;

        public OcpiCredentialsController(
            IOcpiCredentialsService credentialsService,
            ILogger<OcpiCredentialsController> logger)
        {
            _credentialsService = credentialsService;
            _logger = logger;
        }

        /// <summary>
        /// Get current credentials
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiCredentialsResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCredentials()
        {
            try
            {
                _logger.LogInformation("OCPI GetCredentials called");

                var credentials = await _credentialsService.GetCredentialsAsync();

                var response = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = credentials,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCredentials");

                var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Register new credentials
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiCredentialsResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiCredentialsResponseDto>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RegisterCredentials([FromBody] OcpiCredentialsRequestDto request)
        {
            try
            {
                _logger.LogInformation("OCPI RegisterCredentials called");

                if (!ModelState.IsValid)
                {
                    var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                    {
                        StatusCode = 2001,
                        StatusMessage = "Invalid or missing parameters",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return BadRequest(errorResponse);
                }

                var credentials = await _credentialsService.RegisterCredentialsAsync(request);

                var response = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = credentials,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid credentials registration request");

                var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 2001,
                    StatusMessage = ex.Message,
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return BadRequest(errorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RegisterCredentials");

                var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Update existing credentials
        /// </summary>
        [HttpPut]
        [ProducesResponseType(typeof(OcpiResponseDto<OcpiCredentialsResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateCredentials([FromBody] OcpiCredentialsRequestDto request)
        {
            try
            {
                _logger.LogInformation("OCPI UpdateCredentials called");

                if (!ModelState.IsValid)
                {
                    var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                    {
                        StatusCode = 2001,
                        StatusMessage = "Invalid or missing parameters",
                        Data = null,
                        Timestamp = DateTime.UtcNow
                    };

                    return BadRequest(errorResponse);
                }

                var credentials = await _credentialsService.UpdateCredentialsAsync(request);

                var response = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 1000,
                    StatusMessage = "Success",
                    Data = credentials,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid credentials update request");

                var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 3001,
                    StatusMessage = ex.Message,
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return NotFound(errorResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateCredentials");

                var errorResponse = new OcpiResponseDto<OcpiCredentialsResponseDto>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }

        /// <summary>
        /// Delete credentials (unregister)
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(typeof(OcpiResponseDto<object>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteCredentials()
        {
            try
            {
                _logger.LogInformation("OCPI DeleteCredentials called");

                var success = await _credentialsService.DeleteCredentialsAsync();

                var response = new OcpiResponseDto<object>
                {
                    StatusCode = 1000,
                    StatusMessage = success ? "Credentials deleted successfully" : "No credentials to delete",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteCredentials");

                var errorResponse = new OcpiResponseDto<object>
                {
                    StatusCode = 2000,
                    StatusMessage = $"Server error: {ex.Message}",
                    Data = null,
                    Timestamp = DateTime.UtcNow
                };

                return StatusCode(StatusCodes.Status500InternalServerError, errorResponse);
            }
        }
    }
}
