using System;

namespace OCPP.Core.Database
{
    /// <summary>
    /// StopReason values written by the backend (not by a charger) when it closes a
    /// transaction record on its own. A late StopTransaction from the charger is still
    /// allowed to overwrite these with the real stop data.
    /// </summary>
    public static class TransactionStopReasons
    {
        /// <summary>A newer transaction started on the same connector, so this one can no longer be running.</summary>
        public const string StaleSuperseded = "StaleSuperseded";

        /// <summary>The session monitor gave up on a transaction the charger never closed.</summary>
        public const string StaleAutoClosed = "StaleAutoClosed";

        /// <summary>An admin force-closed the transaction from the Transactions monitor.</summary>
        public const string AdminForceClosed = "AdminForceClosed";

        public static bool IsSystemClosed(string stopReason)
        {
            return string.Equals(stopReason, StaleSuperseded, StringComparison.Ordinal)
                || string.Equals(stopReason, StaleAutoClosed, StringComparison.Ordinal)
                || string.Equals(stopReason, AdminForceClosed, StringComparison.Ordinal);
        }
    }
}
