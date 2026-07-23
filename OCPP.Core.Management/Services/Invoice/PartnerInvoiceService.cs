using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.OCPIDTO;
using QuestPDF.Fluent;

namespace OCPP.Core.Management.Services.Invoice
{
    /// <summary>
    /// Computes and persists the tax invoice for a completed OCPI partner (roaming) session, and
    /// renders it as a PDF.
    ///
    /// The taxable value is the partner CPO's reported <see cref="OcpiPartnerSession.TotalCost"/>
    /// plus HyCharge's own additive per-kWh platform fee — 9% CGST + 9% SGST is charged on that
    /// combined amount, not on the platform fee alone, so the invoice covers the full amount
    /// actually billed to the user's wallet.
    ///
    /// OCPI.Core.Roaming's OcpiOrphanSessionService needs the same computation for sessions it
    /// finalises in the background, but that project doesn't reference OCPP.Core.Management —
    /// it calls back into this service over HTTP via IPartnerInvoiceClient rather than duplicating
    /// the tax math locally.
    /// </summary>
    public class PartnerInvoiceService : IPartnerInvoiceService
    {
        private const string InvoicePrefix = "ORTEV-OCPI";
        private const decimal CgstRate = 9m;
        private const decimal SgstRate = 9m;
        private const string SacCode = "998717";
        private const string LineItemDescription = "OCPI Roaming Platform Fee";
        private const int MaxAllocationAttempts = 5;
        private readonly ILogger<PartnerInvoiceService> _logger;
        private readonly OCPPCoreContext _dbContext;
        private readonly string _logoPath;

        public PartnerInvoiceService(OCPPCoreContext dbContext, ILogger<PartnerInvoiceService> logger, IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _logger = logger;
            _logoPath = Path.Combine(env.WebRootPath ?? string.Empty, "images", "company-logo.png");
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
                // Backfills TotalPayable on sessions whose invoice was generated before this
                // column existed, and is a harmless no-op (EF skips the UPDATE) otherwise.
                if (session.TotalPayable != existing.TotalPayable)
                {
                    session.TotalPayable = existing.TotalPayable;
                    await _dbContext.SaveChangesAsync();
                }
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
            decimal partnerCost = session.TotalCost.Value;
            // GST is charged on the partner's own energy cost plus HyCharge's platform fee
            // combined — not on the platform fee alone — so the invoice covers the full amount
            // actually billed to the user, not just HyCharge's margin.
            decimal taxableValue = partnerCost + platformFeeAmount;
            decimal cgstAmount = Math.Round(taxableValue * CgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal sgstAmount = Math.Round(taxableValue * SgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal grandTotal = taxableValue + cgstAmount + sgstAmount;
            decimal totalPayable = grandTotal;

            // Persisted directly on the session (in addition to the invoice row below) so the
            // session-listing APIs can surface it without needing to look up the invoice.
            session.TotalPayable = totalPayable;

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

                    TaxableValue = taxableValue,
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
            var logoPath = File.Exists(_logoPath) ? _logoPath : null;
            var document = new PartnerInvoiceDocument(invoice, logoPath);
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
