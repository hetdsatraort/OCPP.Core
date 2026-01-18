using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargingHubRequestDto
    {
        [Required]
        public string AddressLine1 { get; set; }
        
        public string AddressLine2 { get; set; }
        
        public string AddressLine3 { get; set; }
        
        public string ChargingHubImage { get; set; }
        
        [Required]
        public string City { get; set; }
        
        [Required]
        public string State { get; set; }
        
        [Required]
        public string Pincode { get; set; }
        
        [Required]
        public string Latitude { get; set; }
        
        [Required]
        public string Longitude { get; set; }
        
        [Required]
        public TimeOnly OpeningTime { get; set; }
        
        [Required]
        public TimeOnly ClosingTime { get; set; }
        
        public string TypeATariff { get; set; }
        
        public string TypeBTariff { get; set; }
        
        public string Amenities { get; set; }
        
        public string AdditionalInfo1 { get; set; }
        
        public string AdditionalInfo2 { get; set; }
        
        public string AdditionalInfo3 { get; set; }
    }

    public class ChargingHubUpdateDto : ChargingHubRequestDto
    {
        [Required]
        public string RecId { get; set; }
    }

    public class ChargingHubSearchDto
    {
        [Required]
        [Range(-90, 90)]
        public double Latitude { get; set; }
        
        [Required]
        [Range(-180, 180)]
        public double Longitude { get; set; }
        
        [Required]
        [Range(0.1, 100)]
        public double RadiusKm { get; set; }
    }
}
