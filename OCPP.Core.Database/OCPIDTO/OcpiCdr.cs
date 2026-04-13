using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Charge Detail Record - Complete record of a charging session for billing
    /// </summary>
    public class OcpiCdr
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Country code of the CPO
        /// </summary>
        [Required]
        [MaxLength(2)]
        public string CountryCode { get; set; }

        /// <summary>
        /// Party ID of the CPO
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string PartyId { get; set; }

        /// <summary>
        /// Unique CDR identifier
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string CdrId { get; set; }

        /// <summary>
        /// Start timestamp of the charging session
        /// </summary>
        public DateTime StartDateTime { get; set; }

        /// <summary>
        /// End timestamp of the charging session
        /// </summary>
        public DateTime EndDateTime { get; set; }

        /// <summary>
        /// Session ID that this CDR is for
        /// </summary>
        [MaxLength(36)]
        public string SessionId { get; set; }

        /// <summary>
        /// Authorization reference
        /// </summary>
        [MaxLength(36)]
        public string AuthorizationReference { get; set; }

        /// <summary>
        /// Authorization method used
        /// </summary>
        [MaxLength(50)]
        public string AuthMethod { get; set; }

        /// <summary>
        /// Location ID
        /// </summary>
        [MaxLength(36)]
        public string LocationId { get; set; }

        /// <summary>
        /// EVSE UID
        /// </summary>
        [MaxLength(36)]
        public string EvseUid { get; set; }

        /// <summary>
        /// Connector ID
        /// </summary>
        [MaxLength(36)]
        public string ConnectorId { get; set; }

        /// <summary>
        /// Meter ID if available
        /// </summary>
        [MaxLength(255)]
        public string MeterId { get; set; }

        /// <summary>
        /// Currency code (ISO 4217)
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string Currency { get; set; }

        /// <summary>
        /// Total energy consumed in kWh
        /// </summary>
        public decimal TotalEnergy { get; set; }

        /// <summary>
        /// Total duration in hours
        /// </summary>
        public decimal TotalTime { get; set; }

        /// <summary>
        /// Total parking time in hours
        /// </summary>
        public decimal? TotalParkingTime { get; set; }

        /// <summary>
        /// Total cost excluding VAT
        /// </summary>
        public decimal TotalCostExclVat { get; set; }

        /// <summary>
        /// Total cost including VAT
        /// </summary>
        public decimal? TotalCostInclVat { get; set; }

        /// <summary>
        /// Token UID used for authorization
        /// </summary>
        [MaxLength(36)]
        public string TokenUid { get; set; }

        /// <summary>
        /// Partner credential ID (foreign key) - null if generated locally
        /// </summary>
        public int? PartnerCredentialId { get; set; }

        /// <summary>
        /// Local charging session ID reference
        /// </summary>
        [MaxLength(255)]
        public string LocalSessionId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this CDR last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
