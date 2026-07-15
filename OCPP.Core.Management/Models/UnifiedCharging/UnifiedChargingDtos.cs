using System;
using System.Collections.Generic;

namespace OCPP.Core.Management.Models.UnifiedCharging
{
    public class UnifiedChargingResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class UnifiedConnectorDto
    {
        public string Id { get; set; }
        public ProviderType ProviderType { get; set; }
        public string ConnectorId { get; set; }
        public string ChargerTypeName { get; set; }
        public string PowerOutput { get; set; }
        /// <summary>Null for Partner connectors — pricing is set by the partner CPO and only known via CDR.</summary>
        public string Tariff { get; set; }
        public string Status { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class UnifiedStationDto
    {
        public string Id { get; set; }
        public ProviderType ProviderType { get; set; }
        public string Name { get; set; }
        public int TotalConnectors { get; set; }
        public int AvailableConnectors { get; set; }
        public List<UnifiedConnectorDto> Connectors { get; set; } = new List<UnifiedConnectorDto>();
    }

    public class UnifiedLocationDto
    {
        public string Id { get; set; }
        public ProviderType ProviderType { get; set; }
        public string Name { get; set; }
        public string AddressLine1 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Pincode { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public double? DistanceKm { get; set; }
        public double? AverageRating { get; set; }
        public int TotalStations { get; set; }
        public int AvailableStations { get; set; }
        public int TotalConnectors { get; set; }
        public int AvailableConnectors { get; set; }
        /// <summary>Null for Local hubs.</summary>
        public string PartnerName { get; set; }
        public List<UnifiedStationDto> Stations { get; set; } = new List<UnifiedStationDto>();
    }

    public class UnifiedLocationListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<UnifiedLocationDto> Locations { get; set; } = new List<UnifiedLocationDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class UnifiedLimitProgressDto
    {
        public double? EnergyPct { get; set; }
        public double? CostPct { get; set; }
        public double? TimePct { get; set; }
    }

    public class UnifiedBatteryStateOfChargeDto
    {
        public double? StartSoC { get; set; }
        public double? EndSoC { get; set; }
        public double? CurrentSoC { get; set; }
        public double? SoCGain { get; set; }
        public DateTime? LastUpdate { get; set; }
        public string Unit { get; set; } = "%";
        public bool IsRealtime { get; set; }
        public string DataSource { get; set; }
    }

    public class UnifiedSessionDto
    {
        public string Id { get; set; }
        public ProviderType ProviderType { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double? MeterStart { get; set; }
        public double? MeterCurrent { get; set; }
        public double EnergyDelivered { get; set; }
        public decimal Cost { get; set; }
        public string Currency { get; set; } = "INR";
        public string LocationName { get; set; }
        public string PartnerName { get; set; }
        public string StationId { get; set; }
        public string ConnectorId { get; set; }

        // Limits
        public double? EnergyLimit { get; set; }
        public double? CostLimit { get; set; }
        public int? TimeLimit { get; set; }
        public double? BatteryIncreaseLimit { get; set; }
        public UnifiedLimitProgressDto LimitProgress { get; set; }

        public UnifiedBatteryStateOfChargeDto BatteryStateOfCharge { get; set; }
        public object WalletTransaction { get; set; }

        /// <summary>
        /// The untouched response payload from the delegated controller, for consumers that
        /// need provider-specific fields not yet promoted into the unified shape above.
        /// </summary>
        public object Raw { get; set; }
    }

    public class UnifiedSessionListResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<UnifiedSessionDto> Sessions { get; set; } = new List<UnifiedSessionDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    // ── Requests ──────────────────────────────────────────────────────────

    public class UnifiedLocationSearchDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double RadiusKm { get; set; } = 50;
    }

    public class UnifiedLocationComprehensiveSearchDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
        public string SearchTerm { get; set; }
        public string City { get; set; }
        public string State { get; set; }
    }

    public class UnifiedStartSessionRequestDto
    {
        /// <summary>Composite connector id — "L:{gunRecId}" or "P:{connectorDbId}".</summary>
        public string ConnectorId { get; set; }
        public string ChargeTagId { get; set; }
        /// <summary>OCPI token UID — used for Partner connectors only; falls back to ChargeTagId when omitted.</summary>
        public string TokenUid { get; set; }
        public double? EnergyLimit { get; set; }
        public double? CostLimit { get; set; }
        public int? TimeLimit { get; set; }
        public double? BatteryIncreaseLimit { get; set; }
    }

    public class UnifiedStopSessionRequestDto
    {
        /// <summary>Composite session id — "L:{sessionRecId}" or "P:{ocpiSessionId}".</summary>
        public string SessionId { get; set; }
        public string EndMeterReading { get; set; }
    }

    public class UnifiedUnlockConnectorRequestDto
    {
        /// <summary>Composite connector id — Local only.</summary>
        public string ConnectorId { get; set; }
    }
}
