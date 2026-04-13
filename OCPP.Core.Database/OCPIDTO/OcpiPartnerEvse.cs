using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Stores EVSE (Electric Vehicle Supply Equipment) data from OCPI partners
    /// </summary>
    public class OcpiPartnerEvse
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Unique identifier for the EVSE
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string EvseUid { get; set; }

        /// <summary>
        /// EVSE ID (e.g., "IN*CPO*E12345")
        /// </summary>
        [MaxLength(48)]
        public string EvseId { get; set; }

        /// <summary>
        /// Status of the EVSE (AVAILABLE, BLOCKED, CHARGING, etc.)
        /// </summary>
        [MaxLength(50)]
        public string Status { get; set; }

        /// <summary>
        /// Status timestamp
        /// </summary>
        public DateTime? StatusDateTime { get; set; }

        /// <summary>
        /// Floor level on which the EVSE is located
        /// </summary>
        [MaxLength(10)]
        public string FloorLevel { get; set; }

        /// <summary>
        /// Physical reference for the EVSE
        /// </summary>
        [MaxLength(50)]
        public string PhysicalReference { get; set; }

        /// <summary>
        /// Foreign key to the location
        /// </summary>
        public int PartnerLocationId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this EVSE last updated by the partner
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerLocation PartnerLocation { get; set; }
    }
}
