using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace OCPI.Core.Roaming.BackgroundServices
{
    /// <summary>
    /// Per-host proactive pacing + circuit breaker shared by every outbound call to an OCPI
    /// partner — background pushes/pulls and admin/driver-initiated calls alike.
    ///
    /// An earlier version of this retried a 429 in-process with backoff honouring Retry-After.
    /// Against at least one partner (Numocity) that didn't help — the 429 comes back in
    /// single-digit milliseconds even on the very first call of a freshly restarted process, which
    /// is the signature of a gateway-level block (by IP or token) rather than a live request
    /// counter, and retrying into an active block risks resetting/extending it rather than working
    /// around it.
    ///
    /// So this trips a breaker per host instead: on any 429, every subsequent call to that host —
    /// from either background service or a controller, any partner sharing the host — fails FAST
    /// (no network call, no blocking wait) until the cooldown passes. Callers see a 429 carrying
    /// <see cref="CircuitOpenHeader"/> (test with <see cref="IsCircuitOpen"/>) and handle it as
    /// "partner unavailable for now": background services log and try again next cycle,
    /// controllers return a rate-limited message.
    ///
    /// A few calls cost more to drop than one extra request into a block — a driver's own Stop,
    /// the first STOP_SESSION of a limit auto-stop, onboarding. Those run inside
    /// <see cref="BypassBreaker"/>: still paced, still able to trip/extend the breaker on a real
    /// 429, but sent even while it is open.
    ///
    /// Registered as a singleton so this state is shared across every transient
    /// <see cref="OcpiPartnerRateLimitHandler"/> instance HttpClientFactory creates. The state is
    /// in memory only, so a process restart clears it (the partner's own block is unaffected).
    /// </summary>
    public sealed class OcpiPartnerThrottle
    {
        /// <summary>Response header the handler adds to its own fail-fast 429 (breaker open, nothing sent).</summary>
        public const string CircuitOpenHeader = "X-HyCharge-Circuit-Open";

        private static readonly AsyncLocal<bool> _bypassBreaker = new();

        private sealed class HostState
        {
            public readonly SemaphoreSlim Gate = new(1, 1);
            public DateTime LastRequestUtc = DateTime.MinValue;
            public DateTime CooldownUntilUtc = DateTime.MinValue;
        }

        private sealed class BypassScope : IDisposable
        {
            private readonly bool _previous;
            private bool _disposed;

            public BypassScope(bool previous) => _previous = previous;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _bypassBreaker.Value = _previous;
            }
        }

        private readonly ConcurrentDictionary<string, HostState> _hosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _minInterval;

        /// <summary>
        /// Lets partner calls made inside the returned scope through an open breaker. They are still
        /// paced, and a real 429 still trips/extends the breaker. Reserve it for calls where not
        /// sending costs more than one request into an active block (see the class summary).
        /// Open it with <c>using</c> in the async method that makes the calls: the flag is an
        /// <see cref="AsyncLocal{T}"/>, so it flows into the handler and is restored on dispose.
        /// </summary>
        public static IDisposable BypassBreaker()
        {
            var previous = _bypassBreaker.Value;
            _bypassBreaker.Value = true;
            return new BypassScope(previous);
        }

        /// <summary>True inside a <see cref="BypassBreaker"/> scope.</summary>
        public static bool IsBypassingBreaker => _bypassBreaker.Value;

        /// <summary>True when <paramref name="response"/> is the handler's own fail-fast 429, i.e. no request reached the partner.</summary>
        public static bool IsCircuitOpen(HttpResponseMessage response) =>
            response.StatusCode == HttpStatusCode.TooManyRequests && response.Headers.Contains(CircuitOpenHeader);

        /// <summary>Short human-readable wait for messages, e.g. "about 3 h 20 min" or "about 45 s".</summary>
        public static string DescribeWait(TimeSpan wait)
        {
            if (wait <= TimeSpan.Zero) return "a moment";
            if (wait < TimeSpan.FromMinutes(1)) return $"about {Math.Ceiling(wait.TotalSeconds)} s";
            if (wait < TimeSpan.FromHours(1)) return $"about {Math.Ceiling(wait.TotalMinutes)} min";

            var hours = (int)wait.TotalHours;
            return wait.Minutes == 0 ? $"about {hours} h" : $"about {hours} h {wait.Minutes} min";
        }

        /// <summary>Remaining cooldown for the host of <paramref name="url"/>; zero when the breaker isn't tripped or the url isn't absolute.</summary>
        public TimeSpan GetRemainingCooldownForUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) ? GetRemainingCooldown(uri.Host) : TimeSpan.Zero;

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
    /// DelegatingHandler installed on the named "OcpiPartner" HttpClient. Every outbound call to an
    /// OCPI partner platform must use that client: both background services, and the admin/eMSP
    /// controller calls (Test Connection, onboarding, START/STOP_SESSION, tariff and live-status
    /// lookups, endpoint discovery). The plain default HttpClient is only for our own services.
    /// See <see cref="OcpiPartnerThrottle"/> for why this is a fail-fast circuit breaker rather
    /// than a retry-with-backoff loop, and for the <see cref="OcpiPartnerThrottle.BypassBreaker"/>
    /// exception.
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
                if (!OcpiPartnerThrottle.IsBypassingBreaker)
                {
                    _logger.LogWarning(
                        "OCPI partner host {Host} circuit breaker open for another {Remaining} " +
                        "(tripped by an earlier 429) — skipping {Method} {Url} without hitting the network",
                        host, remaining, request.Method, request.RequestUri);
                    return SyntheticTooManyRequests(request, remaining);
                }

                _logger.LogWarning(
                    "OCPI partner host {Host} circuit breaker open for another {Remaining}, but sending " +
                    "priority call {Method} {Url} anyway (breaker bypass)",
                    host, remaining, request.Method, request.RequestUri);
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
            resp.Headers.TryAddWithoutValidation(OcpiPartnerThrottle.CircuitOpenHeader, "true");
            return resp;
        }
    }
}
