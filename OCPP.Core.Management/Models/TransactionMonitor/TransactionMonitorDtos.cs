using System;
using System.Collections.Generic;

namespace OCPP.Core.Management.Models.TransactionMonitor
{
    public class TransactionMonitorResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class TransactionMonitorListRequestDto
    {
        /// <summary>open | stale | system-closed | closed | all (default: open)</summary>
        public string Status { get; set; } = "open";
        public string ChargePointId { get; set; }
        /// <summary>Matches transaction id (exact), charge point id or start tag id (contains)</summary>
        public string Search { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class TransactionMonitorItemDto
    {
        public int TransactionId { get; set; }
        public string Uid { get; set; }
        public string ChargePointId { get; set; }
        public string ChargePointName { get; set; }
        public int ConnectorId { get; set; }
        public string ChargingStationId { get; set; }
        public string ChargingHubId { get; set; }
        public string ChargingHubName { get; set; }

        public string StartTagId { get; set; }
        public DateTime StartTime { get; set; }
        public double MeterStart { get; set; }
        public DateTime? StopTime { get; set; }
        public double? MeterStop { get; set; }
        public string StopReason { get; set; }
        public bool IsOpen { get; set; }
        public bool ClosedBySystem { get; set; }
        public double DurationMinutes { get; set; }
        /// <summary>kWh; for open transactions uses the connector's live meter</summary>
        public double EnergyKwh { get; set; }

        public string ConnectorStatus { get; set; }
        public double? ConnectorLastMeter { get; set; }
        public DateTime? ConnectorLastMeterTime { get; set; }
        public double? MinutesSinceLastMeter { get; set; }

        public string SessionId { get; set; }
        public bool? SessionActive { get; set; }
        public DateTime? SessionEndTime { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserPhone { get; set; }

        public bool IsOcpiSession { get; set; }
        public int? SupersededByTransactionId { get; set; }

        public bool IsStale { get; set; }
        /// <summary>Superseded | SessionEnded | NoSession | NoMeterUpdates | LongRunning</summary>
        public List<string> StaleReasons { get; set; } = new List<string>();
    }

    public class TransactionMonitorSummaryDto
    {
        public int OpenTransactions { get; set; }
        public int StaleTransactions { get; set; }
        public int Superseded { get; set; }
        public int SessionEnded { get; set; }
        public int NoSession { get; set; }
        public int NoMeterUpdates { get; set; }
        public int LongRunning { get; set; }
        public int SystemClosedLast24h { get; set; }
        public int StaleThresholdMinutes { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class TransactionForceCloseRequestDto
    {
        /// <summary>Optional meter stop (kWh). Defaults to the best known meter reading.</summary>
        public double? MeterStop { get; set; }
        public string Note { get; set; }
    }
}
