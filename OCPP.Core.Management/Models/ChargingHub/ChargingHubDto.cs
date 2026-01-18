using System;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargingHubDto
    {
        public string RecId { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string ChargingHubImage { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public TimeOnly OpeningTime { get; set; }
        public TimeOnly ClosingTime { get; set; }
        public string TypeATariff { get; set; }
        public string TypeBTariff { get; set; }
        public string Amenities { get; set; }
        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public string AdditionalInfo3 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
        public double? DistanceKm { get; set; } // For location-based searches
        public int StationCount { get; set; } // Number of stations in this hub
        public double? AverageRating { get; set; } // Average rating from reviews
    }
}
