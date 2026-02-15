namespace OCPP.Core.Management.Models.ChargingSession
{
    /// <summary>
    /// Response DTO for charging estimation
    /// </summary>
    public class ChargingEstimationResponseDto
    {
        /// <summary>
        /// Whether the estimation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing any issues or additional info
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Estimated energy consumption in kWh
        /// </summary>
        public double EstimatedEnergy { get; set; }

        /// <summary>
        /// Estimated cost in ₹ (Indian Rupees)
        /// </summary>
        public double EstimatedCost { get; set; }

        /// <summary>
        /// Estimated cost with 18% GST included
        /// </summary>
        public double EstimatedCostWithTax { get; set; }

        /// <summary>
        /// Estimated charging time in minutes
        /// </summary>
        public double EstimatedTimeMinutes { get; set; }

        /// <summary>
        /// Estimated charging time in hours (for display)
        /// </summary>
        public double EstimatedTimeHours { get; set; }

        /// <summary>
        /// Estimated kilometres/range that can be added
        /// Based on average efficiency of 4.5 km/kWh
        /// </summary>
        public double EstimatedKilometres { get; set; }

        /// <summary>
        /// Estimated battery increase in percentage
        /// </summary>
        public double EstimatedBatteryIncrease { get; set; }

        /// <summary>
        /// Details about the charger used for estimation
        /// </summary>
        public ChargerDetails Charger { get; set; }

        /// <summary>
        /// Car assumptions used for estimation
        /// </summary>
        public CarAssumptions Car { get; set; }

        /// <summary>
        /// Breakdown of cost calculation
        /// </summary>
        public CostBreakdown CostDetails { get; set; }
    }

    /// <summary>
    /// Charger details used in estimation
    /// </summary>
    public class ChargerDetails
    {
        /// <summary>
        /// Charger power output in kW
        /// </summary>
        public double PowerOutput { get; set; }

        /// <summary>
        /// Charging tariff in ₹/kWh
        /// </summary>
        public double Tariff { get; set; }

        /// <summary>
        /// Charger type (AC/DC, Type 2, CCS, etc.)
        /// </summary>
        public string ChargerType { get; set; }

        /// <summary>
        /// Connector ID
        /// </summary>
        public string ConnectorId { get; set; }
    }

    /// <summary>
    /// Car assumptions used for estimation
    /// </summary>
    public class CarAssumptions
    {
        /// <summary>
        /// Battery capacity in kWh
        /// Default: 40 kWh (common for electric vehicles in India)
        /// </summary>
        public double BatteryCapacity { get; set; }

        /// <summary>
        /// Average efficiency in km/kWh
        /// Default: 4.5 km/kWh (realistic for Indian EVs)
        /// </summary>
        public double Efficiency { get; set; }

        /// <summary>
        /// Current battery percentage (if provided)
        /// </summary>
        public double? CurrentBatteryPercentage { get; set; }

        /// <summary>
        /// Charging efficiency (typically 90-95% for AC, 85-90% for DC)
        /// </summary>
        public double ChargingEfficiency { get; set; }
    }

    /// <summary>
    /// Cost breakdown details
    /// </summary>
    public class CostBreakdown
    {
        /// <summary>
        /// Base energy cost (before tax)
        /// </summary>
        public double EnergyCost { get; set; }

        /// <summary>
        /// GST amount (18% of energy cost)
        /// </summary>
        public double TaxAmount { get; set; }

        /// <summary>
        /// Total cost including tax
        /// </summary>
        public double TotalCost { get; set; }

        /// <summary>
        /// Cost per kilometre
        /// </summary>
        public double CostPerKm { get; set; }

        /// <summary>
        /// Tariff applied
        /// </summary>
        public double TariffApplied { get; set; }

        /// <summary>
        /// Currency symbol
        /// </summary>
        public string Currency { get; set; } = "₹";
    }
}
