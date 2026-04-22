using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OCPI.Contracts;
using OCPP.Core.Database;

namespace OCPI.Core.Roaming.Services
{
    public class OcpiCommandService : IOcpiCommandService
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<OcpiCommandService> _logger;
        private readonly IConfiguration _config;

        public OcpiCommandService(
            OCPPCoreContext dbContext,
            ILogger<OcpiCommandService> logger,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
        }

        // ───────────────────────────── START SESSION ─────────────────────────────

        public async Task<CommandResponseType> HandleStartSessionAsync(OcpiStartSessionCommand command)
        {
            _logger.LogInformation("Handling START_SESSION for location={LocationId} evse={EvseUid} connector={ConnectorId}",
                command.LocationId, command.EvseUid, command.ConnectorId);

            var (chargePointId, connectorNumber, error) =
                await ResolveOcppIdsAsync(command.LocationId, command.EvseUid, command.ConnectorId);

            if (error != null)
            {
                _logger.LogWarning("START_SESSION resolution failed: {Error}", error);
                return CommandResponseType.Rejected;
            }

            var tokenUid = command.Token?.Uid ?? "REMOTE";

            // Fire-and-forget: call OCPP and post result to response_url
            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, msg) = await CallOcppStartTransactionAsync(chargePointId!, connectorNumber, tokenUid);
                    var resultType = success ? CommandResultType.Accepted : CommandResultType.Rejected;
                    await PostCommandResultAsync(command.ResponseUrl, resultType, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in START_SESSION background processing");
                }
            });

            return CommandResponseType.Accepted;
        }

        // ───────────────────────────── STOP SESSION ──────────────────────────────

        public async Task<CommandResponseType> HandleStopSessionAsync(OcpiStopSessionCommand command)
        {
            _logger.LogInformation("Handling STOP_SESSION for session={SessionId}", command.SessionId);

            // Find the active charging session
            var ocpiSession = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == command.SessionId);

            if (ocpiSession == null)
            {
                _logger.LogWarning("STOP_SESSION: session {SessionId} not found", command.SessionId);
                return CommandResponseType.Unknown_session;
            }

            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == ocpiSession.EvseUid && s.Active == 1);

            if (station == null)
            {
                _logger.LogWarning("STOP_SESSION: station {EvseUid} not found", ocpiSession.EvseUid);
                return CommandResponseType.Rejected;
            }

            var gun = await _dbContext.ChargingGuns
                .FirstOrDefaultAsync(g => g.ChargingStationId == station.RecId
                    && g.ConnectorId == ocpiSession.ConnectorId && g.Active == 1);

            if (gun == null)
            {
                _logger.LogWarning("STOP_SESSION: connector {ConnectorId} not found", ocpiSession.ConnectorId);
                return CommandResponseType.Rejected;
            }

            if (!int.TryParse(gun.ConnectorId, out var connectorNumber))
            {
                _logger.LogWarning("STOP_SESSION: invalid connector number {ConnectorId}", gun.ConnectorId);
                return CommandResponseType.Rejected;
            }

            var chargePointId = station.ChargingPointId;

            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, msg) = await CallOcppStopTransactionAsync(chargePointId, connectorNumber);
                    var resultType = success ? CommandResultType.Accepted : CommandResultType.Rejected;
                    await PostCommandResultAsync(command.ResponseUrl, resultType, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in STOP_SESSION background processing");
                }
            });

            return CommandResponseType.Accepted;
        }

        // ───────────────────────────── RESERVE NOW ───────────────────────────────

        public Task<CommandResponseType> HandleReserveNowAsync(OcpiReserveNowCommand command)
        {
            _logger.LogInformation("RESERVE_NOW not supported, responding NOT_SUPPORTED");
            // ReserveNow requires OCPP 1.6 remote reservation support via /API endpoint — not exposed
            return Task.FromResult(CommandResponseType.Not_supported);
        }

        // ─────────────────────────── CANCEL RESERVATION ──────────────────────────

        public Task<CommandResponseType> HandleCancelReservationAsync(OcpiCancelReservationCommand command)
        {
            _logger.LogInformation("CANCEL_RESERVATION not supported, responding NOT_SUPPORTED");
            return Task.FromResult(CommandResponseType.Not_supported);
        }

        // ─────────────────────────── UNLOCK CONNECTOR ────────────────────────────

        public async Task<CommandResponseType> HandleUnlockConnectorAsync(OcpiUnlockConnectorCommand command)
        {
            _logger.LogInformation("Handling UNLOCK_CONNECTOR for location={LocationId} evse={EvseUid} connector={ConnectorId}",
                command.LocationId, command.EvseUid, command.ConnectorId);

            var (chargePointId, connectorNumber, error) =
                await ResolveOcppIdsAsync(command.LocationId, command.EvseUid, command.ConnectorId);

            if (error != null)
            {
                _logger.LogWarning("UNLOCK_CONNECTOR resolution failed: {Error}", error);
                return CommandResponseType.Rejected;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var (success, msg) = await CallOcppUnlockConnectorAsync(chargePointId!, connectorNumber);
                    var resultType = success ? CommandResultType.Accepted : CommandResultType.Rejected;
                    await PostCommandResultAsync(command.ResponseUrl, resultType, msg);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in UNLOCK_CONNECTOR background processing");
                }
            });

            return CommandResponseType.Accepted;
        }

        // ─────────────────────────── PRIVATE HELPERS ─────────────────────────────

        private async Task<(string? ChargePointId, int ConnectorNumber, string? Error)> ResolveOcppIdsAsync(
            string? locationId, string? evseUid, string? connectorId)
        {
            if (string.IsNullOrEmpty(locationId) || string.IsNullOrEmpty(evseUid))
                return (null, 0, "LocationId and EvseUid are required");

            // evseUid = ChargingStation.RecId
            var station = await _dbContext.ChargingStations
                .FirstOrDefaultAsync(s => s.RecId == evseUid
                    && s.ChargingHubId == locationId
                    && s.Active == 1);

            if (station == null)
                return (null, 0, $"EVSE {evseUid} not found at location {locationId}");

            int connectorNumber = 1;
            if (!string.IsNullOrEmpty(connectorId))
            {
                // connectorId = ChargingGuns.RecId
                var gun = await _dbContext.ChargingGuns
                    .FirstOrDefaultAsync(g => g.RecId == connectorId
                        && g.ChargingStationId == station.RecId
                        && g.Active == 1);

                if (gun == null)
                    return (null, 0, $"Connector {connectorId} not found on EVSE {evseUid}");

                if (!int.TryParse(gun.ConnectorId, out connectorNumber))
                    return (null, 0, $"Invalid OCPP connector number for gun {connectorId}");
            }

            return (station.ChargingPointId, connectorNumber, null);
        }

        private async Task<(bool Success, string Message)> CallOcppStartTransactionAsync(
            string chargePointId, int connectorId, string tokenId)
        {
            return await CallOcppApiAsync(
                $"API/StartTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}/{Uri.EscapeDataString(tokenId)}",
                response =>
                {
                    var status = response.GetProperty("status").GetString();
                    return status switch
                    {
                        "Accepted" => (true, "Transaction accepted by charge point"),
                        "Rejected" => (false, "Transaction rejected by charge point"),
                        "Timeout" => (false, "Charge point did not respond in time"),
                        _ => (false, $"Unknown status: {status}")
                    };
                });
        }

        private async Task<(bool Success, string Message)> CallOcppStopTransactionAsync(
            string chargePointId, int connectorId)
        {
            return await CallOcppApiAsync(
                $"API/StopTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}",
                response =>
                {
                    var status = response.GetProperty("status").GetString();
                    return status switch
                    {
                        "Accepted" => (true, "Transaction stopped"),
                        "Rejected" => (false, "Stop rejected by charge point"),
                        "Timeout" => (false, "Charge point did not respond in time"),
                        _ => (false, $"Unknown status: {status}")
                    };
                });
        }

        private async Task<(bool Success, string Message)> CallOcppUnlockConnectorAsync(
            string chargePointId, int connectorId)
        {
            return await CallOcppApiAsync(
                $"API/UnlockConnector/{Uri.EscapeDataString(chargePointId)}/{connectorId}",
                response =>
                {
                    var status = response.GetProperty("status").GetString();
                    return status switch
                    {
                        "Unlocked" => (true, "Connector unlocked"),
                        "UnlockFailed" => (false, "Unlock failed"),
                        "OngoingAuthorizedTransaction" => (false, "Ongoing authorized transaction"),
                        "NotSupported" => (false, "Not supported by charge point"),
                        "Timeout" => (false, "Charge point did not respond in time"),
                        _ => (false, $"Unknown status: {status}")
                    };
                });
        }

        private async Task<(bool Success, string Message)> CallOcppApiAsync(
            string relativeUrl,
            Func<JsonElement, (bool, string)> parseResponse)
        {
            var serverApiUrl = _config.GetValue<string>("ServerApiUrl");
            var apiKey = _config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
                return (false, "OCPP server URL not configured");

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

                if (!serverApiUrl.EndsWith('/'))
                    serverApiUrl += "/";

                var uri = new Uri(new Uri(serverApiUrl), relativeUrl);

                if (!string.IsNullOrWhiteSpace(apiKey))
                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

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

        private async Task PostCommandResultAsync(string? responseUrl, CommandResultType result, string? message)
        {
            if (string.IsNullOrEmpty(responseUrl))
                return;

            var platformToken = _config.GetValue<string>("OCPI:Token");
            var payload = new OcpiCommandResult
            {
                Result = result,
                Message = string.IsNullOrEmpty(message) ? null : new[]
                {
                    new OcpiDisplayText { Language = "en", Text = message }
                }
            };

            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                if (!string.IsNullOrWhiteSpace(platformToken))
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {platformToken}");

                await httpClient.PostAsJsonAsync(responseUrl, payload);
                _logger.LogInformation("Posted CommandResult {Result} to {Url}", result, responseUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to POST CommandResult to {Url}", responseUrl);
            }
        }

    }
}
