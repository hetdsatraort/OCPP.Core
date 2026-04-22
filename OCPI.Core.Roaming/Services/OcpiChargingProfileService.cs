using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts.ChargingProfiles;
using OCPI.Enums.ChargingProfiles;
using OCPI.Enums.SmartCharging;
using OCPP.Core.Database;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiChargingProfileService : IOcpiChargingProfileService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiChargingProfileService> _logger;
        private readonly IConfiguration _config;

        public OcpiChargingProfileService(
            OCPPCoreContext dbContext,
            ILogger<OcpiChargingProfileService> logger,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
        }

        public async Task<ChargingProfileResponseType> SetChargingProfileAsync(
            string sessionId, OcpiSetChargingProfileRequest request)
        {
            _logger.LogInformation("Setting charging profile for session {SessionId}", sessionId);

            var session = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                return ChargingProfileResponseType.UnknownSession;

            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == session.EvseUid && s.Active == 1);

            if (station == null)
                return ChargingProfileResponseType.Rejected;

            if (!int.TryParse(session.ConnectorId, out var connectorNumber))
                return ChargingProfileResponseType.Rejected;

            var firstPeriod = request.ChargingProfile?.ChargingProfilePeriods?.FirstOrDefault();
            if (firstPeriod == null)
                return ChargingProfileResponseType.Rejected;

            var powerLimit = (int)(firstPeriod.Limit ?? 0);

            _ = Task.Run(async () =>
            {
                try
                {
                    await CallOcppApiAsync(
                        $"API/SetChargingLimit/{Uri.EscapeDataString(station.ChargingPointId)}/{connectorNumber}/{powerLimit}",
                        el => el.GetProperty("status").GetString() == "Accepted"
                            ? (true, "Charging profile set")
                            : (false, $"Failed: {el.GetProperty("status").GetString()}"));

                    await PostResultAsync(request.ResponseUrl,
                        new OcpiChargingProfileResult { Result = ChargingProfileResultType.Accepted });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SetChargingProfile background processing for session {SessionId}", sessionId);
                }
            });

            return ChargingProfileResponseType.Accepted;
        }

        public async Task<OcpiChargingProfile?> GetActiveChargingProfileAsync(string sessionId)
        {
            _logger.LogInformation("Getting active charging profile for session {SessionId}", sessionId);

            var session = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                return null;

            // Return a default unlimited profile — actual limit query would require OCPP GetCompositeSchedule
            return new OcpiChargingProfile
            {
                ChargingRateUnit      = ChargingRateUnit.Watts,
                ChargingProfilePeriods = new[]
                {
                    new OcpiChargingProfilePeriod { StartPeriod = 0, Limit = null }
                }
            };
        }

        public async Task<ChargingProfileResponseType> ClearChargingProfileAsync(
            string sessionId, string? responseUrl)
        {
            _logger.LogInformation("Clearing charging profile for session {SessionId}", sessionId);

            var session = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                return ChargingProfileResponseType.UnknownSession;

            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == session.EvseUid && s.Active == 1);

            if (station == null)
                return ChargingProfileResponseType.Rejected;

            if (!int.TryParse(session.ConnectorId, out var connectorNumber))
                return ChargingProfileResponseType.Rejected;

            _ = Task.Run(async () =>
            {
                try
                {
                    await CallOcppApiAsync(
                        $"API/ClearChargingLimit/{Uri.EscapeDataString(station.ChargingPointId)}/{connectorNumber}",
                        el => el.GetProperty("status").GetString() == "Accepted"
                            ? (true, "Charging profile cleared")
                            : (false, $"Failed: {el.GetProperty("status").GetString()}"));

                    await PostResultAsync(responseUrl,
                        new OcpiChargingProfileResult { Result = ChargingProfileResultType.Accepted });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ClearChargingProfile background processing for session {SessionId}", sessionId);
                }
            });

            return ChargingProfileResponseType.Accepted;
        }

        // ─────────────────────────── HELPERS ─────────────────────────────────────

        private async Task<(bool Success, string Message)> CallOcppApiAsync(
            string relativeUrl,
            Func<JsonElement, (bool, string)> parseResponse)
        {
            var serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            var apiKey       = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
                return (false, "OCPP server URL not configured");

            try
            {
                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                if (!string.IsNullOrWhiteSpace(apiKey))
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

                var uri      = new Uri(new Uri(serverApiUrl), relativeUrl);
                var response = await httpClient.GetAsync(uri);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json))
                    {
                        using var doc = JsonDocument.Parse(json);
                        return parseResponse(doc.RootElement);
                    }
                    return (false, "Empty response from OCPP server");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return (false, "Charge point is offline or not found");

                _logger.LogError("OCPP API {Url} returned {StatusCode}", relativeUrl, response.StatusCode);
                return (false, $"OCPP server error: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OCPP API {Url}", relativeUrl);
                return (false, $"Communication error: {ex.Message}");
            }
        }

        private async Task PostResultAsync<T>(string? responseUrl, T payload)
        {
            if (string.IsNullOrEmpty(responseUrl))
                return;

            var platformToken = _config.GetValue<string>("OCPI:Token");
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                if (!string.IsNullOrWhiteSpace(platformToken))
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {platformToken}");

                await httpClient.PostAsJsonAsync(responseUrl, payload);
                _logger.LogInformation("Posted ChargingProfile result to {Url}", responseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to POST ChargingProfile result to {Url}", responseUrl);
            }
        }
    }
}
