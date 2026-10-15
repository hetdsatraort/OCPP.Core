using System;
using System.Text.RegularExpressions;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// Resolved, human-readable stop reason for a transaction.
    /// Category: app | limit | partner | system | charger | fault (drives the UI colour).
    /// Source:   e.g. "OCPP app", "OCPP limit", "OCPI app", "OCPI limit", "OCPI partner", "System", "Charger".
    /// </summary>
    public record ResolvedStopReason(string Code, string Label, string Category, string Source, string Detail, bool IsDerived);

    /// <summary>
    /// Why a transaction stopped. Reads the StopReason recorded on the owning session by the code
    /// path that ended it (ChargingSession / OcpiPartnerSession / OcpiHostedSession); legacy sessions
    /// fall back through <see cref="SessionStopReasonMapper"/>. The charger's own reason is only used
    /// when no session of ours explains the stop.
    ///
    /// Pure function over pre-loaded data — callers batch-load the inputs.
    /// </summary>
    public static class TransactionStopReasonResolver
    {
        public sealed class Inputs
        {
            public Transaction Transaction { get; init; }

            /// <summary>OCPP app session owning the transaction</summary>
            public Database.EVCDTO.ChargingSession AppSession { get; init; }
            /// <summary>Legacy fallback only: the app session's charging debit text</summary>
            public string WalletInfo1 { get; init; }
            public string WalletInfo3 { get; init; }

            /// <summary>OCPI hosted session (roaming session on our charger)</summary>
            public bool HasHostedSession { get; init; }
            public string HostedStopReason { get; init; }
            public string HostedStopReasonDetail { get; init; }
            public string PartnerName { get; init; }

            /// <summary>Our user's eMSP-side session for the hosted session, when the partner network is us</summary>
            public bool HasPartnerSession { get; init; }
            public string PartnerStopReason { get; init; }
            public string PartnerStopReasonDetail { get; init; }
            public bool PartnerLimitViolationHandled { get; init; }
            public DateTime? PartnerLastStopAttemptAt { get; init; }
        }

        public static ResolvedStopReason Resolve(Inputs i)
        {
            var t = i.Transaction;
            if (t == null || !t.StopTime.HasValue) return null;

            // 1. Our backend closed the transaction record itself — most specific for this view
            var system = ResolveTransactionMarker(t.StopReason);
            if (system != null) return system;

            // 2. OCPP app session
            if (i.AppSession != null)
            {
                var r = SessionStopReasonMapper.ForChargingSession(i.AppSession, i.WalletInfo1, i.WalletInfo3);
                if (r != null) return FromSession(r, "OCPP", t);
            }

            // 3. OCPI: our own user's roaming session first (knows app vs limit), then the hosted session
            if (i.HasPartnerSession)
            {
                var r = SessionStopReasonMapper.ForPartnerSession(i.PartnerStopReason, i.PartnerStopReasonDetail,
                    true, i.PartnerLimitViolationHandled, i.PartnerLastStopAttemptAt);
                // ChargerEnded on the partner side carries no more info than the hosted session — keep looking
                if (r != null && r.Code != SessionStopReasons.ChargerEnded) return FromSession(r, "OCPI", t);
            }
            if (i.HasHostedSession)
            {
                var r = SessionStopReasonMapper.Build(i.HostedStopReason, i.HostedStopReasonDetail);
                if (r != null) return FromSession(r, "OCPI", t);
            }

            // 4. Nothing of ours explains it — use the charger's own reason
            return ChargerEnded(t, i.HasHostedSession
                ? $"Roaming session from {i.PartnerName ?? "a partner network"}."
                : null, isDerived: false);
        }

        private static ResolvedStopReason FromSession(SessionStopReasonDto r, string protocol, Transaction t)
        {
            // A plain "ended at charger" is better described by the charger's own reason when it sent one
            if (r.Code == SessionStopReasons.ChargerEnded &&
                !string.IsNullOrEmpty(t.StopReason) && t.StopReason != TransactionStopReasons.NotReported)
            {
                return ChargerEnded(t, r.Detail, r.IsDerived);
            }

            string source = r.Category switch
            {
                "app" => $"{protocol} app",
                "limit" => $"{protocol} limit",
                "partner" => $"{protocol} partner",
                "charger" => "Charger",
                _ => "System"
            };
            return new ResolvedStopReason(r.Code, r.Label, r.Category, source,
                r.Detail != null && r.Detail != r.Description && r.Code != SessionStopReasons.LimitReached
                    ? $"{r.Description} {r.Detail}"
                    : r.Description,
                r.IsDerived);
        }

        private static ResolvedStopReason ResolveTransactionMarker(string stopReason)
        {
            switch (stopReason)
            {
                case TransactionStopReasons.StaleSuperseded:
                    return new ResolvedStopReason(stopReason, "Superseded", "system", "System",
                        "Closed by the backend: a newer transaction started on the same connector.", false);
                case TransactionStopReasons.StaleAutoClosed:
                    return new ResolvedStopReason(stopReason, "Auto-closed (stale)", "system", "System",
                        "Closed by the session monitor after the charger repeatedly failed to stop it.", false);
                case TransactionStopReasons.AdminForceClosed:
                    return new ResolvedStopReason(stopReason, "Force-closed by admin", "system", "System",
                        "Force-closed by an admin from the Transactions monitor.", false);
                case "ChargePointDisconnected":
                    return new ResolvedStopReason(stopReason, "Charger disconnected", "system", "System",
                        "Closed by the OCPI service because the charger went offline mid-session.", false);
                case "Connector unlocked":
                    return new ResolvedStopReason(stopReason, "Connector unlocked", "system", "System",
                        "Closed when the connector was unlocked and stuck sessions were cleaned up.", false);
                default:
                    return null;
            }
        }

        /// <summary>Charger-side end: the charger's own reason when it sent one, otherwise a neutral label</summary>
        private static ResolvedStopReason ChargerEnded(Transaction t, string context, bool isDerived)
        {
            string raw = t.StopReason;
            string suffix = string.IsNullOrEmpty(context) ? string.Empty : " " + context;

            if (string.IsNullOrEmpty(raw) || raw == TransactionStopReasons.NotReported)
            {
                return new ResolvedStopReason(SessionStopReasons.ChargerEnded, "Ended at charger", "charger", "Charger",
                    ("The charger ended it without reporting a reason (typically EV unplugged, vehicle full or stopped at the charger)." + suffix).Trim(), isDerived);
            }

            switch (raw)
            {
                case "EVDisconnected":
                case "EVDeparted":
                    return new ResolvedStopReason(raw, "EV unplugged", "charger", "Charger", ("The cable was unplugged from the vehicle." + suffix).Trim(), isDerived);
                case "Local":
                case "StopAuthorized":
                    return new ResolvedStopReason(raw, "Stopped at charger", "charger", "Charger", ("Stopped at the charger (button or RFID)." + suffix).Trim(), isDerived);
                case "StoppedByEV":
                    return new ResolvedStopReason(raw, "Stopped by EV", "charger", "Charger", ("The vehicle ended the charge (e.g. battery full)." + suffix).Trim(), isDerived);
                case "Remote":
                case "RemoteStop":
                    return new ResolvedStopReason(raw, "Remote stop", "charger", "Charger", ("Charger reports a remote stop with no matching stop recorded on our side." + suffix).Trim(), isDerived);
                case "EmergencyStop":
                    // Rows stored before the OCPP server fix: a missing reason was saved as EmergencyStop
                    return new ResolvedStopReason(raw, "Emergency stop (as recorded)", "fault", "Charger",
                        ("Recorded as EmergencyStop. Transactions stored before the reason fix used this value whenever the charger sent no reason." + suffix).Trim(), isDerived);
                case "PowerLoss":
                case "HardReset":
                case "SoftReset":
                case "Reboot":
                case "GroundFault":
                case "OvercurrentFault":
                case "PowerQuality":
                case "EVCommunicationLost":
                case "AbnormalCondition":
                    return new ResolvedStopReason(raw, SplitWords(raw), "fault", "Charger", ("Reported by the charger." + suffix).Trim(), isDerived);
                default:
                    return new ResolvedStopReason(raw, SplitWords(raw), "charger", "Charger", ("Reported by the charger." + suffix).Trim(), isDerived);
            }
        }

        private static string SplitWords(string value) => Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
