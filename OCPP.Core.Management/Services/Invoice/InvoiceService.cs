using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using QuestPDF.Fluent;

namespace OCPP.Core.Management.Services.Invoice
{
    public class InvoiceService : IInvoiceService
    {
        private const string InvoicePrefix = "ORTEV";
        private const decimal CgstRate = 9m;
        private const decimal SgstRate = 9m;
        private const string SacCode = "998717";
        private const string LineItemDescription = "EV Charging Service";
        private const int MaxAllocationAttempts = 5;

        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<InvoiceService> _logger;
        private readonly string _logoPath;

        public InvoiceService(OCPPCoreContext dbContext, ILogger<InvoiceService> logger, IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _logger = logger;
            // Drop the company logo here once available; the document falls back to a placeholder box until then.
            _logoPath = Path.Combine(env.WebRootPath ?? string.Empty, "images", "company-logo.png");
        }

        public async Task<SessionInvoice> GetOrCreateInvoiceAsync(string chargingSessionId)
        {
            if (string.IsNullOrWhiteSpace(chargingSessionId))
            {
                throw new ArgumentException("Charging session id is required", nameof(chargingSessionId));
            }

            var existing = await _dbContext.SessionInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ChargingSessionId == chargingSessionId && i.Active == 1);
            if (existing != null)
            {
                return existing;
            }

            var session = await _dbContext.ChargingSessions
                .FirstOrDefaultAsync(s => s.RecId == chargingSessionId);
            if (session == null)
            {
                throw new InvalidOperationException("Charging session not found");
            }

            if (session.EndTime == default || session.EndTime <= session.StartTime || string.IsNullOrEmpty(session.EnergyTransmitted))
            {
                throw new InvalidOperationException("Invoice can only be generated for a completed charging session");
            }

            var gun = await _dbContext.ChargingGuns.AsNoTracking()
                .FirstOrDefaultAsync(g => g.RecId == session.ChargingGunId);
            var station = await _dbContext.ChargingStations.AsNoTracking()
                .FirstOrDefaultAsync(s => s.RecId == session.ChargingStationID);
            var hub = station != null
                ? await _dbContext.ChargingHubs.AsNoTracking().FirstOrDefaultAsync(h => h.RecId == station.ChargingHubId)
                : null;
            var chargerType = gun != null
                ? await _dbContext.ChargerTypeMasters.AsNoTracking().FirstOrDefaultAsync(c => c.RecId == gun.ChargerTypeId)
                : null;
            var user = await _dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.RecId == session.UserId);

            decimal.TryParse(session.EnergyTransmitted, out decimal energy);
            decimal.TryParse(session.ChargingTariff, out decimal tariff);

            decimal taxableValue = Math.Round(energy * tariff, 2, MidpointRounding.AwayFromZero);
            decimal cgstAmount = Math.Round(taxableValue * CgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal sgstAmount = Math.Round(taxableValue * SgstRate / 100m, 2, MidpointRounding.AwayFromZero);
            decimal grandTotal = taxableValue + cgstAmount + sgstAmount;

            string billedToName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
            string billedToPhone = user != null && !string.IsNullOrEmpty(user.PhoneNumber)
                ? $"+{user.CountryCode?.TrimStart('+')} {user.PhoneNumber}".Trim()
                : null;

            var invoiceDate = DateTime.UtcNow;

            for (int attempt = 1; attempt <= MaxAllocationAttempts; attempt++)
            {
                var invoiceNumber = await GenerateNextInvoiceNumberAsync(invoiceDate);

                var invoice = new SessionInvoice
                {
                    RecId = Guid.NewGuid().ToString(),
                    InvoiceNumber = invoiceNumber,
                    InvoiceDate = invoiceDate,
                    ChargingSessionId = session.RecId,
                    UserId = session.UserId,

                    BilledToName = billedToName,
                    BilledToPhone = billedToPhone,
                    BilledToEmail = user?.EMailID,

                    ChargingHubName = hub?.ChargingHubName,
                    ChargePointId = station?.ChargingPointId,
                    ChargerType = chargerType?.ChargerType,
                    City = hub?.City,
                    ConnectorId = gun?.ConnectorId,
                    PowerOutput = gun?.PowerOutput,

                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    EnergyConsumedKwh = energy,

                    Description = LineItemDescription,
                    SacCode = SacCode,
                    PricePerUnit = tariff,

                    TaxableValue = taxableValue,
                    Discount = 0,
                    Cashback = 0,
                    CgstRate = CgstRate,
                    CgstAmount = cgstAmount,
                    SgstRate = SgstRate,
                    SgstAmount = sgstAmount,
                    GrandTotal = grandTotal,

                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.SessionInvoices.Add(invoice);

                try
                {
                    await _dbContext.SaveChangesAsync();
                    return invoice;
                }
                catch (DbUpdateException ex)
                {
                    _dbContext.Entry(invoice).State = EntityState.Detached;
                    _logger.LogWarning(ex, "Invoice allocation collision on attempt {Attempt} for session {SessionId}", attempt, chargingSessionId);

                    // Another concurrent request may have already created the invoice for this session
                    var raced = await _dbContext.SessionInvoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.ChargingSessionId == chargingSessionId && i.Active == 1);
                    if (raced != null)
                    {
                        return raced;
                    }

                    if (attempt == MaxAllocationAttempts)
                    {
                        throw new InvalidOperationException("Failed to allocate an invoice number after multiple attempts", ex);
                    }
                }
            }

            throw new InvalidOperationException("Failed to allocate an invoice number after multiple attempts");
        }

        public byte[] RenderPdf(SessionInvoice invoice)
        {
            var logoPath = File.Exists(_logoPath) ? _logoPath : null;
            var document = new InvoiceDocument(invoice, logoPath);
            return document.GeneratePdf();
        }

        private async Task<string> GenerateNextInvoiceNumberAsync(DateTime invoiceDate)
        {
            // Indian financial year: April-March, e.g. 09/05/2026 -> FY2026-27 -> "2627"
            int fyStartYear = invoiceDate.Month >= 4 ? invoiceDate.Year : invoiceDate.Year - 1;
            string fyCode = $"{fyStartYear % 100:D2}{(fyStartYear + 1) % 100:D2}";
            string prefix = $"{InvoicePrefix}/{fyCode}/";

            int count = await _dbContext.SessionInvoices
                .AsNoTracking()
                .CountAsync(i => i.InvoiceNumber.StartsWith(prefix));

            int nextSeq = count + 1;
            return $"{prefix}{nextSeq:D5}";
        }
    }
}
