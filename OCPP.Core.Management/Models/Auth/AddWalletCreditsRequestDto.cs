using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.Auth
{
    public class AddWalletCreditsRequestDto
    {
        [Required]
        public string UserId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        public string TransactionType { get; set; } // e.g., "Credit", "Debit", "Refund"

        public string PaymentRecId { get; set; }

        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public string AdditionalInfo3 { get; set; }
    }
}
