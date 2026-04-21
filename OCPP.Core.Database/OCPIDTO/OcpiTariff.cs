using System;
using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Tariff structure for pricing charging sessions
    /// </summary>
    public class OcpiTariff
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Country code of the CPO
        /// </summary>
        [Required]
        [MaxLength(2)]
        public string CountryCode { get; set; }

        /// <summary>
        /// Party ID of the CPO
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string PartyId { get; set; }

        /// <summary>
        /// Unique tariff identifier
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string TariffId { get; set; }

        /// <summary>
        /// Currency code (ISO 4217)
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string Currency { get; set; }

        /// <summary>
        /// Type of the tariff (AD_HOC_PAYMENT, PROFILE_GREEN, etc.)
        /// </summary>
        [MaxLength(50)]
        public string Type { get; set; }

        /// <summary>
        /// Tariff elements stored as JSON
        /// </summary>
        public string ElementsJson { get; set; }

        /// <summary>
        /// Energy component price per kWh
        /// </summary>
        public decimal? EnergyPrice { get; set; }

        /// <summary>
        /// Time component price per minute
        /// </summary>
        public decimal? TimePrice { get; set; }

        /// <summary>
        /// Flat fee for starting a session
        /// </summary>
        public decimal? SessionFee { get; set; }

        /// <summary>
        /// Is this tariff currently active?
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Start date and time of the tariff validity
        /// </summary>
        public DateTime? StartDateTime { get; set; }

        /// <summary>
        /// End date and time of the tariff validity
        /// </summary>
        public DateTime? EndDateTime { get; set; }

        /// <summary>
        /// Minimum kWh to be billed
        /// </summary>
        public decimal? MinKwh { get; set; }

        /// <summary>
        /// Maximum kWh to be billed
        /// </summary>
        public decimal? MaxKwh { get; set; }

        /// <summary>
        /// When was this record created locally
        /// </summary>
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When was this tariff last updated
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
