using System;

namespace OCPP.Core.Database.OCPIDTO
{
    /// <summary>
    /// The amount a user actually pays for an OCPI partner session: the partner CPO's own cost
    /// (excl. VAT) plus HyCharge's per-kWh platform fee, with CGST + SGST charged on that combined
    /// taxable value. Shared by PartnerInvoiceService (final invoice, in OCPP.Core.Management) and
    /// limit enforcement (OcpiOrphanSessionService, in OCPI.Core.Roaming) so a user's cost limit is
    /// checked against the same figure they are eventually billed.
    /// </summary>
    public static class PartnerSessionCharges
    {
        public const decimal CgstRate = 9m;
        public const decimal SgstRate = 9m;

        /// <summary>
        /// Partner cost + platform fee + GST, rounded the same way as the invoice.
        /// </summary>
        public static decimal TotalPayable(decimal partnerCostExclVat, decimal energyKwh, decimal feePerKwh)
        {
            decimal platformFeeAmount = Math.Round(energyKwh * feePerKwh, 2, MidpointRounding.AwayFromZero);
            decimal taxableValue = partnerCostExclVat + platformFeeAmount;
            decimal cgstAmount = Math.Round(taxableValue * CgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal sgstAmount = Math.Round(taxableValue * SgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            return taxableValue + cgstAmount + sgstAmount;
        }

        /// <summary>
        /// Running payable for a session still in progress, or null while the partner hasn't
        /// reported a cost yet.
        /// </summary>
        public static decimal? ProjectedPayable(OcpiPartnerSession session, decimal feePerKwh)
        {
            if (!session.TotalCost.HasValue) return null;
            return TotalPayable(session.TotalCost.Value, session.TotalEnergy ?? 0m, feePerKwh);
        }
    }
}
