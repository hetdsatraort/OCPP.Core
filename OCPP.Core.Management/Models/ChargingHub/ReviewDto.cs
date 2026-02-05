using System;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ReviewDto
    {
        public string RecId { get; set; }
        public string UserId { get; set; }
        public string ChargingHubId { get; set; }
        public string ChargingStationId { get; set; }
        public int Rating { get; set; }
        public string Description { get; set; }
        public DateTime ReviewTime { get; set; }
        public string ReviewImage1 { get; set; }
        public string ReviewImage2 { get; set; }
        public string ReviewImage3 { get; set; }
        public string ReviewImage4 { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        
        // User info (if needed)
        public string UserName { get; set; }
        public string UserProfileImage { get; set; }
    }
}
