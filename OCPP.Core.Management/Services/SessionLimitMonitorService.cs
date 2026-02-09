using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// Background service that periodically checks active charging sessions for limit violations
    /// and automatically stops sessions that exceed their configured limits.
    /// </summary>
    public class SessionLimitMonitorService : BackgroundService
    {
        private readonly ILogger<SessionLimitMonitorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _checkInterval;

        public SessionLimitMonitorService(
            ILogger<SessionLimitMonitorService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

            // Get check interval from configuration, default to 2 minutes
            var intervalMinutes = _configuration.GetValue<int>("SessionLimits:CheckIntervalMinutes", 2);
            _checkInterval = TimeSpan.FromMinutes(intervalMinutes);

            _logger.LogInformation("SessionLimitMonitorService initialized with check interval: {Interval} minutes", intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SessionLimitMonitorService is starting");

            // Wait a bit before starting to ensure the application is fully initialized
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckSessionLimits(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking session limits");
                }

                // Wait for the configured interval before next check
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }

            _logger.LogInformation("SessionLimitMonitorService is stopping");
        }

        private async Task CheckSessionLimits(CancellationToken cancellationToken)
        {
            try
            {
                var baseUrl = _configuration.GetValue<string>("SessionLimits:ApiBaseUrl") ?? "http://localhost:5000";
                var apiEndpoint = "/api/ChargingSession/check-session-limits";

                var httpClient = _httpClientFactory.CreateClient("SessionLimitMonitor");
                httpClient.BaseAddress = new Uri(baseUrl);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                _logger.LogDebug("Checking session limits at {Url}", $"{baseUrl}{apiEndpoint}");

                var response = await httpClient.GetAsync(apiEndpoint, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Session limit check completed successfully: {Content}", content);
                }
                else
                {
                    _logger.LogWarning("Session limit check returned status code: {StatusCode}", response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error during session limit check");
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Session limit check timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during session limit check");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SessionLimitMonitorService is stopping gracefully");
            await base.StopAsync(cancellationToken);
        }
    }
}
