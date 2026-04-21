using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Stores charging session data received from OCPI partners
    /// </summary>
    public class OcpiPartnerSession
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Partner's country code
        /// </summary>
        [Required]
        [MaxLength(2)]
        public string CountryCode { get; set; }

        /// <summary>
        /// Partner's party ID
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string PartyId { get; set; }

        /// <summary>
        /// Unique identifier for the session
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string SessionId { get; set; }

        /// <summary>
        /// Start timestamp of the session
        /// </summary>
        public DateTime StartDateTime { get; set; }

        /// <summary>
        /// End timestamp of the session (null if ongoing)
        /// </summary>
        public DateTime? EndDateTime { get; set; }

        /// <summary>
        /// Total consumed energy in kWh
        /// </summary>
        public decimal? TotalEnergy { get; set; }

        /// <summary>
        /// Session status (ACTIVE, COMPLETED, INVALID, etc.)
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; }

        /// <summary>
        /// Location ID where the session is happening
        /// </summary>
        [MaxLength(36)]
        public string LocationId { get; set; }

        /// <summary>
        /// EVSE UID being used
        /// </summary>
        [MaxLength(36)]
        public string EvseUid { get; set; }

        /// <summary>
        /// Connector ID being used
        /// </summary>
        [MaxLength(36)]
        public string ConnectorId { get; set; }

        /// <summary>
        /// Authorization reference
        /// </summary>
        [MaxLength(36)]
        public string AuthorizationReference { get; set; }

        /// <summary>
        /// Token UID used for authorization
        /// </summary>
        [MaxLength(36)]
        public string TokenUid { get; set; }

        /// <summary>
        /// Currency code (ISO 4217)
        /// </summary>
        [MaxLength(3)]
        public string Currency { get; set; }

        /// <summary>
        /// Total cost of the session
        /// </summary>
        public decimal? TotalCost { get; set; }

        /// <summary>
        /// Partner credential ID (foreign key)
        /// </summary>
        public int PartnerCredentialId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this session last updated by the partner
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
