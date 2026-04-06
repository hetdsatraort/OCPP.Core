using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPI.Core.Roaming.Models.OCPI;
using OCPI.Core.Roaming.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// Service implementation for OCPI Credentials operations
    /// </summary>
    public class OcpiCredentialsService : IOcpiCredentialsService
    {
        private readonly ILogger<OcpiCredentialsService> _logger;
        private readonly IConfiguration _configuration;
        private OcpiCredentialsResponseDto _credentials;

        public OcpiCredentialsService(ILogger<OcpiCredentialsService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<OcpiCredentialsResponseDto> GetCredentialsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving OCPI credentials");

                if (_credentials == null)
                {
                    // Generate default credentials from configuration
                    _credentials = new OcpiCredentialsResponseDto
                    {
                        Token = _configuration["OCPI:Token"] ?? Guid.NewGuid().ToString(),
                        Url = _configuration["OCPI:BaseUrl"] ?? "https://localhost:5001/ocpi/versions",
                        CountryCode = _configuration["OCPI:CountryCode"] ?? "US",
                        PartyId = _configuration["OCPI:PartyId"] ?? "CPO",
                        BusinessDetails = new BusinessDetails
                        {
                            Name = _configuration["OCPI:BusinessName"] ?? "EV Charging Platform",
                            Website = _configuration["OCPI:Website"] ?? "https://evcharging.com"
                        }
                    };
                }

                return await Task.FromResult(_credentials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OCPI credentials");
                throw;
            }
        }

        public async Task<OcpiCredentialsResponseDto> RegisterCredentialsAsync(OcpiCredentialsRequestDto request)
        {
            try
            {
                _logger.LogInformation("Registering OCPI credentials");

                // Validate request
                if (string.IsNullOrEmpty(request.Token) || string.IsNullOrEmpty(request.Url))
                {
                    throw new ArgumentException("Token and Url are required");
                }

                // Store credentials
                _credentials = new OcpiCredentialsResponseDto
                {
                    Token = Guid.NewGuid().ToString(), // Generate new token for the partner
                    Url = _configuration["OCPI:BaseUrl"] ?? "https://localhost:5001/ocpi/versions",
                    CountryCode = _configuration["OCPI:CountryCode"] ?? "US",
                    PartyId = _configuration["OCPI:PartyId"] ?? "CPO",
                    BusinessDetails = new BusinessDetails
                    {
                        Name = _configuration["OCPI:BusinessName"] ?? "EV Charging Platform",
                        Website = _configuration["OCPI:Website"] ?? "https://evcharging.com"
                    }
                };

                _logger.LogInformation($"Successfully registered OCPI credentials for: {request.BusinessDetails?.Name}");

                return await Task.FromResult(_credentials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering OCPI credentials");
                throw;
            }
        }

        public async Task<OcpiCredentialsResponseDto> UpdateCredentialsAsync(OcpiCredentialsRequestDto request)
        {
            try
            {
                _logger.LogInformation("Updating OCPI credentials");

                if (_credentials == null)
                {
                    throw new InvalidOperationException("No credentials exist to update");
                }

                // Update credentials
                _credentials.Token = Guid.NewGuid().ToString(); // Rotate token

                _logger.LogInformation("Successfully updated OCPI credentials");

                return await Task.FromResult(_credentials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating OCPI credentials");
                throw;
            }
        }

        public async Task<bool> DeleteCredentialsAsync()
        {
            try
            {
                _logger.LogInformation("Deleting OCPI credentials");

                if (_credentials == null)
                {
                    _logger.LogWarning("No credentials exist to delete");
                    return await Task.FromResult(false);
                }

                _credentials = null;
                _logger.LogInformation("Successfully deleted OCPI credentials");

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting OCPI credentials");
                throw;
            }
        }
    }

    public static class OcpiCredentialsExtensions
    {
        public static DateTime LastUpdated { get; set; }
    }
}
