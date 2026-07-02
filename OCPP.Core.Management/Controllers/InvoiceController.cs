using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models.Invoice;
using OCPP.Core.Management.Services.Invoice;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly IInvoiceService _invoiceService;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(OCPPCoreContext dbContext, IInvoiceService invoiceService, ILogger<InvoiceController> logger)
        {
            _dbContext = dbContext;
            _invoiceService = invoiceService;
            _logger = logger;
        }

        /// <summary>
        /// Generates (if needed) and downloads the invoice PDF for a charging session.
        /// Reuses the same invoice number/date on repeat calls.
        /// </summary>
        [HttpGet("session/{sessionId}/download")]
        public async Task<IActionResult> DownloadInvoice(string sessionId)
        {
            var authResult = await AuthorizeSessionAccess(sessionId);
            if (authResult != null)
            {
                return authResult;
            }

            try
            {
                var invoice = await _invoiceService.GetOrCreateInvoiceAsync(sessionId);
                var pdfBytes = _invoiceService.RenderPdf(invoice);
                var fileName = $"Invoice_{invoice.InvoiceNumber.Replace('/', '_')}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for session {SessionId}", sessionId);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Generates (if needed) and returns invoice metadata as JSON, without the PDF bytes.
        /// </summary>
        [HttpGet("session/{sessionId}")]
        public async Task<IActionResult> GetInvoiceInfo(string sessionId)
        {
            var authResult = await AuthorizeSessionAccess(sessionId);
            if (authResult != null)
            {
                return authResult;
            }

            try
            {
                var invoice = await _invoiceService.GetOrCreateInvoiceAsync(sessionId);
                return Ok(new { success = true, data = InvoiceInfoDto.FromEntity(invoice) });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating invoice for session {SessionId}", sessionId);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        private async Task<IActionResult> AuthorizeSessionAccess(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { success = false, message = "Session id is required" });
            }

            var sessionUserId = await _dbContext.ChargingSessions
                .Where(s => s.RecId == sessionId)
                .Select(s => s.UserId)
                .FirstOrDefaultAsync();

            if (sessionUserId == null)
            {
                return NotFound(new { success = false, message = "Charging session not found" });
            }

            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Administrator") || User.IsInRole("Admin");

            if (!isAdmin && sessionUserId != currentUserId)
            {
                return Forbid();
            }

            return null;
        }
    }
}
