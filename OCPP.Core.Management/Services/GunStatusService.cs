using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Services
{
    public class GunStatusService : BackgroundService
    {
        private readonly ILogger<GunStatusService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _checkInterval;

        public GunStatusService(
            ILogger<GunStatusService> logger,
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;

            var intervalMinutes = configuration.GetValue<int>("GunStatus:CheckIntervalMinutes", 1);
            _checkInterval = TimeSpan.FromMinutes(intervalMinutes);

            _logger.LogInformation(
                "GunStatusService initialized — will sync every {Interval} minute(s)", intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Let the application finish starting before the first sync
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            using var timer = new PeriodicTimer(_checkInterval);

            try
            {
                do
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var syncService = scope.ServiceProvider.GetRequiredService<IGunStatusSyncService>();
                        await syncService.SyncAllGunsAsync(stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "GunStatusService: unhandled error during sync cycle");
                    }
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown — no action needed
            }
        }
    }
}

