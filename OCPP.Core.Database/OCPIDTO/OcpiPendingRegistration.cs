using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Represents a pre-generated A-token that allows a new partner to bootstrap
    /// the OCPI credentials handshake.  Once the partner POSTs to /2.2.1/credentials
    /// using this token, we generate a permanent B-token for them and mark this record
    /// as used.
    /// </summary>
    public class OcpiPendingRegistration
    {
        [Key]
        public int Id { get; set; }

        /// <summary>The A-token issued to the partner out-of-band (e.g. via email).</summary>
        [Required]
        [MaxLength(255)]
        public string AToken { get; set; }

        /// <summary>Admin-assigned label to identify which partner this token was created for.</summary>
        [MaxLength(200)]
        public string Label { get; set; }

        /// <summary>UTC time after which the A-token is no longer accepted.</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>UTC timestamp when this record was created.</summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>True once the partner has completed the handshake with this token.</summary>
        public bool IsUsed { get; set; } = false;

        /// <summary>UTC timestamp when the partner used the token (null if unused).</summary>
        public DateTime? UsedOn { get; set; }

        /// <summary>
        /// FK to the OcpiPartnerCredential record that was created when the token was redeemed.
        /// Null while the token is still pending.
        /// </summary>
        public int? PartnerCredentialId { get; set; }
    }
}
