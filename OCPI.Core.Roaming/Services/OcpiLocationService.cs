using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPI.Core.Roaming.Models.OCPI;
using OCPI.Core.Roaming.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// Service implementation for OCPI Location operations
    /// </summary>
    public class OcpiLocationService : IOcpiLocationService
    {
        private readonly ILogger<OcpiLocationService> _logger;
        private readonly IConfiguration _configuration;
        private static readonly List<OcpiLocationDto> _locations = new();

        public OcpiLocationService(ILogger<OcpiLocationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<List<OcpiLocationDto>> GetLocationsAsync(string countryCode = null, string partyId = null)
        {
            try
            {
                _logger.LogInformation($"Retrieving OCPI locations. CountryCode: {countryCode}, PartyId: {partyId}");
                
                var query = _locations.AsQueryable();
                
                if (!string.IsNullOrEmpty(countryCode))
                {
                    query = query.Where(l => l.Country.Equals(countryCode, StringComparison.OrdinalIgnoreCase));
                }

                return await Task.FromResult(query.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OCPI locations");
                throw;
            }
        }

        public async Task<OcpiLocationDto> GetLocationByIdAsync(string locationId)
        {
            try
            {
                _logger.LogInformation($"Retrieving OCPI location: {locationId}");
                var location = _locations.FirstOrDefault(l => l.Id == locationId);
                
                if (location == null)
                {
                    _logger.LogWarning($"Location not found: {locationId}");
                }

                return await Task.FromResult(location);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving OCPI location: {locationId}");
                throw;
            }
        }

        public async Task<OcpiLocationDto> CreateOrUpdateLocationAsync(OcpiLocationDto location)
        {
            try
            {
                _logger.LogInformation($"Creating/Updating OCPI location: {location.Id}");
                
                var existingLocation = _locations.FirstOrDefault(l => l.Id == location.Id);
                
                if (existingLocation != null)
                {
                    _locations.Remove(existingLocation);
                    _logger.LogInformation($"Updated existing location: {location.Id}");
                }
                else
                {
                    _logger.LogInformation($"Created new location: {location.Id}");
                }

                location.LastUpdated = DateTime.UtcNow;
                _locations.Add(location);

                return await Task.FromResult(location);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating/updating OCPI location: {location.Id}");
                throw;
            }
        }

        public async Task<bool> DeleteLocationAsync(string locationId)
        {
            try
            {
                _logger.LogInformation($"Deleting OCPI location: {locationId}");
                var location = _locations.FirstOrDefault(l => l.Id == locationId);
                
                if (location != null)
                {
                    _locations.Remove(location);
                    _logger.LogInformation($"Successfully deleted location: {locationId}");
                    return await Task.FromResult(true);
                }

                _logger.LogWarning($"Location not found for deletion: {locationId}");
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting OCPI location: {locationId}");
                throw;
            }
        }

        public async Task<OcpiEvseDto> GetEvseAsync(string locationId, string evseUid)
        {
            try
            {
                _logger.LogInformation($"Retrieving EVSE: {evseUid} from location: {locationId}");
                var location = await GetLocationByIdAsync(locationId);
                
                if (location == null)
                {
                    return null;
                }

                var evse = location.Evses?.FirstOrDefault(e => e.Uid == evseUid);
                
                if (evse == null)
                {
                    _logger.LogWarning($"EVSE not found: {evseUid} in location: {locationId}");
                }

                return evse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving EVSE: {evseUid} from location: {locationId}");
                throw;
            }
        }

        public async Task<OcpiConnectorDto> GetConnectorAsync(string locationId, string evseUid, string connectorId)
        {
            try
            {
                _logger.LogInformation($"Retrieving connector: {connectorId} from EVSE: {evseUid} in location: {locationId}");
                var evse = await GetEvseAsync(locationId, evseUid);
                
                if (evse == null)
                {
                    return null;
                }

                var connector = evse.Connectors?.FirstOrDefault(c => c.Id == connectorId);
                
                if (connector == null)
                {
                    _logger.LogWarning($"Connector not found: {connectorId} in EVSE: {evseUid}");
                }

                return connector;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving connector: {connectorId}");
                throw;
            }
        }
    }
}
