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
    /// Service implementation for OCPI Session operations
    /// </summary>
    public class OcpiSessionService : IOcpiSessionService
    {
        private readonly ILogger<OcpiSessionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IOcpiLocationService _locationService;
        private static readonly List<OcpiSessionDto> _sessions = new();

        public OcpiSessionService(
            ILogger<OcpiSessionService> logger, 
            IConfiguration configuration,
            IOcpiLocationService locationService)
        {
            _logger = logger;
            _configuration = configuration;
            _locationService = locationService;
        }

        public async Task<List<OcpiSessionDto>> GetSessionsAsync(string countryCode = null, string partyId = null)
        {
            try
            {
                _logger.LogInformation($"Retrieving OCPI sessions. CountryCode: {countryCode}, PartyId: {partyId}");
                return await Task.FromResult(_sessions.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OCPI sessions");
                throw;
            }
        }

        public async Task<OcpiSessionDto> GetSessionByIdAsync(string sessionId)
        {
            try
            {
                _logger.LogInformation($"Retrieving OCPI session: {sessionId}");
                var session = _sessions.FirstOrDefault(s => s.Id == sessionId);
                
                if (session == null)
                {
                    _logger.LogWarning($"Session not found: {sessionId}");
                }

                return await Task.FromResult(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving OCPI session: {sessionId}");
                throw;
            }
        }

        public async Task<OcpiSessionDto> CreateOrUpdateSessionAsync(OcpiSessionDto session)
        {
            try
            {
                _logger.LogInformation($"Creating/Updating OCPI session: {session.Id}");
                
                var existingSession = _sessions.FirstOrDefault(s => s.Id == session.Id);
                
                if (existingSession != null)
                {
                    _sessions.Remove(existingSession);
                    _logger.LogInformation($"Updated existing session: {session.Id}");
                }
                else
                {
                    _logger.LogInformation($"Created new session: {session.Id}");
                }

                session.LastUpdated = DateTime.UtcNow;
                _sessions.Add(session);

                return await Task.FromResult(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating/updating OCPI session: {session.Id}");
                throw;
            }
        }

        public async Task<OcpiSessionDto> StartSessionAsync(StartSessionRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Starting OCPI session for location: {request.LocationId}, EVSE: {request.EvseUid}");

                // Validate location exists
                var location = await _locationService.GetLocationByIdAsync(request.LocationId);
                if (location == null)
                {
                    throw new InvalidOperationException($"Location not found: {request.LocationId}");
                }

                // Validate EVSE exists
                var evse = await _locationService.GetEvseAsync(request.LocationId, request.EvseUid);
                if (evse == null)
                {
                    throw new InvalidOperationException($"EVSE not found: {request.EvseUid}");
                }

                // Create new session
                var session = new OcpiSessionDto
                {
                    Id = Guid.NewGuid().ToString(),
                    StartDateTime = DateTime.UtcNow,
                    LocationId = request.LocationId,
                    EvseUid = request.EvseUid,
                    ConnectorId = request.ConnectorId,
                    CdrToken = new OcpiCdrToken
                    {
                        Uid = request.TokenUid,
                        Type = "APP_USER",
                        ContractId = request.TokenUid
                    },
                    AuthMethod = "AUTH_REQUEST",
                    Currency = "USD",
                    KWh = 0,
                    Status = "ACTIVE",
                    LastUpdated = DateTime.UtcNow
                };

                _sessions.Add(session);
                _logger.LogInformation($"Successfully started session: {session.Id}");

                return await Task.FromResult(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting OCPI session");
                throw;
            }
        }

        public async Task<OcpiSessionDto> StopSessionAsync(StopSessionRequestDto request)
        {
            try
            {
                _logger.LogInformation($"Stopping OCPI session: {request.SessionId}");
                
                var session = _sessions.FirstOrDefault(s => s.Id == request.SessionId);
                
                if (session == null)
                {
                    throw new InvalidOperationException($"Session not found: {request.SessionId}");
                }

                if (session.Status != "ACTIVE")
                {
                    throw new InvalidOperationException($"Session is not active: {request.SessionId}");
                }

                session.EndDateTime = DateTime.UtcNow;
                session.Status = "COMPLETED";
                session.LastUpdated = DateTime.UtcNow;

                _logger.LogInformation($"Successfully stopped session: {session.Id}");

                return await Task.FromResult(session);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error stopping OCPI session: {request.SessionId}");
                throw;
            }
        }
    }
}
