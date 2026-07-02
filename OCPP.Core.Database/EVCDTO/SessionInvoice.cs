using System;

namespace OCPP.Core.Database.EVCDTO
{
    public class SessionInvoice
    {
        public string RecId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string ChargingSessionId { get; set; }
        public string UserId { get; set; }

        // Billed-to snapshot (frozen at first generation, independent of later profile edits)
        public string BilledToName { get; set; }
        public string BilledToPhone { get; set; }
        public string BilledToEmail { get; set; }

        // Station info snapshot
        public string ChargingHubName { get; set; }
        public string ChargePointId { get; set; }
        public string ChargerType { get; set; }
        public string City { get; set; }
        public string ConnectorId { get; set; }
        public string PowerOutput { get; set; }

        // Session detail snapshot
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public decimal EnergyConsumedKwh { get; set; }

        // Charging line item
        public string Description { get; set; }
        public string SacCode { get; set; }
        public decimal PricePerUnit { get; set; }

        // Amounts
        public decimal TaxableValue { get; set; }
        public decimal Discount { get; set; }
        public decimal Cashback { get; set; }
        public decimal CgstRate { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstRate { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal GrandTotal { get; set; }

        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
