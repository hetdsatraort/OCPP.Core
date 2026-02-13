using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Models.ChargingHub;
using System;
using System.Collections.Generic;

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
        
        // Session Limits (all optional)
        public double? EnergyLimit { get; set; }  // kWh
        public double? CostLimit { get; set; }  // Currency units
        public int? TimeLimit { get; set; }  // Minutes
        public double? BatteryIncreaseLimit { get; set; }  // Percentage (0-100)
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
        public ChargingHubDto ChargingHub { get; set; }
        public ChargingGuns ChargingGun { get; set; }
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
        
        // Session Limits
        public double? EnergyLimit { get; set; }  // kWh
        public double? CostLimit { get; set; }  // Currency units
        public int? TimeLimit { get; set; }  // Minutes
        public double? BatteryIncreaseLimit { get; set; }  // Percentage (0-100)
    }

    public class ChargingGunStatusDto
    {
        public string ChargingGunId { get; set; }
        public string ChargingStationId { get; set; }
        public string ChargingStationName { get; set; }
        public string ConnectorId { get; set; }
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

    public class SessionLimitCheckDto
    {
        public string SessionId { get; set; }
        public string ChargingStationId { get; set; }
        public string UserId { get; set; }
        public bool HasViolations { get; set; }
        public List<string> ViolatedLimits { get; set; } = new List<string>();
        public SessionLimitStatus LimitStatus { get; set; }
    }

    public class SessionLimitStatus
    {
        // Energy
        public double? EnergyConsumed { get; set; }  // kWh
        public double? EnergyLimit { get; set; }
        public double? EnergyPercentage { get; set; }
        
        // Cost
        public double? CurrentCost { get; set; }
        public double? CostLimit { get; set; }
        public double? CostPercentage { get; set; }
        
        // Time
        public int? ElapsedMinutes { get; set; }
        public int? TimeLimit { get; set; }
        public double? TimePercentage { get; set; }
        
        // Battery
        public double? BatteryIncrease { get; set; }  // Percentage points
        public double? BatteryIncreaseLimit { get; set; }
        public double? BatteryPercentage { get; set; }
    }
}
