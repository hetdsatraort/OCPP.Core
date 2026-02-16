using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class ForgotPasswordRequestDto
    {
        [Required(ErrorMessage = "Email or Phone is required")]
        public string EmailOrPhone { get; set; }

        [Required(ErrorMessage = "OTP code is required")]
        public string OtpCode { get; set; }

        [Required(ErrorMessage = "Auth ID is required")]
        public string AuthId { get; set; }

        [Required(ErrorMessage = "New password is required")]
        [StringLength(200, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "Confirm password is required")]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation password do not match")]
        public string ConfirmPassword { get; set; }
    }
}
