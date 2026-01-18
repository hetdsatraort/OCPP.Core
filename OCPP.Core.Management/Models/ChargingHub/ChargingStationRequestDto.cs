using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargingStationRequestDto
    {
        [Required]
        public string ChargingHubId { get; set; }
        
        [Required]
        public string ChargingPointId { get; set; }
        
        [Range(1, 10)]
        public int ChargingGunCount { get; set; }
        
        public string ChargingStationImage { get; set; }
    }

    public class ChargingStationUpdateDto : ChargingStationRequestDto
    {
        [Required]
        public string RecId { get; set; }
    }
}
