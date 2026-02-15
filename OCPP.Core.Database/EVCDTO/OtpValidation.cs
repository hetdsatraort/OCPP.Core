using System;

namespace OCPP.Core.Database.EVCDTO
{
    /// <summary>
    /// OTP Validation entity for SMS-based authentication
    /// </summary>
    public class OtpValidation
    {
        /// <summary>
        /// Unique record ID
        /// </summary>
        public string RecId { get; set; }

        /// <summary>
        /// Unique Auth ID for this OTP session
        /// </summary>
        public string AuthId { get; set; }

        /// <summary>
        /// Phone number with country code (e.g., +919876543210)
        /// </summary>
        public string PhoneNumber { get; set; }

        /// <summary>
        /// Country code (e.g., +91)
        /// </summary>
        public string CountryCode { get; set; }

        /// <summary>
        /// OTP code (6 digits)
        /// </summary>
        public string OtpCode { get; set; }

        /// <summary>
        /// OTP creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// OTP expiration timestamp (typically 5-10 minutes)
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Whether the OTP has been verified
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Timestamp when OTP was verified
        /// </summary>
        public DateTime? VerifiedAt { get; set; }

        /// <summary>
        /// Number of verification attempts
        /// </summary>
        public int AttemptCount { get; set; }

        /// <summary>
        /// IP address from which OTP was requested
        /// </summary>
        public string RequestIp { get; set; }

        /// <summary>
        /// IP address from which OTP was verified
        /// </summary>
        public string VerifyIp { get; set; }

        /// <summary>
        /// User ID if linked to existing user
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Purpose of OTP (Login, Registration, PasswordReset, etc.)
        /// </summary>
        public string Purpose { get; set; }

        /// <summary>
        /// Whether OTP is still active (not expired and not verified)
        /// </summary>
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        /// <summary>
        /// Whether OTP is still valid for verification
        /// </summary>
        public bool IsActive => !IsVerified && !IsExpired && AttemptCount < 5;
    }
}
