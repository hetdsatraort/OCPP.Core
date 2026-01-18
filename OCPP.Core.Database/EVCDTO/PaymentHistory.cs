using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OCPP.Core.Database.EVCDTO
{
    public class PaymentHistory
    {
        public string RecId { get; set; }
        public string TransactionType { get; set; }
        public string UserId { get; set; }
        public string ChargingStationId { get; set; }
        public TimeSpan SessionDuration { get; set; }
        public string PaymentMethod { get; set; }
        public string AdditionalInfo1 { get; set; }
        public string AdditionalInfo2 { get; set; }
        public string AdditionalInfo3 { get; set; }
        public string OrderId { get; set; }
        public string PaymentId { get; set; }
        public string UserRemarks { get; set; }
        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }

    }
}
