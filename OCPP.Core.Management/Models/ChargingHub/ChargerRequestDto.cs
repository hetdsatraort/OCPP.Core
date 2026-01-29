using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargerRequestDto
    {
        [Required]
        public string ChargingStationId { get; set; }
        
        [Required]
        public string ChargePointId { get; set; }
        
        [Required]
        public string ConnectorId { get; set; }
        
        [Required]
        public string ChargerTypeId { get; set; }
        
        public string ChargerTariff { get; set; }
        
        public string PowerOutput { get; set; }
        
        public string AdditionalInfo1 { get; set; }
        
        public string AdditionalInfo2 { get; set; }
    }

    public class ChargerUpdateDto
    {
        [Required]
        public string RecId { get; set; }
        
        public string ChargerTypeId { get; set; }
        
        public string ChargerTariff { get; set; }
        
        public string PowerOutput { get; set; }
        
        public string ChargerStatus { get; set; }
        
        public string AdditionalInfo1 { get; set; }
        
        public string AdditionalInfo2 { get; set; }
    }
}

