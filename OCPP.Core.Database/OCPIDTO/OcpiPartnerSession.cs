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
        /// Partner credential ID (foreign key) — identifies which CPO partner sent this session data.
        /// </summary>
        public int? PartnerCredentialId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this session last updated by the partner
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // ── App-side session tracking (eMSP role) ─────────────────────────────

        /// <summary>
        /// App user who initiated this session via our eMSP flow.
        /// Null for sessions not started from our app (e.g. received via partner sync only).
        /// Links to EVCDTO.Users.RecId.
        /// </summary>
        [MaxLength(250)]
        public string? UserId { get; set; }

        /// <summary>Maximum energy to deliver in kWh. Null = no limit.</summary>
        public double? EnergyLimit { get; set; }

        /// <summary>Maximum cost in session currency. Null = no limit.</summary>
        public double? CostLimit { get; set; }

        /// <summary>Maximum session duration in minutes. Null = no limit.</summary>
        public int? TimeLimit { get; set; }

        /// <summary>Maximum battery percentage increase (0–100). Null = no limit.</summary>
        public double? BatteryIncreaseLimit { get; set; }

        /// <summary>
        /// Set to true once a limit-violation debit has been recorded for this session
        /// to prevent double-billing when the partner later sends the final COMPLETED status.
        /// </summary>
        public bool LimitViolationHandled { get; set; } = false;

        /// <summary>
        /// Latest EV state of charge (0–100%) reported by the partner CPO, extracted from the
        /// STATE_OF_CHARGE dimension of the most recent charging_period on the session. Not all
        /// CPOs report this — typically only DC fast chargers do. Null when never reported.
        /// </summary>
        public decimal? CurrentStateOfCharge { get; set; }

        /// <summary>When CurrentStateOfCharge was last updated by the partner.</summary>
        public DateTime? StateOfChargeLastUpdate { get; set; }

        // Navigation property
        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
