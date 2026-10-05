using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPI.Core.Roaming.Services
{
    /// <summary>
    /// Shared helpers for turning our stored OCPP connector statuses into OCPI EVSE statuses.
    /// </summary>
    public static class OcppConnectorStatusHelper
    {
        /// <summary>
        /// Returns "{ChargePointId}|{ConnectorId}" keys for every connector of the given charge
        /// points that currently has an open (not yet stopped) OCPP transaction.
        /// </summary>
        public static async Task<HashSet<string>> GetOpenTransactionConnectorsAsync(
            OCPPCoreContext db,
            IEnumerable<string?> chargePointIds,
            CancellationToken ct = default)
        {
            var ids = chargePointIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            if (ids.Count == 0)
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var open = await db.Transactions
                .Where(t => ids.Contains(t.ChargePointId) && t.StopTime == null)
                .Select(t => new { t.ChargePointId, t.ConnectorId })
                .Distinct()
                .ToListAsync(ct);

            return open
                .Select(t => Key(t.ChargePointId, t.ConnectorId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The OCPP server stores OCPP 1.6 Preparing (cable plugged in, awaiting authorization)
        /// as "Occupied" — the same value it uses while energy is flowing. Published as-is that
        /// maps to OCPI CHARGING, and partner eMSPs then refuse to send START_SESSION for a
        /// driver who has already plugged in. An Occupied connector without an open transaction
        /// is therefore reported as "Available" (startable); the charger's own response to the
        /// RemoteStartTransaction stays authoritative for anything it can't actually accept
        /// (e.g. Finishing, where the cable from the previous session is still connected).
        /// </summary>
        public static string? NormalizeAwaitingAuthorization(
            string? ocppStatus, string? chargePointId, int connectorId, HashSet<string> openTxConnectors)
        {
            if (string.Equals(ocppStatus, "Occupied", StringComparison.OrdinalIgnoreCase) &&
                !openTxConnectors.Contains(Key(chargePointId, connectorId)))
                return "Available";

            return ocppStatus;
        }

        private static string Key(string? chargePointId, int connectorId) => $"{chargePointId}|{connectorId}";
    }
}
