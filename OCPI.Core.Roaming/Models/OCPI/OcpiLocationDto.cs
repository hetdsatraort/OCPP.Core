using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Location DTO - Represents a charging location
    /// </summary>
    public class OcpiLocationDto
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string Type { get; set; } // ON_STREET, PARKING_GARAGE, etc.

        public string Name { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string City { get; set; }

        public string PostalCode { get; set; }

        public string State { get; set; }

        [Required]
        public string Country { get; set; }

        [Required]
        public GeoLocation Coordinates { get; set; }

        public List<string> RelatedLocations { get; set; }

        public string ParkingType { get; set; }

        public List<OcpiEvseDto> Evses { get; set; }

        public List<string> Directions { get; set; }

        public BusinessDetails Operator { get; set; }
        public BusinessDetails SubOperator { get; set; }
        public BusinessDetails Owner { get; set; }

        public List<string> Facilities { get; set; }

        public string TimeZone { get; set; }

        public Hours OpeningTimes { get; set; }

        public bool? ChargingWhenClosed { get; set; }

        public List<Image> Images { get; set; }

        public EnergyMix EnergyMix { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }

        public bool Publish { get; set; } = true;
    }

    /// <summary>
    /// Geographic coordinates
    /// </summary>
    public class GeoLocation
    {
        [Required]
        [Range(-90, 90)]
        public decimal Latitude { get; set; }

        [Required]
        [Range(-180, 180)]
        public decimal Longitude { get; set; }
    }

    /// <summary>
    /// EVSE (Electric Vehicle Supply Equipment) DTO
    /// </summary>
    public class OcpiEvseDto
    {
        [Required]
        public string Uid { get; set; }

        public string EvseId { get; set; }

        [Required]
        public string Status { get; set; } // AVAILABLE, BLOCKED, CHARGING, etc.

        public List<StatusSchedule> StatusSchedule { get; set; }

        public List<string> Capabilities { get; set; }

        [Required]
        public List<OcpiConnectorDto> Connectors { get; set; }

        public string FloorLevel { get; set; }

        public GeoLocation Coordinates { get; set; }

        public string PhysicalReference { get; set; }

        public List<string> Directions { get; set; }

        public string ParkingRestrictions { get; set; }

        public List<Image> Images { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Connector DTO
    /// </summary>
    public class OcpiConnectorDto
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string Standard { get; set; } // IEC_62196_T2, CHADEMO, etc.

        [Required]
        public string Format { get; set; } // SOCKET, CABLE

        [Required]
        public string PowerType { get; set; } // AC_1_PHASE, AC_3_PHASE, DC

        public int? MaxVoltage { get; set; }

        public int? MaxAmperage { get; set; }

        public int? MaxElectricPower { get; set; }

        public List<string> TariffIds { get; set; }

        public string TermsAndConditions { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Status Schedule
    /// </summary>
    public class StatusSchedule
    {
        [Required]
        public DateTime PeriodBegin { get; set; }

        public DateTime? PeriodEnd { get; set; }

        [Required]
        public string Status { get; set; }
    }

    /// <summary>
    /// Opening hours
    /// </summary>
    public class Hours
    {
        [Required]
        public bool TwentyFourSeven { get; set; }

        public List<RegularHours> RegularHours { get; set; }
        public List<ExceptionalPeriod> ExceptionalOpenings { get; set; }
        public List<ExceptionalPeriod> ExceptionalClosings { get; set; }
    }

    public class RegularHours
    {
        [Required]
        public int Weekday { get; set; } // 1-7 (Monday-Sunday)

        [Required]
        public string PeriodBegin { get; set; } // HH:MM

        [Required]
        public string PeriodEnd { get; set; } // HH:MM
    }

    public class ExceptionalPeriod
    {
        [Required]
        public DateTime PeriodBegin { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }
    }

    /// <summary>
    /// Energy Mix
    /// </summary>
    public class EnergyMix
    {
        [Required]
        public bool IsGreenEnergy { get; set; }

        public List<EnergySource> EnergySources { get; set; }
        public List<EnvironmentalImpact> EnvironmentalImpacts { get; set; }

        public string SupplierName { get; set; }
        public string EnergyProductName { get; set; }
    }

    public class EnergySource
    {
        [Required]
        public string Source { get; set; } // SOLAR, WIND, etc.

        [Required]
        public double Percentage { get; set; }
    }

    public class EnvironmentalImpact
    {
        [Required]
        public string Category { get; set; }

        [Required]
        public double Amount { get; set; }
    }
}
