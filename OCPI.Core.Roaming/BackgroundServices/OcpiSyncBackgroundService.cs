using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Background service to periodically sync OCPI data with partners
    /// </summary>
    public class OcpiSyncBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OcpiSyncBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _syncInterval;

        public OcpiSyncBackgroundService(
            IServiceProvider services,
            ILogger<OcpiSyncBackgroundService> logger,
            IConfiguration configuration)
        {
            _services = services;
            _logger = logger;
            _configuration = configuration;
            
            // Default sync interval: 5 minutes (can be configured)
            var intervalMinutes = configuration.GetValue<int>("OCPI:SyncIntervalMinutes", 5);
            _syncInterval = TimeSpan.FromMinutes(intervalMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCPI Sync Background Service started. Sync interval: {Interval}", _syncInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_syncInterval, stoppingToken);
                    
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    await PerformSyncAsync(stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OCPI sync background service");
                    // Continue running despite errors
                }
            }

            _logger.LogInformation("OCPI Sync Background Service stopped");
        }

        private async Task PerformSyncAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OCPPCoreContext>();

            try
            {
                // Get all active partners
                var partners = await dbContext.OcpiPartnerCredentials
                    .Where(p => p.IsActive)
                    .ToListAsync(cancellationToken);

                if (!partners.Any())
                {
                    _logger.LogDebug("No active OCPI partners to sync");
                    return;
                }

                _logger.LogInformation("Starting OCPI sync for {Count} partners", partners.Count);

                foreach (var partner in partners)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await SyncPartnerAsync(partner, scope.ServiceProvider, cancellationToken);
                        
                        // Update last sync time
                        partner.LastSyncOn = DateTime.UtcNow;
                        dbContext.OcpiPartnerCredentials.Update(partner);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error syncing partner {CountryCode}-{PartyId}", 
                            partner.CountryCode, partner.PartyId);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("OCPI sync completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during OCPI sync");
            }
        }

        private async Task SyncPartnerAsync(
            OCPP.Core.Database.OCPIDTO.OcpiPartnerCredential partner, 
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            // In a full implementation, this would:
            // 1. Call partner's /locations endpoint to get their locations
            // 2. Update local database with partner's charging locations
            // 3. Push our updates to the partner if needed (bidirectional sync)
            // 4. Handle sessions updates
            // 5. Exchange CDRs for billing

            _logger.LogDebug("Syncing with partner {BusinessName} ({CountryCode}-{PartyId})", 
                partner.BusinessName, partner.CountryCode, partner.PartyId);

            // Example: You would use HttpClient to call partner APIs
            // var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
            // httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {partner.Token}");
            // var response = await httpClient.GetAsync($"{partner.Url}/locations", cancellationToken);
            // ... process response and update database

            // For now, just log that sync would happen
            _logger.LogDebug("Sync with {BusinessName} would be performed here", partner.BusinessName);
            
            await Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OCPI Sync Background Service is stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}
