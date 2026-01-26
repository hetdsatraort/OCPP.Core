using System.Collections.Generic;

namespace OCPP.Core.Management.Models.ChargingHub
{
    public class ChargingHubResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ChargingHubDto Hub { get; set; }
    }

    public class ChargingHubListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ChargingHubDto> Hubs { get; set; }
        public int TotalCount { get; set; }
    }

    public class ChargingHubDetailsResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ChargingHubDto Hub { get; set; }
        public List<ChargingStationDto> Stations { get; set; }
        public List<ReviewDto> Reviews { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class ChargingStationResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ChargingStationDto Station { get; set; }
    }

    public class ChargingStationListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ChargingStationDto> Stations { get; set; }
        public int TotalCount { get; set; }
    }

    public class ChargingStationDetailsResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ChargingStationDto Station { get; set; }
        public List<ChargerDto> Chargers { get; set; }
        public List<ReviewDto> Reviews { get; set; }
    }

    public class ChargerResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ChargerDto Charger { get; set; }
    }

    public class ChargerListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ChargerDto> Chargers { get; set; }
        public int TotalCount { get; set; }
    }

    public class ReviewResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ReviewDto Review { get; set; }
    }

    public class ReviewListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ReviewDto> Reviews { get; set; }
        public int TotalCount { get; set; }
        public double AverageRating { get; set; }
    }

    /// <summary>
    /// Comprehensive response with hubs, stations, and chargers
    /// </summary>
    public class ChargingHubComprehensiveResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ChargingHubWithStationsDto> Hubs { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Hub with nested stations and chargers
    /// </summary>
    public class ChargingHubWithStationsDto
    {
        // Hub info
        public string RecId { get; set; }
        public string ChargingHubName { get; set; }
        public string AddressLine1 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string ChargingHubImage { get; set; }
        public string OpeningTime { get; set; }
        public string ClosingTime { get; set; }
        public string TypeATariff { get; set; }
        public string TypeBTariff { get; set; }
        public string Amenities { get; set; }
        
        // Calculated fields
        public double? DistanceKm { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalStations { get; set; }
        public int TotalChargers { get; set; }
        public int AvailableChargers { get; set; }
        
        // Nested data
        public List<ChargingStationWithChargersDto> Stations { get; set; }
    }

    /// <summary>
    /// Station with nested chargers
    /// </summary>
    public class ChargingStationWithChargersDto
    {
        // Station info
        public string RecId { get; set; }
        public string ChargingPointId { get; set; }
        public string ChargePointName { get; set; }
        public int ChargingGunCount { get; set; }
        public string ChargingStationImage { get; set; }
        
        // Calculated fields
        public int TotalChargers { get; set; }
        public int AvailableChargers { get; set; }
        
        // Nested data
        public List<ChargerDto> Chargers { get; set; }
    }
}
