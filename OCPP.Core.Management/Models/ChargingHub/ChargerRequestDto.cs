using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargerRequestDto
    {
        [Required]
        public string ChargePointId { get; set; }
        
        [Required]
        [Range(1, 100)]
        public int ConnectorId { get; set; }
        
        [Required]
        public string ConnectorName { get; set; }
    }

    public class ChargerUpdateDto : ChargerRequestDto
    {
        public string LastStatus { get; set; }
    }
}
