using System;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargerDto
    {
        public string RecId { get; set; }
        public string ChargingStationId { get; set; }
        public string ChargingHubId { get; set; }
        public string ChargePointId { get; set; }
        public string ConnectorId { get; set; }
        public string ChargerTypeId { get; set; }
        public string ChargerTypeName { get; set; }
        public string ChargerTariff { get; set; }
        public string PowerOutput { get; set; }
        public string ChargerStatus { get; set; }
        public string ChargerMeterReading { get; set; }
        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        
        // Connector Status Info (from OCPP)
        public string ConnectorName { get; set; }
        public string LastStatus { get; set; }
        public DateTime? LastStatusTime { get; set; }
        public double? LastMeter { get; set; }
        public DateTime? LastMeterTime { get; set; }
        
        // Station info
        public string ChargePointName { get; set; }
        public string ChargingHubName { get; set; }
    }
}

