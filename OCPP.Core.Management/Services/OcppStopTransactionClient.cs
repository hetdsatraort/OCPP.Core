using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// Result of a stop request sent through the OCPP server.
    /// Status: Accepted | Rejected | Timeout | Superseded | NoOpenTransaction | Offline | Error
    /// </summary>
    public record OcppStopResult(bool Success, string Message, string Status);

    /// <summary>
    /// Calls the OCPP server's /API/StopTransaction endpoint.
    /// Always pass the transactionId when you know it: without it the OCPP server stops the
    /// LATEST transaction on the connector, which may not be the one you meant.
    /// </summary>
    public static class OcppStopTransactionClient
    {
        public static async Task<OcppStopResult> StopAsync(IConfiguration config, ILogger logger,
            string chargePointId, int connectorId, int? transactionId = null, int timeoutSeconds = 60)
        {
            string serverApiUrl = config.GetValue<string>("ServerApiUrl");
            string apiKeyConfig = config.GetValue<string>("ApiKey");

            if (string.IsNullOrEmpty(serverApiUrl))
            {
                logger.LogWarning("CallOCPPStopTransaction: ServerApiUrl not configured");
                return new OcppStopResult(false, "OCPP server URL not configured", "Error");
            }

            try
            {
                using (var httpClient = new HttpClient())
                {
                    if (!serverApiUrl.EndsWith('/'))
                    {
                        serverApiUrl += "/";
                    }
                    Uri uri = new Uri(serverApiUrl);
                    string path = $"StopTransaction/{Uri.EscapeDataString(chargePointId)}/{connectorId}";
                    if (transactionId.HasValue)
                    {
                        path += $"/{transactionId.Value}";
                    }
                    uri = new Uri(uri, path);
                    httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                    {
                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                    }

                    HttpResponseMessage response = await httpClient.GetAsync(uri);

                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(jsonResult))
                        {
                            dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                            string status = jsonObject.status;

                            logger.LogInformation("OCPP StopTransaction result for {ChargePointId}/{ConnectorId} (Txn {TransactionId}): {Status}",
                                chargePointId, connectorId, transactionId?.ToString() ?? "latest", status);

                            switch (status)
                            {
                                case "Accepted":
                                    return new OcppStopResult(true, "Stop transaction accepted by charge point", status);
                                case "Rejected":
                                    return new OcppStopResult(false, "Stop transaction rejected by charge point", status);
                                case "Timeout":
                                    return new OcppStopResult(false, "Charge point did not respond in time", status);
                                case "Superseded":
                                    return new OcppStopResult(false, "A newer transaction is running on this connector; stale transaction was not sent to the charger", status);
                                default:
                                    return new OcppStopResult(false, $"Unknown status: {status}", "Error");
                            }
                        }
                        return new OcppStopResult(false, "Empty response from OCPP server", "Error");
                    }
                    else if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return new OcppStopResult(false, "Charge point is offline", "Offline");
                    }
                    else if (response.StatusCode == HttpStatusCode.FailedDependency)
                    {
                        return new OcppStopResult(false, "Connector has no open transaction", "NoOpenTransaction");
                    }
                    else
                    {
                        logger.LogError("OCPP API returned status: {StatusCode}", response.StatusCode);
                        return new OcppStopResult(false, "OCPP server returned error", "Error");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calling OCPP StopTransaction API");
                return new OcppStopResult(false, $"Error communicating with OCPP server: {ex.Message}", "Error");
            }
        }
    }
}
