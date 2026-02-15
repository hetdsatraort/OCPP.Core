using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    /// <summary>
    /// Request to send OTP to phone number
    /// </summary>
    public class SendOtpRequestDto
    {
        /// <summary>
        /// Phone number without country code (e.g., 9876543210)
        /// </summary>
        [Required(ErrorMessage = "Phone number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Phone number must be 10 digits")]
        public string PhoneNumber { get; set; }

        /// <summary>
        /// Country code (e.g., +91 for India)
        /// Default: +91
        /// </summary>
        public string CountryCode { get; set; } = "+91";

        /// <summary>
        /// Purpose of OTP: Login, Registration, PasswordReset
        /// Default: Login
        /// </summary>
        public string Purpose { get; set; } = "Login";
    }

    /// <summary>
    /// Response after sending OTP
    /// </summary>
    public class SendOtpResponseDto
    {
        /// <summary>
        /// Whether OTP was sent successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Message describing the result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Unique Auth ID for this OTP session
        /// Must be used for verification
        /// </summary>
        public string AuthId { get; set; }

        /// <summary>
        /// Masked phone number for display (e.g., +91-98765*****)
        /// </summary>
        public string MaskedPhoneNumber { get; set; }

        /// <summary>
        /// OTP expiration time in seconds (e.g., 300 for 5 minutes)
        /// </summary>
        public int ExpiresInSeconds { get; set; }
    }
}
