using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class RegisterRequestDto
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(200)]
        public string EMailID { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(10)]
        public string CountryCode { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("Password", ErrorMessage = "Password and confirmation password do not match")]
        public string ConfirmPassword { get; set; }

        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string AddressLine3 { get; set; }
        public string State { get; set; }
        public string City { get; set; }
        public string PinCode { get; set; }
    }
}
