using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    /// <summary>
    /// Admin edit of another user's profile. Unlike UpdateProfileRequestDto this is a full
    /// replacement: optional fields sent as null/empty are cleared.
    /// </summary>
    public class AdminUpdateUserRequestDto
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [StringLength(100)]
        public string LastName { get; set; }

        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(200)]
        public string EMailID { get; set; }

        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(10)]
        public string CountryCode { get; set; }

        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string PinCode { get; set; }
    }

    public class AdminUpdateRoleRequestDto
    {
        [Required]
        public string Role { get; set; }
    }
}
