using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    /// <summary>
    /// Payment Validation entity to ensure secure payment processing
    /// Tracks payment verification status and prevents duplicate processing
    /// </summary>
    public class PaymentValidation
    {
        /// <summary>
        /// Unique identifier for this validation record
        /// </summary>
        public string RecId { get; set; }

        /// <summary>
        /// User ID who initiated the payment
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Razorpay Order ID
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// Razorpay Payment ID
        /// </summary>
        public string PaymentId { get; set; }

        /// <summary>
        /// Payment signature for verification
        /// </summary>
        public string PaymentSignature { get; set; }

        /// <summary>
        /// Amount in smallest currency unit (paise for INR)
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// Currency code (e.g., INR, USD)
        /// </summary>
        public string Currency { get; set; }

        /// <summary>
        /// Payment status: Pending, Verified, Failed, Processed, Refunded
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Payment method used
        /// </summary>
        public string PaymentMethod { get; set; }

        /// <summary>
        /// IP address of the user making payment
        /// </summary>
        public string IpAddress { get; set; }

        /// <summary>
        /// User agent/device information
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Verification timestamp
        /// </summary>
        public DateTime? VerifiedAt { get; set; }

        /// <summary>
        /// Processing timestamp when credits were added
        /// </summary>
        public DateTime? ProcessedAt { get; set; }

        /// <summary>
        /// Reference to PaymentHistory RecId
        /// </summary>
        public string PaymentHistoryId { get; set; }

        /// <summary>
        /// Reference to WalletTransactionLog RecId
        /// </summary>
        public string WalletTransactionId { get; set; }

        /// <summary>
        /// Verification result message
        /// </summary>
        public string VerificationMessage { get; set; }

        /// <summary>
        /// Hash of payment details for tamper detection
        /// </summary>
        public string SecurityHash { get; set; }

        /// <summary>
        /// Number of verification attempts
        /// </summary>
        public int VerificationAttempts { get; set; }

        /// <summary>
        /// Reason for failure if status is Failed
        /// </summary>
        public string FailureReason { get; set; }

        /// <summary>
        /// Additional metadata as JSON
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Additional information field 1
        /// </summary>
        public string AdditionalInfo1 { get; set; }

        /// <summary>
        /// Additional information field 2
        /// </summary>
        public string AdditionalInfo2 { get; set; }

        /// <summary>
        /// Additional information field 3
        /// </summary>
        public string AdditionalInfo3 { get; set; }

        /// <summary>
        /// Active flag: 1 = Active, 0 = Inactive/Deleted
        /// </summary>
        public int Active { get; set; }

        /// <summary>
        /// Record creation timestamp
        /// </summary>
        public DateTime CreatedOn { get; set; }

        /// <summary>
        /// Record last update timestamp
        /// </summary>
        public DateTime UpdatedOn { get; set; }
    }
}
