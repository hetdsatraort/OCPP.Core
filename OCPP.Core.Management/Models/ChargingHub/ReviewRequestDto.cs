using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ReviewRequestDto
    {
        public string ChargingHubId { get; set; }
        
        public string ChargingStationId { get; set; }
        
        [Required]
        [Range(1, 5)]
        public int Rating { get; set; }
        
        [Required]
        [MaxLength(1000)]
        public string Description { get; set; }
        
        public string ReviewImage1 { get; set; }
        
        public string ReviewImage2 { get; set; }
        
        public string ReviewImage3 { get; set; }
        
        public string ReviewImage4 { get; set; }
    }

    public class ReviewUpdateDto : ReviewRequestDto
    {
        [Required]
        public string RecId { get; set; }
    }
}
