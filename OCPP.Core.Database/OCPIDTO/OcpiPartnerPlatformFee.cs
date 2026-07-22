using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Per-partner platform fee charged by HyCharge on top of the partner CPO's own session
    /// cost for OCPI roaming sessions, expressed per unit of energy delivered (kWh).
    /// </summary>
    public class OcpiPartnerPlatformFee
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Partner this fee configuration applies to.</summary>
        public int PartnerCredentialId { get; set; }

        /// <summary>Platform fee in INR per kWh delivered. Zero means no fee is charged.</summary>
        public decimal FeePerKwh { get; set; }

        /// <summary>When false, the fee is treated as unset (0) even if a row exists.</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
