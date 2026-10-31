using System;
using System.Collections.Generic;

namespace OCPP.Core.Management.Models.Payment
{
    public class RazorpayKeyResponseDto
    {
        public string Key { get; set; }
    }

    public class CreateOrderRequestDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Receipt { get; set; }
        public Dictionary<string, string> Notes { get; set; }
    }

    public class CreateOrderResponseDto
    {
        public string Id { get; set; }
        public string Entity { get; set; }
        public long Amount { get; set; }
        public long AmountPaid { get; set; }
        public long AmountDue { get; set; }
        public string Currency { get; set; }
        public string Receipt { get; set; }
        public string Status { get; set; }
        public int Attempts { get; set; }
        public Dictionary<string, string> Notes { get; set; }
        public long CreatedAt { get; set; }
    }

    public class VerifyPaymentRequestDto
    {
        public string RazorpayOrderId { get; set; }
        public string RazorpayPaymentId { get; set; }
        public string RazorpaySignature { get; set; }
    }

    public class VerifyPaymentResponseDto
    {
        public string Status { get; set; }
        public string Message { get; set; }
    }

    public class OrderDto
    {
        public string OrderId { get; set; }
        public long Amount { get; set; }
        public string Currency { get; set; }
        public string Receipt { get; set; }
        public string Status { get; set; }
        public string PaymentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class PaymentResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class HealthCheckResponseDto
    {
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AddCreditsRequestDto
    {
        public string UserId { get; set; }
        public string OrderId { get; set; }
        public string PaymentId { get; set; }
        public string PaymentSignature { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }

    public class AddCreditsResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public decimal NewBalance { get; set; }
        public string ValidationId { get; set; }
        public string TransactionId { get; set; }
        /// <summary>True when this order had already been credited before this request (idempotent replay)</summary>
        public bool AlreadyProcessed { get; set; }
    }

    public class WalletTopUpRequestDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        /// <summary>Where Razorpay redirects the user after payment</summary>
        public string CallbackUrl { get; set; }
    }

    /// <summary>Hosted wallet top-up (Express payment link or Premier checkout session) as returned by the payment gateway</summary>
    public class WalletTopUpDto
    {
        public string Id { get; set; }
        /// <summary>Express Checkout: Razorpay payment link URL</summary>
        public string ShortUrl { get; set; }
        /// <summary>Premier Checkout: branded checkout page URL</summary>
        public string CheckoutUrl { get; set; }
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string UserId { get; set; }
        public string OrderId { get; set; }
        public string PaymentId { get; set; }
        public DateTime? ExpireBy { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class WalletTopUpStatusDto
    {
        public string Id { get; set; }
        /// <summary>created, paid, expired, cancelled (or partially_paid for links)</summary>
        public string Status { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        /// <summary>True once the wallet has been credited for this top-up</summary>
        public bool Credited { get; set; }
        public decimal? NewBalance { get; set; }
    }

    public class InternalWebhookPayloadDto
    {
        public string Event { get; set; }
        public string OrderId { get; set; }
        public string PaymentId { get; set; }
        public string UserId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
    }
}
