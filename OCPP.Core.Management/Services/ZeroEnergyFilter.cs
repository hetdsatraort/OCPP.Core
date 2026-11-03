using System;
using System.Linq;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Database.OCPIDTO;

namespace OCPP.Core.Management.Services
{
    /// <summary>
    /// The single definition of a "zero-energy session" for the session lists and reports: a session
    /// that has ENDED having delivered less than <see cref="ThresholdKwh"/>. Active sessions never count,
    /// they may simply not have delivered energy yet. Matches the zero-energy counts on the dashboard
    /// trend (ReportsController.GetEnergyTrend).
    ///
    /// The ExcludeZeroEnergy extensions are SQL-translatable so they can run before Count/Skip/Take.
    /// </summary>
    public static class ZeroEnergyFilter
    {
        public const double ThresholdKwh = 0.01;
        private const decimal ThresholdKwhDecimal = 0.01m;

        /// <summary>
        /// ChargingSession.EnergyTransmitted is a string, so this matches on text rather than parsing it
        /// in SQL (a CAST would throw on a malformed value). Every writer formats it as "0" or with
        /// ToString("F2"/"F3"), so a value below 0.01 kWh is exactly: null/empty, "0", "0.0", anything
        /// starting "0.00" ("0.00", "0.000", "0.004"), or negative.
        /// </summary>
        public static IQueryable<ChargingSession> ExcludeZeroEnergy(this IQueryable<ChargingSession> query)
            => query.Where(s => s.EndTime == DateTime.MinValue
                || !(s.EnergyTransmitted == null
                     || s.EnergyTransmitted == ""
                     || s.EnergyTransmitted == "0"
                     || s.EnergyTransmitted == "0.0"
                     || s.EnergyTransmitted.StartsWith("0.00")
                     || s.EnergyTransmitted.StartsWith("-")));

        public static IQueryable<OcpiPartnerSession> ExcludeZeroEnergy(this IQueryable<OcpiPartnerSession> query)
            => query.Where(s => s.Status == "ACTIVE" || (s.TotalEnergy ?? 0m) >= ThresholdKwhDecimal);

        public static IQueryable<OcpiHostedSession> ExcludeZeroEnergy(this IQueryable<OcpiHostedSession> query)
            => query.Where(s => s.Status == "ACTIVE" || (s.TotalEnergy ?? 0m) >= ThresholdKwhDecimal);

        /// <summary>In-memory check for an OCPI session (partner or hosted), same rule as the query filters.</summary>
        public static bool IsZeroEnergy(string status, decimal? totalEnergy)
            => !string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
               && (double)(totalEnergy ?? 0m) < ThresholdKwh;
    }
}
