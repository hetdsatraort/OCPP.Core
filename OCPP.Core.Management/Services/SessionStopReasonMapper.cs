using System;
using System.Text.RegularExpressions;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Services
{
    /// <summary>Stop reason as returned to the UIs</summary>
    public class SessionStopReasonDto
    {
        /// <summary>SessionStopReasons code</summary>
        public string Code { get; set; }
        public string Label { get; set; }
        /// <summary>app | limit | charger | partner | system</summary>
        public string Category { get; set; }
        public string Detail { get; set; }
        public string Description { get; set; }
        /// <summary>
        /// True when no reason was recorded on the session (sessions from before StopReason existed)
        /// and it was inferred from wallet-log text / OCPI flags instead.
        /// </summary>
        public bool IsDerived { get; set; }
    }

    /// <summary>
    /// Builds the stop reason for a session: the StopReason recorded by the code path that ended it,
    /// with a legacy fallback for sessions closed before that column existed. This is the ONLY place
    /// that still interprets wallet-log text.
    /// </summary>
    public static class SessionStopReasonMapper
    {
        private static readonly Regex LimitViolationsRegex = new Regex(@"Limit Violations:\s*(.+)", RegexOptions.Compiled);

        public static SessionStopReasonDto Build(string code, string detail, bool isDerived = false)
        {
            if (string.IsNullOrEmpty(code)) return null;
            var d = SessionStopReasons.Describe(code, detail);
            if (d == null) return null;
            return new SessionStopReasonDto
            {
                Code = code,
                Label = d.Label,
                Category = d.Category,
                Detail = detail,
                Description = d.Text,
                IsDerived = isDerived
            };
        }

        /// <summary>
        /// OCPP app session. Uses the recorded reason; for legacy rows falls back to the session's
        /// charging debit text. Returns null while the session is active or when nothing can be inferred.
        /// </summary>
        public static SessionStopReasonDto ForChargingSession(Database.EVCDTO.ChargingSession session,
            string walletInfo1 = null, string walletInfo3 = null)
        {
            if (session == null || session.EndTime == DateTime.MinValue) return null;

            var recorded = Build(session.StopReason, session.StopReasonDetail);
            if (recorded != null) return recorded;

            // ── Legacy fallback: wallet-log text written by the old stop paths ──
            if (string.IsNullOrEmpty(walletInfo1)) return null;

            if (walletInfo1.StartsWith("Auto-stopped", StringComparison.OrdinalIgnoreCase))
            {
                var m = string.IsNullOrEmpty(walletInfo3) ? Match.Empty : LimitViolationsRegex.Match(walletInfo3);
                return Build(SessionStopReasons.LimitReached, m.Success ? m.Groups[1].Value.Trim() : null, isDerived: true);
            }
            if (walletInfo1.StartsWith("Session auto-closed - zero energy", StringComparison.OrdinalIgnoreCase))
                return Build(SessionStopReasons.ZeroEnergy, null, isDerived: true);
            if (walletInfo1.StartsWith("Session auto-closed", StringComparison.OrdinalIgnoreCase))
                return Build(SessionStopReasons.ChargerEnded, null, isDerived: true);
            if (walletInfo1.StartsWith("Charging at", StringComparison.OrdinalIgnoreCase))
                // Legacy app end-session — can't tell whether the charger had already stopped
                return Build(SessionStopReasons.AppStop, null, isDerived: true);

            return null;
        }

        /// <summary>
        /// OCPI partner session (our user at a partner CPO). Uses the recorded reason; for legacy rows
        /// falls back to the LimitViolationHandled / LastStopAttemptAt flags.
        /// </summary>
        public static SessionStopReasonDto ForPartnerSession(string stopReason, string stopReasonDetail,
            bool isEnded, bool limitViolationHandled, DateTime? lastStopAttemptAt)
        {
            if (!isEnded && !limitViolationHandled && !lastStopAttemptAt.HasValue) return null;

            var recorded = Build(stopReason, stopReasonDetail);
            if (recorded != null) return recorded;

            if (limitViolationHandled) return Build(SessionStopReasons.LimitReached, null, isDerived: true);
            if (lastStopAttemptAt.HasValue) return Build(SessionStopReasons.AppStop, null, isDerived: true);
            return isEnded ? Build(SessionStopReasons.ChargerEnded, null, isDerived: true) : null;
        }
    }
}
