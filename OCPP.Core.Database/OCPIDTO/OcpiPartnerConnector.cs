using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Stores connector data from OCPI partners
    /// </summary>
    public class OcpiPartnerConnector
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Identifier of the connector within the EVSE
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string ConnectorId { get; set; }

        /// <summary>
        /// Connector standard (IEC_62196_T2, CHADEMO, etc.)
        /// </summary>
        [MaxLength(50)]
        public string Standard { get; set; }

        /// <summary>
        /// Connector format (SOCKET, CABLE)
        /// </summary>
        [MaxLength(20)]
        public string Format { get; set; }

        /// <summary>
        /// Power type (AC_1_PHASE, AC_3_PHASE, DC)
        /// </summary>
        [MaxLength(50)]
        public string PowerType { get; set; }

        /// <summary>
        /// Maximum voltage in Volt
        /// </summary>
        public int? MaxVoltage { get; set; }

        /// <summary>
        /// Maximum amperage in Ampere
        /// </summary>
        public int? MaxAmperage { get; set; }

        /// <summary>
        /// Maximum electric power in Watts
        /// </summary>
        public int? MaxElectricPower { get; set; }

        /// <summary>
        /// Foreign key to the EVSE
        /// </summary>
        public int PartnerEvseId { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this connector last updated by the partner
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation property
        public virtual OcpiPartnerEvse PartnerEvse { get; set; }
    }
}
