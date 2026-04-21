using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Stores location data received from OCPI partners
    /// </summary>
    public class OcpiPartnerLocation
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
        /// Uniqueness identifier for the location
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string LocationId { get; set; }

        /// <summary>
        /// Display name of the location
        /// </summary>
        [MaxLength(255)]
        public string Name { get; set; }

        /// <summary>
        /// Street/block name and house number
        /// </summary>
        [MaxLength(500)]
        public string Address { get; set; }

        /// <summary>
        /// City or town
        /// </summary>
        [MaxLength(100)]
        public string City { get; set; }

        /// <summary>
        /// Postal code
        /// </summary>
        [MaxLength(20)]
        public string PostalCode { get; set; }

        /// <summary>
        /// Country (ISO 3166-1 alpha-3)
        /// </summary>
        [MaxLength(3)]
        public string Country { get; set; }

        /// <summary>
        /// Latitude in decimal degrees
        /// </summary>
        [MaxLength(20)]
        public string Latitude { get; set; }

        /// <summary>
        /// Longitude in decimal degrees
        /// </summary>
        [MaxLength(20)]
        public string Longitude { get; set; }

        /// <summary>
        /// Type of the location (ON_STREET, PARKING_LOT, etc.)
        /// </summary>
        [MaxLength(50)]
        public string LocationType { get; set; }

        /// <summary>
        /// Partner credential ID (foreign key)
        /// </summary>
        public int PartnerCredentialId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this location last updated by the partner
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
