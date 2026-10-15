namespace OCPP.Core.Database
{
    /// <summary>
    /// Why a charging session ended. Stored as the code on ChargingSession / OcpiPartnerSession /
    /// OcpiHostedSession.StopReason by the code path that ends the session — the single source of
    /// truth, shared by Management, Roaming and every UI (via <see cref="Describe"/>).
    ///
    /// A null StopReason on an ended session means none of our stop paths ended it: the charger
    /// (or partner CPO) did, or the session predates these columns.
    /// </summary>
    public static class SessionStopReasons
    {
        /// <summary>The user ended the session from the app</summary>
        public const string AppStop = "AppStop";

        /// <summary>Auto-stopped because a session limit was hit. Detail: the violated limits, e.g. "Energy, Cost"</summary>
        public const string LimitReached = "LimitReached";

        /// <summary>The charger (EV unplugged, vehicle full, stopped at the charger) ended it before we requested a stop</summary>
        public const string ChargerEnded = "ChargerEnded";

        /// <summary>Auto-closed because no energy was delivered</summary>
        public const string ZeroEnergy = "ZeroEnergy";

        /// <summary>A roaming partner (eMSP) sent STOP_SESSION for a session on our charger</summary>
        public const string PartnerStop = "PartnerStop";

        /// <summary>The charger went offline mid-session and the session was closed</summary>
        public const string ChargerDisconnected = "ChargerDisconnected";

        /// <summary>Closed by the backend (zombie/stuck/invalid session, transaction force-closed). Detail says which.</summary>
        public const string SystemClosed = "SystemClosed";

        public sealed record Description(string Label, string Category, string Text);

        /// <summary>
        /// Display info for a code. Category: app | limit | charger | partner | system.
        /// </summary>
        public static Description Describe(string code, string detail = null)
        {
            switch (code)
            {
                case AppStop:
                    return new Description("Stopped via app", "app", "The user ended the session from the app.");
                case LimitReached:
                    return new Description(string.IsNullOrEmpty(detail) ? "Limit reached" : $"Limit reached: {detail}", "limit",
                        "Stopped automatically because a session limit was reached.");
                case ChargerEnded:
                    return new Description("Ended at charger", "charger",
                        "The charger ended the session (EV unplugged, vehicle full or stopped at the charger).");
                case ZeroEnergy:
                    return new Description("No energy delivered", "system", "Closed automatically because no energy was delivered.");
                case PartnerStop:
                    return new Description("Stopped by roaming partner", "partner", "The roaming partner's customer ended the session.");
                case ChargerDisconnected:
                    return new Description("Charger disconnected", "system", "The charger went offline during the session.");
                case SystemClosed:
                    return new Description("Closed by system", "system", string.IsNullOrEmpty(detail) ? "Closed automatically by the backend." : detail);
                default:
                    return null;
            }
        }
    }
}
