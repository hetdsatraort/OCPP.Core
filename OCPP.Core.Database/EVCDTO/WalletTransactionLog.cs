using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class WalletTransactionLog
    {
        public string RecId { get; set; }
        public string UserId { get; set; }
        public string PreviousCreditBalance { get; set; }
        public string CurrentCreditBalance { get; set; }
        public string TransactionType { get; set; }
        public string PaymentRecId { get; set; }
        public string ChargingSessionId { get; set; }
        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public string AdditionalInfo3 { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
