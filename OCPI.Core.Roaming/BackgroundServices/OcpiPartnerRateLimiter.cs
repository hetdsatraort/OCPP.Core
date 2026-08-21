using System.Collections.Concurrent;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Process-wide, per-partner 429 cooldown tracker shared by every background service that
    /// pushes to OCPI partners (<see cref="OcpiSyncBackgroundService"/>'s periodic bulk push and
    /// <see cref="OcpiOrphanSessionService"/>'s ~10s real-time push). A partner that rate-limits
    /// one of these loops should pause both — hammering it from the other loop a few seconds
    /// later just extends the ban.
    /// </summary>
    internal static class OcpiPartnerRateLimiter
    {
        private static readonly ConcurrentDictionary<int, DateTime> _cooldownUntil = new();

        /// <summary>True if this partner is still within a cooldown window from a prior 429.</summary>
        public static bool IsCoolingDown(int partnerId, out TimeSpan remaining)
        {
            if (_cooldownUntil.TryGetValue(partnerId, out var until) && until > DateTime.UtcNow)
            {
                remaining = until - DateTime.UtcNow;
                return true;
            }

            remaining = TimeSpan.Zero;
            return false;
        }

        /// <summary>
        /// Inspects a push response for 429 Too Many Requests. If found, starts (or extends) the
        /// partner's cooldown — honouring their Retry-After header when present, else a
        /// conservative default — and returns true so the caller can stop sending further items.
        /// </summary>
        public static bool HandleIfRateLimited(HttpResponseMessage resp, int partnerId)
        {
            if (resp.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                return false;

            var cooldown = ParseRetryAfter(resp) ?? TimeSpan.FromMinutes(5);
            _cooldownUntil[partnerId] = DateTime.UtcNow + cooldown;
            return true;
        }

        private static TimeSpan? ParseRetryAfter(HttpResponseMessage resp)
        {
            var retryAfter = resp.Headers.RetryAfter;
            if (retryAfter == null) return null;

            if (retryAfter.Delta.HasValue)
                return retryAfter.Delta.Value;

            if (retryAfter.Date.HasValue)
            {
                var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
            }

            return null;
        }
    }
}
