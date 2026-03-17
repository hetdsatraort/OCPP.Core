using System;
using System.Collections.Generic;

namespace OCPP.Core.Management.Models.Reports
{
    // ── Generic wrapper ───────────────────────────────────────────────────────
    public class ReportResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    // ── Dashboard KPI summary ─────────────────────────────────────────────────
    public class ReportSummaryDto
    {
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int ActiveSessions { get; set; }

        // Energy
        public double TotalEnergyKwh { get; set; }
        public double AvgEnergyPerSessionKwh { get; set; }

        // Revenue
        public double TotalRevenueInr { get; set; }
        public double AvgRevenuePerSessionInr { get; set; }

        // Time
        public double TotalChargingTimeHours { get; set; }
        public double AvgSessionDurationMinutes { get; set; }

        // Performance
        public double AvgChargingSpeedKw { get; set; }

        // Battery / SoC
        public double AvgSocGainPercent { get; set; }
        public int SessionsWithSocData { get; set; }

        // Usage patterns
        public int PeakHourOfDay { get; set; }   // 0-23, -1 if no data

        // Period info
        public string DateRangeFrom { get; set; }
        public string DateRangeTo { get; set; }
        public string Currency { get; set; } = "INR";
    }

    // ── Flat enriched session row (table view + CSV) ──────────────────────────
    public class SessionReportRowDto
    {
        public string SessionId { get; set; }

        // Location
        public string HubId { get; set; }
        public string HubName { get; set; }
        public string HubCity { get; set; }
        public string StationRecId { get; set; }
        public string ChargePointId { get; set; }
        public string GunId { get; set; }
        public string ConnectorId { get; set; }
        public string ChargerType { get; set; }
        public string PowerOutputKw { get; set; }

        // User
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserPhone { get; set; }
        public string UserEmail { get; set; }

        // Vehicle
        public string VehicleManufacturer { get; set; }
        public string VehicleModel { get; set; }
        public string VehicleRegistration { get; set; }
        public double? BatteryCapacityKwh { get; set; }

        // Timing
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double DurationMinutes { get; set; }

        // Metering
        public double StartMeterKwh { get; set; }
        public double EndMeterKwh { get; set; }
        public double EnergyKwh { get; set; }

        // Cost
        public double TariffPerKwh { get; set; }
        public double TotalFeeInr { get; set; }

        // Battery / SoC
        public double? SoCStartPct { get; set; }
        public double? SoCEndPct { get; set; }
        public double? SoCGainPct { get; set; }

        // Meta
        public string Status { get; set; }
        public int? TransactionId { get; set; }
    }

    // ── Time-series trend point ───────────────────────────────────────────────
    public class TrendPointDto
    {
        public string Period { get; set; }           // e.g. "2026-03-18" / "2026-W11" / "2026-03"
        public DateTime PeriodStart { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public double TotalEnergyKwh { get; set; }
        public double TotalRevenueInr { get; set; }
        public double AvgDurationMinutes { get; set; }
        public double AvgEnergyKwh { get; set; }
        public double AvgChargingSpeedKw { get; set; }
    }

    // ── Per-hub performance ───────────────────────────────────────────────────
    public class HubPerformanceDto
    {
        public string HubId { get; set; }
        public string HubName { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int ActiveSessions { get; set; }
        public double TotalEnergyKwh { get; set; }
        public double TotalRevenueInr { get; set; }
        public double AvgSessionDurationMinutes { get; set; }
        public double AvgEnergyPerSessionKwh { get; set; }
        public double AvgChargingSpeedKw { get; set; }
        public int TotalStations { get; set; }
        public int TotalGuns { get; set; }
    }

    // ── Per-station performance ───────────────────────────────────────────────
    public class StationPerformanceDto
    {
        public string StationRecId { get; set; }
        public string ChargePointId { get; set; }
        public string HubId { get; set; }
        public string HubName { get; set; }
        public string HubCity { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public int ActiveSessions { get; set; }
        public double TotalEnergyKwh { get; set; }
        public double TotalRevenueInr { get; set; }
        public double AvgSessionDurationMinutes { get; set; }
        public double AvgEnergyPerSessionKwh { get; set; }
        public double AvgChargingSpeedKw { get; set; }
        public int GunCount { get; set; }
    }

    // ── Per-gun utilization ───────────────────────────────────────────────────
    public class GunUtilizationDto
    {
        public string GunId { get; set; }
        public string ConnectorId { get; set; }
        public string ChargerType { get; set; }
        public string PowerOutputKw { get; set; }
        public string StationRecId { get; set; }
        public string ChargePointId { get; set; }
        public string HubName { get; set; }
        public string HubCity { get; set; }
        public string CurrentStatus { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public double TotalEnergyKwh { get; set; }
        public double TotalRevenueInr { get; set; }
        public double TotalChargingHours { get; set; }
        public double AvgSessionDurationMinutes { get; set; }
        public double AvgEnergyPerSessionKwh { get; set; }
        /// <summary>Charging time as a % of the queried period (0-100). -1 when period cannot be determined.</summary>
        public double UtilizationPct { get; set; }
    }

    // ── Per-user activity ─────────────────────────────────────────────────────
    public class UserActivityDto
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public int TotalSessions { get; set; }
        public int CompletedSessions { get; set; }
        public double TotalEnergyKwh { get; set; }
        public double TotalSpentInr { get; set; }
        public double AvgSessionDurationMinutes { get; set; }
        public double AvgEnergyPerSessionKwh { get; set; }
        public DateTime? LastSessionTime { get; set; }
        public string FavoriteHubName { get; set; }
    }
}
