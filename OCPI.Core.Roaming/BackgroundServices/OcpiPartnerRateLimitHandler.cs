using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Per-host proactive pacing + circuit breaker shared by every outbound call to an OCPI
    /// partner.
    ///
    /// An earlier version of this retried a 429 in-process with backoff honouring Retry-After.
    /// Against at least one partner (Numocity) that didn't help — the 429 comes back in
    /// single-digit milliseconds even on the very first call of a freshly restarted process, which
    /// is the signature of a gateway-level block (by IP or token) rather than a live request
    /// counter, and retrying into an active block risks resetting/extending it rather than working
    /// around it.
    ///
    /// So this trips a breaker per host instead: on any 429, every subsequent call to that host —
    /// from either background service, any partner sharing the host — fails FAST (no network call,
    /// no blocking wait) until the cooldown passes, and the calling background service's normal
    /// "log + move on, try again next cycle" handling takes it from there.
    /// Registered as a singleton so this state is shared across every transient
    /// <see cref="OcpiPartnerRateLimitHandler"/> instance HttpClientFactory creates.
    /// </summary>
    public sealed class OcpiPartnerThrottle
    {
        private sealed class HostState
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public DateTime LastRequestUtc = DateTime.MinValue;
            public DateTime CooldownUntilUtc = DateTime.MinValue;
        }

        private readonly ConcurrentDictionary<string, HostState> _hosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _minInterval;

        public OcpiPartnerThrottle(IConfiguration configuration)
        {
            var ms = configuration.GetValue<int>("OCPI:PartnerMinRequestIntervalMs", 200);
            _minInterval = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        }

        /// <summary>Trips the breaker for <paramref name="host"/> until <c>now + cooldown</c>.</summary>
        public void TripBreaker(string host, TimeSpan cooldown)
        {
            var until = DateTime.UtcNow + cooldown;
            var state = _hosts.GetOrAdd(host, _ => new HostState());
            lock (state)
            {
                if (until > state.CooldownUntilUtc)
                    state.CooldownUntilUtc = until;
            }
        }

        /// <summary>Remaining cooldown for <paramref name="host"/>, or <see cref="TimeSpan.Zero"/> if the breaker isn't tripped.</summary>
        public TimeSpan GetRemainingCooldown(string host)
        {
            if (!_hosts.TryGetValue(host, out var state)) return TimeSpan.Zero;
            lock (state)
            {
                var remaining = state.CooldownUntilUtc - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        /// <summary>Proactive pacing only — keeps calls to one host at least <c>PartnerMinRequestIntervalMs</c> apart.</summary>
        public async Task WaitTurnAsync(string host, CancellationToken ct)
        {
            if (_minInterval <= TimeSpan.Zero) return;

            var state = _hosts.GetOrAdd(host, _ => new HostState());
            await state.Gate.WaitAsync(ct);
            try
            {
                var wait = state.LastRequestUtc + _minInterval - DateTime.UtcNow;
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, ct);

                state.LastRequestUtc = DateTime.UtcNow;
            }
            finally
            {
                state.Gate.Release();
            }
        }
    }

    /// <summary>
    /// DelegatingHandler installed on the named "OcpiPartner" HttpClient used for every outbound
    /// call to an OCPI partner platform (both directions, both background services). See
    /// <see cref="OcpiPartnerThrottle"/> for why this is a fail-fast circuit breaker rather than a
    /// retry-with-backoff loop.
    /// </summary>
    public sealed class OcpiPartnerRateLimitHandler : DelegatingHandler
    {
        private readonly OcpiPartnerThrottle _throttle;
        private readonly ILogger<OcpiPartnerRateLimitHandler> _logger;
        private readonly TimeSpan _cooldownCeiling;
        private readonly TimeSpan _defaultCooldown;

        public OcpiPartnerRateLimitHandler(
            OcpiPartnerThrottle throttle,
            ILogger<OcpiPartnerRateLimitHandler> logger,
            IConfiguration configuration)
        {
            _throttle = throttle;
            _logger = logger;
            // Ceiling on how long a single partner-supplied Retry-After is allowed to hold the
            // breaker closed. Confirmed against Numocity that OCPI partners can enforce a hard
            // per-CALENDAR-DAY quota (200/day, reset at UTC midnight) rather than a short window —
            // their Retry-After legitimately came back as ~14h. Default covers a full day (25h, to
            // safely span a reset seen from any time of day) rather than truncating a day-scale
            // wait down to minutes and re-probing (and getting 429'd again) every few minutes for
            // the rest of the day. Since calls now fail fast instead of blocking a thread, honouring
            // a long partner-directed cooldown costs nothing.
            _cooldownCeiling = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue<int>("OCPI:PartnerCooldownCeilingSeconds", 90000)));
            // Used when a 429 arrives with no Retry-After header at all.
            _defaultCooldown = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue<int>("OCPI:PartnerDefaultCooldownSeconds", 60)));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var host = request.RequestUri?.Host ?? "unknown-host";

            var remaining = _throttle.GetRemainingCooldown(host);
            if (remaining > TimeSpan.Zero)
            {
                _logger.LogWarning(
                    "OCPI partner host {Host} circuit breaker open for another {Remaining} " +
                    "(tripped by an earlier 429) — skipping {Method} {Url} without hitting the network",
                    host, remaining, request.Method, request.RequestUri);
                return SyntheticTooManyRequests(request, remaining);
            }

            await _throttle.WaitTurnAsync(host, ct);

            var response = await base.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var (cooldown, rawRetryAfter) = GetCooldown(response);
                _throttle.TripBreaker(host, cooldown);

                var body = await CaptureBodyAsync(response, ct);
                var headerDump = string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join("|", h.Value)}"));

                _logger.LogWarning(
                    "OCPI partner host {Host} returned 429 Too Many Requests for {Method} {Url} — " +
                    "Retry-After={RetryAfter}; tripping breaker for {Cooldown}. Headers: [{Headers}]. Body: {Body}",
                    host, request.Method, request.RequestUri, rawRetryAfter ?? "(not sent)", cooldown,
                    headerDump, Truncate(body, 500));
            }

            return response;
        }

        private (TimeSpan Cooldown, string? RawRetryAfter) GetCooldown(HttpResponseMessage response)
        {
            var retryAfter = response.Headers.RetryAfter;
            if (retryAfter != null)
            {
                if (retryAfter.Delta.HasValue)
                    return (Min(retryAfter.Delta.Value, _cooldownCeiling), $"{retryAfter.Delta.Value.TotalSeconds}s");

                if (retryAfter.Date.HasValue)
                {
                    var delta = retryAfter.Date.Value - DateTimeOffset.UtcNow;
                    if (delta > TimeSpan.Zero)
                        return (Min(delta, _cooldownCeiling), retryAfter.Date.Value.ToString("O"));
                }
            }

            return (_defaultCooldown, null);
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

        /// <summary>Reads the 429 body for logging, then rewrites it back onto the response so any
        /// downstream code that also reads <c>response.Content</c> still sees it.</summary>
        private static async Task<string> CaptureBodyAsync(HttpResponseMessage response, CancellationToken ct)
        {
            string body;
            try { body = await response.Content.ReadAsStringAsync(ct); }
            catch { return string.Empty; }

            response.Content = new StringContent(body);
            return body;
        }

        private static HttpResponseMessage SyntheticTooManyRequests(HttpRequestMessage request, TimeSpan retryAfter)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                RequestMessage = request,
                ReasonPhrase = "Circuit breaker open (client-side, no request sent)",
                Content = new StringContent(string.Empty)
            };
            resp.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter);
            return resp;
        }
    }
}
