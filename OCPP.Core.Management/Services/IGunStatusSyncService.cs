using OCPP.Core.Management.Models.ChargingSession;
using System.Threading;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Services
{
    public interface IGunStatusSyncService
    {
        /// <summary>
        /// Syncs the status of a single charging gun against the OCPP server and saves any changes.
        /// Returns null if the gun was not found.
        /// </summary>
        Task<ChargingGunStatusDto> SyncGunStatusAsync(string chargingGunId);

        /// <summary>
        /// Iterates all active charging guns and syncs each one.
        /// Designed to be called from the background service.
        /// </summary>
        Task SyncAllGunsAsync(CancellationToken ct = default);
    }
}
