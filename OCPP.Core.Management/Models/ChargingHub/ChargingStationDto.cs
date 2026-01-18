using System;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargingStationDto
    {
        public string RecId { get; set; }
        public string ChargingPointId { get; set; }
        public string ChargingHubId { get; set; }
        public int ChargingGunCount { get; set; }
        public string ChargingStationImage { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        
        // Additional info from ChargePoint
        public string ChargePointName { get; set; }
        public string ChargePointComment { get; set; }
        
        // Hub info
        public string HubCity { get; set; }
        public string HubState { get; set; }
    }
}
