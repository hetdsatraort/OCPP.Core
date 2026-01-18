using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class UpdateProfileRequestDto
    {
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

        public string ProfileImageID { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string PinCode { get; set; }
    }
}
