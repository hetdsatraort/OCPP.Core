using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ChargingSession
{
    /// <summary>
    /// Request DTO for charging estimation
    /// </summary>
    public class ChargingEstimationRequestDto
    {
        /// <summary>
        /// Charging gun/connector ID to estimate for
        /// </summary>
        [Required]
        public string ChargingGunId { get; set; }

        /// <summary>
        /// Charging station ID
        /// </summary>
        [Required]
        public string ChargingStationId { get; set; }

        /// <summary>
        /// Connector ID
        /// </summary>
        [Required]
        public string ConnectorId { get; set; }

        /// <summary>
        /// Optional: User's battery capacity in kWh (if known)
        /// If not provided, we'll use default assumptions
        /// </summary>
        public double? BatteryCapacity { get; set; }

        /// <summary>
        /// Optional: Desired energy to charge in kWh
        /// If not provided, we'll estimate for full charging duration
        /// </summary>
        public double? DesiredEnergy { get; set; }

        /// <summary>
        /// Optional: Desired charging duration in minutes
        /// If not provided, we'll estimate based on energy or default duration
        /// </summary>
        public int? DesiredDuration { get; set; }

        /// <summary>
        /// Optional: Current battery percentage (0-100)
        /// Used for more accurate battery increase calculation
        /// </summary>
        public double? CurrentBatteryPercentage { get; set; }

        /// <summary>
        /// Optional: Desired cost/budget to spend (in currency units)
        /// If provided, estimation will calculate what you can get for this amount
        /// </summary>
        public double? DesiredCost { get; set; }
    }
}
