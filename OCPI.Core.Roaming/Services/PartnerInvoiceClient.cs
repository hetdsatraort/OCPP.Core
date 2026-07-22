using System.Net.Http.Json;
using System.Text.Json;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// Calls back into OCPP.Core.Management to create/fetch the platform-fee invoice for a
    /// completed OCPI partner session. Management owns the fee/GST computation
    /// (Services/Invoice/PartnerInvoiceService.cs) since that's where the invoice entity,
    /// QuestPDF rendering, and the admin-facing invoice API already live — this project only
    /// calls the internal endpoint it exposes for that, rather than keeping its own duplicate
    /// copy of the tax math in sync.
    /// </summary>
    public class PartnerInvoiceClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PartnerInvoiceClient> _logger;

        public PartnerInvoiceClient(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<PartnerInvoiceClient> logger)
        {
            _httpFactory = httpFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Returns the invoice breakdown for the given OCPI session id, or null if it couldn't be
        /// obtained (Management API not configured, unreachable, or the session isn't billable
        /// yet). Callers should treat null as "retry later", not "no fee applies".
        /// </summary>
        public async Task<PartnerInvoiceResult?> GetOrCreateInvoiceAsync(string ocpiSessionId, CancellationToken ct)
        {
            var managementApiUrl = _configuration.GetValue<string>("ManagementApiUrl");
            if (string.IsNullOrWhiteSpace(managementApiUrl))
            {
                _logger.LogWarning(
                    "PartnerInvoiceClient: ManagementApiUrl not configured — cannot create partner " +
                    "invoice for session {SessionId}", ocpiSessionId);
                return null;
            }

            try
            {
                var http = _httpFactory.CreateClient();
                http.Timeout = TimeSpan.FromSeconds(15);

                var url = $"{managementApiUrl.TrimEnd('/')}/api/Invoice/internal/partner-session/{Uri.EscapeDataString(ocpiSessionId)}";
                var resp = await http.PostAsync(url, content: null, ct);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "PartnerInvoiceClient: Management API returned HTTP {Status} for session {SessionId}: {Body}",
                        (int)resp.StatusCode, ocpiSessionId, body);
                    return null;
                }

                var payload = await resp.Content.ReadFromJsonAsync<PartnerInvoiceResponse>(JsonOptions, ct);
                return payload?.Data;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PartnerInvoiceClient: failed to create partner invoice for session {SessionId}", ocpiSessionId);
                return null;
            }
        }

        private class PartnerInvoiceResponse
        {
            public bool Success { get; set; }
            public PartnerInvoiceResult? Data { get; set; }
        }
    }

    public class PartnerInvoiceResult
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal PartnerCost { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal TotalPayable { get; set; }
    }
}
