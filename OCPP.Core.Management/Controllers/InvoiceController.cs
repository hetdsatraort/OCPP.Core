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
        private readonly IPartnerInvoiceService _partnerInvoiceService;
        private readonly ILogger<InvoiceController> _logger;

        public InvoiceController(
            OCPPCoreContext dbContext,
            IInvoiceService invoiceService,
            IPartnerInvoiceService partnerInvoiceService,
            ILogger<InvoiceController> logger)
        {
            _dbContext = dbContext;
            _invoiceService = invoiceService;
            _partnerInvoiceService = partnerInvoiceService;
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

        /// <summary>
        /// Generates (if needed) and downloads the platform-fee invoice PDF for an OCPI partner
        /// (roaming) session, identified by its OCPI session id.
        /// </summary>
        [HttpGet("partner-session/{sessionId}/download")]
        public async Task<IActionResult> DownloadPartnerInvoice(string sessionId)
        {
            var authResult = await AuthorizePartnerSessionAccess(sessionId);
            if (authResult != null)
            {
                return authResult;
            }

            try
            {
                var invoice = await _partnerInvoiceService.GetOrCreateInvoiceAsync(sessionId);
                var pdfBytes = _partnerInvoiceService.RenderPdf(invoice);
                var fileName = $"Invoice_{invoice.InvoiceNumber.Replace('/', '_')}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating partner invoice for session {SessionId}", sessionId);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Generates (if needed) and returns platform-fee invoice metadata as JSON for an OCPI
        /// partner session, without the PDF bytes.
        /// </summary>
        [HttpGet("partner-session/{sessionId}")]
        public async Task<IActionResult> GetPartnerInvoiceInfo(string sessionId)
        {
            var authResult = await AuthorizePartnerSessionAccess(sessionId);
            if (authResult != null)
            {
                return authResult;
            }

            try
            {
                var invoice = await _partnerInvoiceService.GetOrCreateInvoiceAsync(sessionId);
                return Ok(new { success = true, data = PartnerInvoiceInfoDto.FromEntity(invoice) });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating partner invoice for session {SessionId}", sessionId);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        /// <summary>
        /// Internal server-to-server endpoint used by OCPI.Core.Roaming's background billing
        /// service (OcpiOrphanSessionService) to create/fetch the platform-fee invoice for a
        /// partner session before debiting the wallet. No JWT is available to a background
        /// service, so this mirrors the existing zero-auth internal-call pattern already used
        /// between these two services in the other direction (see OcpiAdminController's
        /// admin/emsp/* endpoints in OCPI.Core.Roaming) — trust is via network placement, not a
        /// user token.
        /// </summary>
        [HttpPost("internal/partner-session/{sessionId}")]
        [AllowAnonymous]
        public async Task<IActionResult> CreatePartnerInvoiceInternal(string sessionId)
        {
            try
            {
                var invoice = await _partnerInvoiceService.GetOrCreateInvoiceAsync(sessionId);
                return Ok(new { success = true, data = PartnerInvoiceInfoDto.FromEntity(invoice) });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating partner invoice (internal) for session {SessionId}", sessionId);
                return StatusCode(500, new { success = false, message = "Internal server error" });
            }
        }

        private async Task<IActionResult> AuthorizePartnerSessionAccess(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return BadRequest(new { success = false, message = "Session id is required" });
            }

            var sessionUserId = await _dbContext.OcpiPartnerSessions
                .Where(s => s.SessionId == sessionId)
                .Select(s => s.UserId)
                .FirstOrDefaultAsync();

            if (sessionUserId == null)
            {
                return NotFound(new { success = false, message = "Partner session not found" });
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
