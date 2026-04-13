using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Authorization token for EV drivers from OCPI partners
    /// </summary>
    public class OcpiToken
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Country code of the eMSP
        /// </summary>
        [Required]
        [MaxLength(2)]
        public string CountryCode { get; set; }

        /// <summary>
        /// Party ID of the eMSP
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string PartyId { get; set; }

        /// <summary>
        /// Unique token identifier
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string TokenUid { get; set; }

        /// <summary>
        /// Token type (RFID, APP_USER, AD_HOC_USER, etc.)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Type { get; set; }

        /// <summary>
        /// Visual readable number (e.g., RFID card number)
        /// </summary>
        [MaxLength(64)]
        public string VisualNumber { get; set; }

        /// <summary>
        /// Issuing company name
        /// </summary>
        [MaxLength(64)]
        public string Issuer { get; set; }

        /// <summary>
        /// Group ID for tokens belonging to the same group
        /// </summary>
        [MaxLength(36)]
        public string GroupId { get; set; }

        /// <summary>
        /// Is this token valid for charging?
        /// </summary>
        public bool Valid { get; set; } = true;

        /// <summary>
        /// Whitelist type (ALLOWED, ALLOWED_OFFLINE, etc.)
        /// </summary>
        [MaxLength(50)]
        public string Whitelist { get; set; }

        /// <summary>
        /// Language code for user interface
        /// </summary>
        [MaxLength(2)]
        public string Language { get; set; }

        /// <summary>
        /// Default profile type for the token
        /// </summary>
        [MaxLength(50)]
        public string DefaultProfileType { get; set; }

        /// <summary>
        /// Maximum kWh allowed for this token
        /// </summary>
        public decimal? EnergyContract { get; set; }

        /// <summary>
        /// Partner credential ID (foreign key)
        /// </summary>
        public int PartnerCredentialId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this token last updated by the partner
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
