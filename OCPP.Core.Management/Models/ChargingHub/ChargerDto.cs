using System;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargerDto
    {
        public string ChargePointId { get; set; }
        public int ConnectorId { get; set; }
        public string ConnectorName { get; set; }
        public string LastStatus { get; set; }
        public DateTime? LastStatusTime { get; set; }
        public double? LastMeter { get; set; }
        public DateTime? LastMeterTime { get; set; }
        
        // Station info
        public string StationRecId { get; set; }
        public string ChargePointName { get; set; }
    }
}
