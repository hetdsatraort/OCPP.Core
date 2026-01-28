using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargingHubRequestDto
    {
        [Required]
        public string ChargingHubName { get; set; }

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

    /// <summary>
    /// Comprehensive search request with filters and pagination
    /// </summary>
    public class ChargingHubComprehensiveSearchDto
    {
        // Pagination
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        // Location-based search (optional)
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }

        // Text search filters
        public string SearchTerm { get; set; }  // Search in hub name, city, state
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }

        // Status filters
        public string ChargerStatus { get; set; }  // Available, Charging, Faulted, etc.
        public bool? HasAvailableChargers { get; set; }

        // Sorting
        public string SortBy { get; set; } = "Distance";  // Distance, Name, Rating, CreatedOn
        public string SortOrder { get; set; } = "Asc";  // Asc, Desc
    }
}
