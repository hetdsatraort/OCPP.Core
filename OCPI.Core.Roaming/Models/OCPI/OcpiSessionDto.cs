using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Session DTO - Represents a charging session
    /// </summary>
    public class OcpiSessionDto
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        public DateTime? EndDateTime { get; set; }

        [Required]
        public decimal KWh { get; set; }

        [Required]
        public OcpiCdrToken CdrToken { get; set; }

        [Required]
        public string AuthMethod { get; set; } // AUTH_REQUEST, COMMAND, WHITELIST

        public string AuthorizationReference { get; set; }

        [Required]
        public string LocationId { get; set; }

        [Required]
        public string EvseUid { get; set; }

        [Required]
        public string ConnectorId { get; set; }

        public string MeterId { get; set; }

        [Required]
        public string Currency { get; set; }

        public List<ChargingPeriod> ChargingPeriods { get; set; }

        public decimal? TotalCost { get; set; }

        [Required]
        public string Status { get; set; } // ACTIVE, COMPLETED, INVALID, PENDING

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Charging Period details
    /// </summary>
    public class ChargingPeriod
    {
        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public List<CdrDimension> Dimensions { get; set; }

        public string TariffId { get; set; }
    }

    /// <summary>
    /// CDR Dimension - Usage dimension
    /// </summary>
    public class CdrDimension
    {
        [Required]
        public string Type { get; set; } // ENERGY, TIME, FLAT, etc.

        [Required]
        public decimal Volume { get; set; }
    }

    /// <summary>
    /// CDR Token - Token used for charging
    /// </summary>
    public class OcpiCdrToken
    {
        public string CountryCode { get; set; }
        public string PartyId { get; set; }

        [Required]
        public string Uid { get; set; }

        [Required]
        public string Type { get; set; } // RFID, APP_USER, etc.

        public string ContractId { get; set; }
    }

    /// <summary>
    /// Start Session Request
    /// </summary>
    public class StartSessionRequestDto
    {
        [Required]
        public string LocationId { get; set; }

        [Required]
        public string EvseUid { get; set; }

        [Required]
        public string ConnectorId { get; set; }

        [Required]
        public string TokenUid { get; set; }

        public string AuthorizationReference { get; set; }
    }

    /// <summary>
    /// Stop Session Request
    /// </summary>
    public class StopSessionRequestDto
    {
        [Required]
        public string SessionId { get; set; }
    }
}
