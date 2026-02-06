using System;

namespace OCPP.Core.Management.Models.ChargingSession
{
    public class StartChargingSessionRequestDto
    {
        public string ChargingGunId { get; set; }
        public string ChargingStationId { get; set; }
        public string UserId { get; set; }
        public string ChargeTagId { get; set; }
        public int ConnectorId { get; set; }
        public string StartMeterReading { get; set; }
        public string ChargingTariff { get; set; }
    }

    public class EndChargingSessionRequestDto
    {
        public string SessionId { get; set; }
        public string EndMeterReading { get; set; }
    }

    public class UnlockConnectorRequestDto
    {
        public string ChargingStationId { get; set; }
        public int ConnectorId { get; set; }
    }

    public class ChargingSessionDto
    {
        public string RecId { get; set; }
        public string ChargingGunId { get; set; }
        public string ConnectorName { get; set; }
        public string ChargingStationId { get; set; }
        public string ChargingStationName { get; set; }
        public string ChargingHubName { get; set; }
        public string StartMeterReading { get; set; }
        public string EndMeterReading { get; set; }
        public string EnergyTransmitted { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string ChargingSpeed { get; set; }
        public string ChargingTariff { get; set; }
        public string ChargingTotalFee { get; set; }
        public string Status { get; set; }
        public TimeSpan? Duration { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        public double? SoCStart { get; set; }
        public double? SoCEnd { get; set; }
        public DateTime? SoCLastUpdate { get; set; }
    }

    public class ChargingGunStatusDto
    {
        public string ChargingGunId { get; set; }
        public string ChargingStationId { get; set; }
        public string ChargingStationName { get; set; }
        public string Status { get; set; }
        public string CurrentSessionId { get; set; }
        public DateTime? LastStatusUpdate { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class ChargingSessionResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
