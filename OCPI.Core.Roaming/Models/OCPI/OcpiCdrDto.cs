using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI CDR (Charge Detail Record) DTO
    /// </summary>
    public class OcpiCdrDto
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        public string SessionId { get; set; }

        [Required]
        public OcpiCdrToken CdrToken { get; set; }

        [Required]
        public string AuthMethod { get; set; }

        public string AuthorizationReference { get; set; }

        [Required]
        public OcpiCdrLocation CdrLocation { get; set; }

        public string MeterId { get; set; }

        [Required]
        public string Currency { get; set; }

        public List<OcpiTariffDto> Tariffs { get; set; }

        [Required]
        public List<ChargingPeriod> ChargingPeriods { get; set; }

        public SignedData SignedData { get; set; }

        [Required]
        public decimal TotalCost { get; set; }

        public decimal? TotalFixedCost { get; set; }

        [Required]
        public decimal TotalEnergy { get; set; }

        public decimal? TotalEnergyCost { get; set; }

        [Required]
        public decimal TotalTime { get; set; }

        public decimal? TotalTimeCost { get; set; }

        public decimal? TotalParkingTime { get; set; }
        public decimal? TotalParkingCost { get; set; }

        public decimal? TotalReservationCost { get; set; }

        public string Remark { get; set; }

        public string InvoiceReferenceId { get; set; }

        public bool? Credit { get; set; }

        public string CreditReferenceId { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// CDR Location details
    /// </summary>
    public class OcpiCdrLocation
    {
        [Required]
        public string Id { get; set; }

        public string Name { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string City { get; set; }

        public string PostalCode { get; set; }

        public string State { get; set; }

        [Required]
        public string Country { get; set; }

        [Required]
        public GeoLocation Coordinates { get; set; }

        [Required]
        public string EvseUid { get; set; }

        [Required]
        public string EvseId { get; set; }

        [Required]
        public string ConnectorId { get; set; }

        [Required]
        public string ConnectorStandard { get; set; }

        [Required]
        public string ConnectorFormat { get; set; }

        [Required]
        public string ConnectorPowerType { get; set; }
    }

    /// <summary>
    /// Signed Data
    /// </summary>
    public class SignedData
    {
        [Required]
        public string EncodingMethod { get; set; }

        public int? EncodingMethodVersion { get; set; }

        public string PublicKey { get; set; }

        [Required]
        public List<SignedValue> SignedValues { get; set; }

        [Required]
        public string Url { get; set; }
    }

    /// <summary>
    /// Signed Value
    /// </summary>
    public class SignedValue
    {
        [Required]
        public string Nature { get; set; }

        [Required]
        public string PlainData { get; set; }

        [Required]
        public string SignedData { get; set; }
    }
}
