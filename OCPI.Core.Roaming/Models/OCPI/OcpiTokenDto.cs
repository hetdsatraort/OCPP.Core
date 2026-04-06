using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Token DTO - Represents an authorization token
    /// </summary>
    public class OcpiTokenDto
    {
        [Required]
        public string CountryCode { get; set; }

        [Required]
        public string PartyId { get; set; }

        [Required]
        public string Uid { get; set; }

        [Required]
        public string Type { get; set; } // RFID, APP_USER, AD_HOC_USER, OTHER

        [Required]
        public string ContractId { get; set; }

        public string VisualNumber { get; set; }

        [Required]
        public string Issuer { get; set; }

        public string GroupId { get; set; }

        [Required]
        public bool Valid { get; set; }

        [Required]
        public string Whitelist { get; set; } // ALWAYS, ALLOWED, ALLOWED_OFFLINE, NEVER

        public string Language { get; set; }

        public string DefaultProfileType { get; set; }

        public EnergyContract EnergyContract { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Energy Contract details
    /// </summary>
    public class EnergyContract
    {
        [Required]
        public string SupplierName { get; set; }

        public string ContractId { get; set; }
    }

    /// <summary>
    /// Authorization Info
    /// </summary>
    public class AuthorizationInfo
    {
        [Required]
        public string Allowed { get; set; } // ALLOWED, BLOCKED, EXPIRED, etc.

        [Required]
        public OcpiLocationReference Location { get; set; }

        public AuthorizationReference Authorization { get; set; }

        public DisplayText Info { get; set; }
    }

    /// <summary>
    /// Location Reference for Authorization
    /// </summary>
    public class OcpiLocationReference
    {
        [Required]
        public string LocationId { get; set; }

        [Required]
        public List<string> EvseUids { get; set; }
    }

    /// <summary>
    /// Authorization Reference
    /// </summary>
    public class AuthorizationReference
    {
        [Required]
        [JsonPropertyName("AuthorizationReference")]
        public string authorizationReference { get; set; }
    }

    /// <summary>
    /// Token Authorization Request
    /// </summary>
    public class TokenAuthorizationRequestDto
    {
        [Required]
        public string TokenUid { get; set; }

        public string TokenType { get; set; }

        [Required]
        public string LocationId { get; set; }

        public List<string> EvseUids { get; set; }
    }
}
