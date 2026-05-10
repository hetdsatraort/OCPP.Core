using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Stores charging sessions hosted at OUR charging stations initiated by eMSP partners (CPO role).
    /// These sessions are created when an OCPI START_SESSION command is received from a partner eMSP,
    /// or when a roaming token is authorized at one of our chargepoints.
    ///
    /// Contrast with <see cref="OcpiPartnerSession"/>, which records sessions our users do at
    /// third-party CPO stations (eMSP role), received via PUT/PATCH from partner CPOs.
    /// </summary>
    public class OcpiHostedSession
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Unique OCPI session ID we generated (GUID string).
        /// This is the ID we expose to eMSP partners via the Sender interface.
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string SessionId { get; set; }

        /// <summary>
        /// The OCPP Transaction ID on our chargepoint that corresponds to this session.
        /// Null until the chargepoint fires a StartTransaction confirmation.
        /// </summary>
        public int? TransactionId { get; set; }

        /// <summary>
        /// Our OCPP chargepoint identifier (ChargePoint.ChargePointId).
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string ChargePointId { get; set; }

        /// <summary>
        /// OCPP connector number on the chargepoint.
        /// </summary>
        public int ConnectorNumber { get; set; }

        /// <summary>
        /// Our EVSE identifier — references ChargingStation.RecId.
        /// Used to look up location and EVSE info when serving sessions to eMSPs.
        /// </summary>
        [MaxLength(36)]
        public string EvseUid { get; set; }

        /// <summary>
        /// Our connector identifier — references ChargingGuns.RecId.
        /// </summary>
        [MaxLength(36)]
        public string ConnectorId { get; set; }

        /// <summary>
        /// The token UID used to authorise the session (eMSP RFID / app token).
        /// </summary>
        [MaxLength(36)]
        public string TokenUid { get; set; }

        /// <summary>
        /// Location (Hub) ID — references ChargingHub.RecId or OCPI LocationId.
        /// </summary>
        [MaxLength(36)]
        public string LocationId { get; set; }

        /// <summary>
        /// The partner credential (eMSP) whose token was used.
        /// Null when the session was started via a local admin command.
        /// </summary>
        public int? PartnerCredentialId { get; set; }

        /// <summary>Session start timestamp.</summary>
        public DateTime StartDateTime { get; set; }

        /// <summary>Session end timestamp. Null while the session is still active.</summary>
        public DateTime? EndDateTime { get; set; }

        /// <summary>Total consumed energy in kWh.</summary>
        public decimal? TotalEnergy { get; set; }

        /// <summary>Session status: ACTIVE, COMPLETED, INVALID.</summary>
        [MaxLength(50)]
        public string Status { get; set; }

        /// <summary>Total cost of the session (excl. VAT).</summary>
        public decimal? TotalCost { get; set; }

        /// <summary>Currency code (ISO 4217).</summary>
        [MaxLength(3)]
        public string Currency { get; set; }

        /// <summary>When this record was created locally.</summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last updated.</summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerCredential PartnerCredential { get; set; }
    }
}
