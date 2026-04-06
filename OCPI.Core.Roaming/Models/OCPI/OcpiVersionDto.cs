using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Version DTO
    /// </summary>
    public class OcpiVersionDto
    {
        [Required]
        public string Version { get; set; }

        [Required]
        public string Url { get; set; }
    }

    /// <summary>
    /// OCPI Version Details
    /// </summary>
    public class OcpiVersionDetailsDto
    {
        [Required]
        public string Version { get; set; }

        [Required]
        public List<OcpiEndpointDto> Endpoints { get; set; }
    }

    /// <summary>
    /// OCPI Endpoint DTO
    /// </summary>
    public class OcpiEndpointDto
    {
        [Required]
        public string Identifier { get; set; }

        [Required]
        public string Role { get; set; } // SENDER, RECEIVER

        [Required]
        public string Url { get; set; }
    }

    /// <summary>
    /// OCPI Response Wrapper
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OcpiResponseDto<T>
    {
        [Required]
        public int StatusCode { get; set; }

        public string StatusMessage { get; set; }

        public T Data { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// OCPI Paginated Response
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OcpiPagedResponseDto<T>
    {
        [Required]
        public int StatusCode { get; set; }

        public string StatusMessage { get; set; }

        public List<T> Data { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        public int? Limit { get; set; }
        public int? Offset { get; set; }
        public int? TotalCount { get; set; }

        public string NextPageUrl { get; set; }
    }

    /// <summary>
    /// Generic OCPI Request
    /// </summary>
    public class OcpiRequestDto
    {
        public string CountryCode { get; set; }
        public string PartyId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int? Offset { get; set; }
        public int? Limit { get; set; }
    }
}
