using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Credentials Request/Response DTO
    /// </summary>
    public class OcpiCredentialsRequestDto
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [Url]
        public string Url { get; set; }

        public BusinessDetails BusinessDetails { get; set; }
        public string CountryCode { get; set; }
        public string PartyId { get; set; }
    }

    /// <summary>
    /// Business Details for OCPI
    /// </summary>
    public class BusinessDetails
    {
        [Required]
        public string Name { get; set; }

        public string Website { get; set; }
        public Image Logo { get; set; }
    }

    /// <summary>
    /// Image details
    /// </summary>
    public class Image
    {
        [Required]
        [Url]
        public string Url { get; set; }

        public string Thumbnail { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    /// <summary>
    /// OCPI Credentials Response DTO
    /// </summary>
    public class OcpiCredentialsResponseDto
    {
        public string Token { get; set; }
        public string Url { get; set; }
        public BusinessDetails BusinessDetails { get; set; }
        public string CountryCode { get; set; }
        public string PartyId { get; set; }
    }
}
