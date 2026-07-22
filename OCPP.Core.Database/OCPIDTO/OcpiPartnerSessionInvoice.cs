using System;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// Tax invoice for HyCharge's own platform fee on an OCPI roaming (partner) session.
    /// Distinct from <see cref="OCPP.Core.Database.EVCDTO.SessionInvoice"/>, which invoices
    /// energy sold directly by HyCharge on local OCPP sessions — here the partner CPO sells the
    /// energy and bills its own cost/tax (<see cref="PartnerCost"/>, informational only), while
    /// this invoice covers only the platform/facilitation fee HyCharge charges on top, which is
    /// what CGST/SGST is computed against.
    /// </summary>
    public class OcpiPartnerSessionInvoice
    {
        public string RecId { get; set; }
        public string InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public int OcpiPartnerSessionId { get; set; }
        public string SessionId { get; set; }
        public string UserId { get; set; }
        public int? PartnerCredentialId { get; set; }
        public string PartnerName { get; set; }

        // Billed-to snapshot
        public string BilledToName { get; set; }
        public string BilledToPhone { get; set; }
        public string BilledToEmail { get; set; }

        // Session detail snapshot
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal EnergyConsumedKwh { get; set; }
        public string Currency { get; set; }

        /// <summary>Partner CPO's own reported session cost — informational, not taxed by us.</summary>
        public decimal PartnerCost { get; set; }

        // Platform fee line item
        public string Description { get; set; }
        public string SacCode { get; set; }
        public decimal PricePerUnit { get; set; }

        // Amounts (all pertain to the platform fee only)
        public decimal TaxableValue { get; set; }
        public decimal CgstRate { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstRate { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal GrandTotal { get; set; }

        /// <summary>PartnerCost + GrandTotal — the total amount actually debited from the user's wallet.</summary>
        public decimal TotalPayable { get; set; }

        public int Active { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime UpdatedOn { get; set; }
    }
}
