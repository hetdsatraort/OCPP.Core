using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    /// <summary>
    /// Request to verify OTP
    /// </summary>
    public class VerifyOtpRequestDto
    {
        /// <summary>
        /// Auth ID received from SendOTP response
        /// </summary>
        [Required(ErrorMessage = "Auth ID is required")]
        public string AuthId { get; set; }

        /// <summary>
        /// OTP code (6 digits)
        /// </summary>
        [Required(ErrorMessage = "OTP code is required")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must be 6 digits")]
        public string OtpCode { get; set; }

        /// <summary>
        /// Phone number (for additional validation)
        /// </summary>
        [Required(ErrorMessage = "Phone number is required")]
        public string PhoneNumber { get; set; }
    }

    /// <summary>
    /// Response after verifying OTP
    /// </summary>
    public class VerifyOtpResponseDto
    {
        /// <summary>
        /// Whether OTP verification was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// JWT access token (if login was successful)
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// Refresh token for renewing access token
        /// </summary>
        public string RefreshToken { get; set; }

        /// <summary>
        /// User details if login successful
        /// </summary>
        public UserInfo User { get; set; }

        /// <summary>
        /// Whether this is a new user (registration required)
        /// </summary>
        public bool IsNewUser { get; set; }
    }

    /// <summary>
    /// Basic user information returned after OTP verification
    /// </summary>
    public class UserInfo
    {
        public string UserId { get; set; }
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string UserRole { get; set; }
        public bool ProfileCompleted { get; set; }
    }
}
