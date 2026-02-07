using System;
using System.Collections.Generic;

namespace OCPP.Core.Management.Models.Auth
{
    public class WalletDto
    {
        public string UserId { get; set; }
        public decimal CurrentBalance { get; set; }
        public int TotalTransactions { get; set; }
        public List<WalletTransactionDto> RecentTransactions { get; set; }
    }

    public class WalletTransactionDto
    {
        public string RecId { get; set; }
        public string PreviousCreditBalance { get; set; }
        public string CurrentCreditBalance { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; }
        public string PaymentRecId { get; set; }
        public string ChargingSessionId { get; set; }
        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public string AdditionalInfo3 { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
