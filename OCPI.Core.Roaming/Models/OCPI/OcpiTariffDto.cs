using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Tariff DTO - Pricing structure
    /// </summary>
    public class OcpiTariffDto
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string Currency { get; set; }

        public string Type { get; set; } // PROFILE_GREEN, PROFILE_CHEAP, etc.

        public List<DisplayText> TariffAltText { get; set; }

        public string TariffAltUrl { get; set; }

        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        [Required]
        public List<TariffElement> Elements { get; set; }

        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }

        public EnergyMix EnergyMix { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Tariff Element
    /// </summary>
    public class TariffElement
    {
        [Required]
        public List<PriceComponent> PriceComponents { get; set; }

        public TariffRestrictions Restrictions { get; set; }
    }

    /// <summary>
    /// Price Component
    /// </summary>
    public class PriceComponent
    {
        [Required]
        public string Type { get; set; } // ENERGY, TIME, FLAT, PARKING_TIME

        [Required]
        public decimal Price { get; set; }

        public decimal? Vat { get; set; }

        public int? StepSize { get; set; }
    }

    /// <summary>
    /// Tariff Restrictions
    /// </summary>
    public class TariffRestrictions
    {
        public string StartTime { get; set; } // HH:MM
        public string EndTime { get; set; } // HH:MM

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public decimal? MinKWh { get; set; }
        public decimal? MaxKWh { get; set; }

        public decimal? MinCurrent { get; set; }
        public decimal? MaxCurrent { get; set; }

        public decimal? MinPower { get; set; }
        public decimal? MaxPower { get; set; }

        public int? MinDuration { get; set; }
        public int? MaxDuration { get; set; }

        public List<string> DayOfWeek { get; set; } // MONDAY, TUESDAY, etc.

        public string Reservation { get; set; }
    }

    /// <summary>
    /// Display Text for multilingual support
    /// </summary>
    public class DisplayText
    {
        [Required]
        public string Language { get; set; }

        [Required]
        public string Text { get; set; }
    }
}
