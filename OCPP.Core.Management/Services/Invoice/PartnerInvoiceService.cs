using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
using QuestPDF.Fluent;

namespace OCPP.Core.Management.Services.Invoice
{
    /// <summary>
    /// Computes and persists HyCharge's own platform-fee invoice for a completed OCPI partner
    /// (roaming) session, and renders it as a PDF.
    ///
    /// The partner CPO's own reported <see cref="OcpiPartnerSession.TotalCost"/> is never taxed
    /// here — it is that partner's own sale, already priced (and taxed, if applicable) by them.
    /// This service only computes and invoices HyCharge's additive per-kWh platform fee plus
    /// 9% CGST + 9% SGST on that fee.
    ///
    /// OCPI.Core.Roaming's OcpiOrphanSessionService needs the same computation for sessions it
    /// finalises in the background, but that project doesn't reference OCPP.Core.Management —
    /// it carries its own local copy of this logic (OCPI.Core.Roaming/Services/PartnerInvoiceService.cs),
    /// mirroring how the wallet-debit logic itself is already duplicated between the two projects
    /// rather than shared.
    /// </summary>
    public class PartnerInvoiceService : IPartnerInvoiceService
    {
        private const string InvoicePrefix = "ORTEV-OCPI";
        private const decimal CgstRate = 9m;
        private const decimal SgstRate = 9m;
        private const string SacCode = "998717";
        private const string LineItemDescription = "OCPI Roaming Platform Fee";
        private const int MaxAllocationAttempts = 5;

        private readonly OCPPCoreContext _dbContext;

        public PartnerInvoiceService(OCPPCoreContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OcpiPartnerSessionInvoice> GetOrCreateInvoiceAsync(string ocpiSessionId)
        {
            if (string.IsNullOrWhiteSpace(ocpiSessionId))
            {
                throw new ArgumentException("OCPI session id is required", nameof(ocpiSessionId));
            }

            var session = await _dbContext.OcpiPartnerSessions
                .FirstOrDefaultAsync(s => s.SessionId == ocpiSessionId);
            if (session == null)
            {
                throw new InvalidOperationException("Partner session not found");
            }

            return await GetOrCreateInvoiceAsync(session);
        }

        public async Task<OcpiPartnerSessionInvoice> GetOrCreateInvoiceAsync(OcpiPartnerSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var existing = await _dbContext.OcpiPartnerSessionInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.OcpiPartnerSessionId == session.Id && i.Active == 1);
            if (existing != null)
            {
                return existing;
            }

            if (!session.TotalCost.HasValue || session.TotalCost <= 0)
            {
                throw new InvalidOperationException("Invoice can only be generated once the partner session has a reported cost");
            }

            decimal energy = session.TotalEnergy ?? 0;

            decimal feePerKwh = 0;
            var feeConfig = await _dbContext.OcpiPartnerPlatformFees
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.PartnerCredentialId == session.PartnerCredentialId && f.IsActive);
            if (feeConfig != null)
            {
                feePerKwh = feeConfig.FeePerKwh;
            }

            decimal platformFeeAmount = Math.Round(energy * feePerKwh, 2, MidpointRounding.AwayFromZero);
            decimal cgstAmount = Math.Round(platformFeeAmount * CgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal sgstAmount = Math.Round(platformFeeAmount * SgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal grandTotal = platformFeeAmount + cgstAmount + sgstAmount;
            decimal partnerCost = session.TotalCost.Value;
            decimal totalPayable = partnerCost + grandTotal;

            OcpiPartnerCredential partner = null;
            if (session.PartnerCredentialId.HasValue)
            {
                partner = await _dbContext.OcpiPartnerCredentials
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == session.PartnerCredentialId.Value);
            }

            OCPP.Core.Database.EVCDTO.Users user = null;
            if (!string.IsNullOrEmpty(session.UserId))
            {
                user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.RecId == session.UserId);
            }

            string billedToName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
            string billedToPhone = user != null && !string.IsNullOrEmpty(user.PhoneNumber)
                ? $"+{user.CountryCode?.TrimStart('+')} {user.PhoneNumber}".Trim()
                : null;

            var invoiceDate = DateTime.UtcNow;

            for (int attempt = 1; attempt <= MaxAllocationAttempts; attempt++)
            {
                var invoiceNumber = await GenerateNextInvoiceNumberAsync(invoiceDate);

                var invoice = new OcpiPartnerSessionInvoice
                {
                    RecId = Guid.NewGuid().ToString(),
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = invoiceDate,
                    OcpiPartnerSessionId = session.Id,
                    SessionId = session.SessionId,
                    UserId = session.UserId,
                    PartnerCredentialId = session.PartnerCredentialId,
                    PartnerName = partner?.BusinessName,

                    BilledToName = billedToName,
                    BilledToPhone = billedToPhone,
                    BilledToEmail = user?.EMailID,

                    StartTime = session.StartDateTime,
                    EndTime = session.EndDateTime,
                    EnergyConsumedKwh = energy,
                    Currency = session.Currency,

                    PartnerCost = partnerCost,

                    Description = LineItemDescription,
                    SacCode = SacCode,
                    PricePerUnit = feePerKwh,

                    TaxableValue = platformFeeAmount,
                    CgstRate = CgstRate,
                    CgstAmount = cgstAmount,
                    SgstRate = SgstRate,
                    SgstAmount = sgstAmount,
                    GrandTotal = grandTotal,
                    TotalPayable = totalPayable,

                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.OcpiPartnerSessionInvoices.Add(invoice);

                try
                {
                    await _dbContext.SaveChangesAsync();
                    return invoice;
                }
                catch (DbUpdateException)
                {
                    _dbContext.Entry(invoice).State = EntityState.Detached;

                    // Another concurrent request may have already created the invoice for this session
                    var raced = await _dbContext.OcpiPartnerSessionInvoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.OcpiPartnerSessionId == session.Id && i.Active == 1);
                    if (raced != null)
                    {
                        return raced;
                    }

                    if (attempt == MaxAllocationAttempts)
                    {
                        throw new InvalidOperationException("Failed to allocate a partner invoice number after multiple attempts");
                    }
                }
            }

            throw new InvalidOperationException("Failed to allocate a partner invoice number after multiple attempts");
        }

        public byte[] RenderPdf(OcpiPartnerSessionInvoice invoice)
        {
            var document = new PartnerInvoiceDocument(invoice);
            return document.GeneratePdf();
        }

        private async Task<string> GenerateNextInvoiceNumberAsync(DateTime invoiceDate)
        {
            // Indian financial year: April-March, e.g. 09/05/2026 -> FY2026-27 -> "2627"
            int fyStartYear = invoiceDate.Month >= 4 ? invoiceDate.Year : invoiceDate.Year - 1;
            string fyCode = $"{fyStartYear % 100:D2}{(fyStartYear + 1) % 100:D2}";
            string prefix = $"{InvoicePrefix}/{fyCode}/";

            int count = await _dbContext.OcpiPartnerSessionInvoices
                .AsNoTracking()
                .CountAsync(i => i.InvoiceNumber.StartsWith(prefix));

            int nextSeq = count + 1;
            return $"{prefix}{nextSeq:D5}";
        }
    }
}
