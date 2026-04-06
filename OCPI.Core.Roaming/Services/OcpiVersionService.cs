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
    /// Service implementation for OCPI Version operations
    /// </summary>
    public class OcpiVersionService : IOcpiVersionService
    {
        private readonly ILogger<OcpiVersionService> _logger;
        private readonly IConfiguration _configuration;

        public OcpiVersionService(ILogger<OcpiVersionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<OcpiVersionDto>> GetVersionsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving OCPI versions");

                var baseUrl = _configuration["OCPI:BaseUrl"] ?? "https://localhost:5001/ocpi";

                var versions = new List<OcpiVersionDto>
                {
                    new OcpiVersionDto
                    {
                        Version = "2.2.1",
                        Url = $"{baseUrl}/2.2.1"
                    }
                };

                return await Task.FromResult(versions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OCPI versions");
                throw;
            }
        }

        public async Task<OcpiVersionDetailsDto> GetVersionDetailsAsync(string version)
        {
            try
            {
                _logger.LogInformation($"Retrieving OCPI version details: {version}");

                if (version != "2.2.1")
                {
                    throw new NotSupportedException($"OCPI version {version} is not supported");
                }

                var baseUrl = _configuration["OCPI:BaseUrl"] ?? "https://localhost:5001/ocpi";

                var versionDetails = new OcpiVersionDetailsDto
                {
                    Version = "2.2.1",
                    Endpoints = new List<OcpiEndpointDto>
                    {
                        new OcpiEndpointDto
                        {
                            Identifier = "credentials",
                            Role = "SENDER",
                            Url = $"{baseUrl}/2.2.1/credentials"
                        },
                        new OcpiEndpointDto
                        {
                            Identifier = "locations",
                            Role = "SENDER",
                            Url = $"{baseUrl}/2.2.1/locations"
                        },
                        new OcpiEndpointDto
                        {
                            Identifier = "sessions",
                            Role = "RECEIVER",
                            Url = $"{baseUrl}/2.2.1/sessions"
                        },
                        new OcpiEndpointDto
                        {
                            Identifier = "cdrs",
                            Role = "RECEIVER",
                            Url = $"{baseUrl}/2.2.1/cdrs"
                        },
                        new OcpiEndpointDto
                        {
                            Identifier = "tariffs",
                            Role = "SENDER",
                            Url = $"{baseUrl}/2.2.1/tariffs"
                        },
                        new OcpiEndpointDto
                        {
                            Identifier = "tokens",
                            Role = "RECEIVER",
                            Url = $"{baseUrl}/2.2.1/tokens"
                        }
                    }
                };

                return await Task.FromResult(versionDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving OCPI version details: {version}");
                throw;
            }
        }
    }
}
