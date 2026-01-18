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
}
