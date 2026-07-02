using System;
using OCPP.Core.Database.EVCDTO;

namespace OCPP.Core.Management.Models.Invoice
{
    public class InvoiceInfoDto
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string ChargingSessionId { get; set; }
        public decimal TaxableValue { get; set; }
        public decimal Discount { get; set; }
        public decimal Cashback { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal GrandTotal { get; set; }

        public static InvoiceInfoDto FromEntity(SessionInvoice invoice) => new InvoiceInfoDto
        {
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            ChargingSessionId = invoice.ChargingSessionId,
            TaxableValue = invoice.TaxableValue,
            Discount = invoice.Discount,
            Cashback = invoice.Cashback,
            CgstAmount = invoice.CgstAmount,
            SgstAmount = invoice.SgstAmount,
            GrandTotal = invoice.GrandTotal
        };
    }
}
