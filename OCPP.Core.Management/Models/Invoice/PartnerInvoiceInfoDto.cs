using System;
using OCPP.Core.Database.OCPIDTO;

namespace OCPP.Core.Management.Models.Invoice
{
    public class PartnerInvoiceInfoDto
    {
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string SessionId { get; set; }
        public string PartnerName { get; set; }
        public string Currency { get; set; }
        public decimal EnergyConsumedKwh { get; set; }
        public decimal PartnerCost { get; set; }
        public decimal PricePerUnit { get; set; }
        public decimal TaxableValue { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal TotalPayable { get; set; }

        public static PartnerInvoiceInfoDto FromEntity(OcpiPartnerSessionInvoice invoice) => new PartnerInvoiceInfoDto
        {
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            SessionId = invoice.SessionId,
            PartnerName = invoice.PartnerName,
            Currency = invoice.Currency,
            EnergyConsumedKwh = invoice.EnergyConsumedKwh,
            PartnerCost = invoice.PartnerCost,
            PricePerUnit = invoice.PricePerUnit,
            TaxableValue = invoice.TaxableValue,
            CgstAmount = invoice.CgstAmount,
            SgstAmount = invoice.SgstAmount,
            GrandTotal = invoice.GrandTotal,
            TotalPayable = invoice.TotalPayable
        };
    }
}
