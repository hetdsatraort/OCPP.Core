using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Stores credentials for OCPI partner platforms
    /// </summary>
    public class OcpiPartnerCredential
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Partner's token for authentication
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Token { get; set; }

        /// <summary>
        /// Partner's versions endpoint URL
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Url { get; set; }

        /// <summary>
        /// Partner's country code
        /// </summary>
        [Required]
        [MaxLength(2)]
        public string CountryCode { get; set; }

        /// <summary>
        /// Partner's party ID (CPO or EMSP identifier)
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string PartyId { get; set; }

        /// <summary>
        /// Partner's business name
        /// </summary>
        [MaxLength(200)]
        public string BusinessName { get; set; }

        /// <summary>
        /// Partner's role (CPO, EMSP, etc.)
        /// </summary>
        [MaxLength(50)]
        public string Role { get; set; }

        /// <summary>
        /// OCPI version (e.g., "2.2.1")
        /// </summary>
        [MaxLength(10)]
        public string Version { get; set; }

        /// <summary>
        /// Is this credential currently active?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// When was the credential created
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was the credential last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last successful sync with this partner
        /// </summary>
        public DateTime? LastSyncOn { get; set; }
    }
}
