using System;
using System.Collections.Concurrent;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// Process-wide, in-memory estimate of a charging session's current output power, derived from
    /// successive cumulative-energy readings: kW = ΔkWh ÷ Δhours between the last two readings that
    /// actually changed. Used when no charger-reported power is available, i.e. for OCPI partner
    /// sessions (a CPO only pushes a running kWh total) and for local chargers that aren't currently
    /// reporting through the OCPP server.
    ///
    /// Nothing is persisted, so after an app restart the first reading only seeds a sample and a
    /// value appears from the next energy change onwards. Callers should fall back to the session's
    /// average power until then.
    /// </summary>
    public static class SessionPowerTracker
    {
        private sealed class Sample
        {
            public double EnergyKwh;
            public DateTime SampleTimeUtc;
            public double? PowerKw;
            /// <summary>When we observed the energy change that produced PowerKw, measured on our own clock.</summary>
            public DateTime PowerObservedUtc;
            public DateTime LastSeenUtc;
        }

        private static readonly ConcurrentDictionary<string, Sample> _samples = new ConcurrentDictionary<string, Sample>();
        private static readonly TimeSpan Retention = TimeSpan.FromHours(12);
        private static readonly TimeSpan PruneEvery = TimeSpan.FromMinutes(10);
        // Readings closer together than this give wildly inflated rates, so wait for a wider gap
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(10);
        private const double MaxPlausibleKw = 1000;
        private static DateTime _lastPruneUtc = DateTime.MinValue;

        /// <summary>
        /// Records a cumulative energy reading for <paramref name="sessionKey"/> and returns the
        /// power between the last two distinct readings. Returns null when there's no such pair yet,
        /// or when energy hasn't moved for longer than <paramref name="staleAfter"/>. In that case
        /// the last rate no longer describes what the charger is doing.
        /// </summary>
        /// <param name="sampleTimeUtc">When the reading was taken: the CPO's last_updated for partner
        /// sessions, otherwise the time of observation.</param>
        public static double? Observe(string sessionKey, double energyKwh, DateTime sampleTimeUtc, TimeSpan staleAfter)
        {
            if (string.IsNullOrEmpty(sessionKey)) return null;

            var now = DateTime.UtcNow;
            PruneIfDue(now);

            var sample = _samples.AddOrUpdate(
                sessionKey,
                _ => new Sample { EnergyKwh = energyKwh, SampleTimeUtc = sampleTimeUtc, LastSeenUtc = now },
                (_, prev) =>
                {
                    // Energy went backwards: the meter reset or the session data was replaced, so start over
                    if (energyKwh < prev.EnergyKwh)
                        return new Sample { EnergyKwh = energyKwh, SampleTimeUtc = sampleTimeUtc, LastSeenUtc = now };

                    var next = new Sample
                    {
                        EnergyKwh = prev.EnergyKwh,
                        SampleTimeUtc = prev.SampleTimeUtc,
                        PowerKw = prev.PowerKw,
                        PowerObservedUtc = prev.PowerObservedUtc,
                        LastSeenUtc = now
                    };

                    var elapsed = sampleTimeUtc - prev.SampleTimeUtc;
                    if (energyKwh > prev.EnergyKwh && elapsed >= MinInterval)
                    {
                        var kw = (energyKwh - prev.EnergyKwh) / elapsed.TotalHours;
                        if (kw > 0 && kw <= MaxPlausibleKw)
                        {
                            next.PowerKw = kw;
                            next.PowerObservedUtc = now;
                        }
                        next.EnergyKwh = energyKwh;
                        next.SampleTimeUtc = sampleTimeUtc;
                    }
                    return next;
                });

            return sample.PowerKw.HasValue && now - sample.PowerObservedUtc <= staleAfter
                ? sample.PowerKw
                : null;
        }

        /// <summary>Drops a session's samples, e.g. once it has ended.</summary>
        public static void Forget(string sessionKey)
        {
            if (!string.IsNullOrEmpty(sessionKey))
                _samples.TryRemove(sessionKey, out _);
        }

        private static void PruneIfDue(DateTime now)
        {
            if (now - _lastPruneUtc < PruneEvery) return;
            _lastPruneUtc = now;
            foreach (var entry in _samples)
            {
                if (now - entry.Value.LastSeenUtc > Retention)
                    _samples.TryRemove(entry.Key, out _);
            }
        }
    }
}
