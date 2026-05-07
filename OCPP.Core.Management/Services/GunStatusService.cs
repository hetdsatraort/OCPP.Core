using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Services
{
    public class GunStatusService : BackgroundService
    {
        private readonly ILogger<GunStatusService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TimeSpan _checkInterval;

        public GunStatusService(
            ILogger<GunStatusService> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

            // Get check interval from configuration, default to 30 secs
            var intervalSeconds = _configuration.GetValue<int>("GunStatus:CheckIntervalSeconds", 30);
            _checkInterval = TimeSpan.FromSeconds(intervalSeconds);

            _logger.LogInformation("GunStatusService initialized with check interval: {Interval} seconds", intervalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // _logger.LogInformation("SessionLimitMonitorService is starting");

            // Wait a bit before starting to ensure the application is fully initialized
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            // Use PeriodicTimer for efficient periodic execution
            using var timer = new PeriodicTimer(_checkInterval);

            try
            {
                // Execute immediately on start, then wait for timer
                do
                {
                    try
                    {
                        //await CheckSessionLimits(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred while checking session limits");
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                // _logger.LogInformation("SessionLimitMonitorService is stopping");
            }
        }
    }
}
