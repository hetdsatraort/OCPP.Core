using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Models.ServiceTicket;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ServiceTicketController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<ServiceTicketController> _logger;

        public ServiceTicketController(OCPPCoreContext dbContext, ILogger<ServiceTicketController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // USER ENDPOINTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Create a new service ticket (any authenticated user).
        /// POST /api/ServiceTicket
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromBody] CreateServiceTicketRequestDto request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(new ServiceTicketResponse { Success = false, Message = "Invalid request data." });

                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ServiceTicketResponse { Success = false, Message = "User not authenticated." });

                var recId = Guid.NewGuid().ToString();
                var ticketId = GenerateTicketId();

                var ticket = new Database.EVCDTO.ServiceTicket
                {
                    RecId = recId,
                    ServiceTicketId = ticketId,
                    UserId = userId,
                    Category = request.Category,
                    Subject = request.Subject,
                    Description = request.Description,
                    Status = "Open",
                    Priority = string.IsNullOrEmpty(request.Priority) ? "Medium" : request.Priority,
                    RelatedSessionId = request.RelatedSessionId,
                    Active = 1,
                    CreatedOn = DateTime.UtcNow,
                    UpdatedOn = DateTime.UtcNow
                };

                _dbContext.ServiceTickets.Add(ticket);
                await _dbContext.SaveChangesAsync();

                var user = await _dbContext.Users.FindAsync(userId);
                return Ok(new ServiceTicketResponse
                {
                    Success = true,
                    Message = $"Ticket {ticketId} created successfully.",
                    Ticket = MapToDto(ticket, user, null)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service ticket");
                return StatusCode(500, new ServiceTicketResponse { Success = false, Message = "Internal server error." });
            }
        }

        /// <summary>
        /// Get all tickets for the currently logged-in user.
        /// GET /api/ServiceTicket/my-tickets
        /// </summary>
        [HttpGet("my-tickets")]
        public async Task<IActionResult> GetMyTickets(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string status = null)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new ServiceTicketListResponse { Success = false, Message = "User not authenticated." });

                var query = _dbContext.ServiceTickets
                    .Where(t => t.UserId == userId && t.Active == 1);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(t => t.Status == status);

                var totalCount = await query.CountAsync();

                var tickets = await query
                    .OrderByDescending(t => t.CreatedOn)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var user = await _dbContext.Users.FindAsync(userId);
                var dtos = tickets.Select(t => MapToDto(t, user, null)).ToList();

                return Ok(new ServiceTicketListResponse
                {
                    Success = true,
                    Message = "Tickets retrieved successfully.",
                    Tickets = dtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user tickets");
                return StatusCode(500, new ServiceTicketListResponse { Success = false, Message = "Internal server error." });
            }
        }

        /// <summary>
        /// Get a single ticket by its ServiceTicketId (e.g. HYC-A1B2C3D4).
        /// GET /api/ServiceTicket/{serviceTicketId}
        /// </summary>
        [HttpGet("{serviceTicketId}")]
        public async Task<IActionResult> GetTicket(string serviceTicketId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var userRole = GetCurrentUserRole();

                var ticket = await _dbContext.ServiceTickets
                    .FirstOrDefaultAsync(t => t.ServiceTicketId == serviceTicketId && t.Active == 1);

                if (ticket == null)
                    return NotFound(new ServiceTicketResponse { Success = false, Message = "Ticket not found." });

                // Non-admins can only view their own tickets
                if (userRole != "Administrator" && ticket.UserId != userId)
                    return Forbid();

                var user = await _dbContext.Users.FindAsync(ticket.UserId);
                Users adminUser = null;
                if (!string.IsNullOrEmpty(ticket.AssignedToAdminId))
                    adminUser = await _dbContext.Users.FindAsync(ticket.AssignedToAdminId);

                return Ok(new ServiceTicketResponse
                {
                    Success = true,
                    Message = "Ticket retrieved.",
                    Ticket = MapToDto(ticket, user, adminUser)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ticket {TicketId}", serviceTicketId);
                return StatusCode(500, new ServiceTicketResponse { Success = false, Message = "Internal server error." });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ADMIN ENDPOINTS
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Get all tickets across all users (Admin only).
        /// GET /api/ServiceTicket/admin/all
        /// </summary>
        [HttpGet("admin/all")]
        public async Task<IActionResult> AdminGetAllTickets(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string status = null,
            [FromQuery] string category = null,
            [FromQuery] string priority = null)
        {
            try
            {
                if (!IsAdministrator())
                    return Forbid();

                var query = _dbContext.ServiceTickets.Where(t => t.Active == 1);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(t => t.Status == status);
                if (!string.IsNullOrEmpty(category))
                    query = query.Where(t => t.Category == category);
                if (!string.IsNullOrEmpty(priority))
                    query = query.Where(t => t.Priority == priority);

                var totalCount = await query.CountAsync();

                var tickets = await query
                    .OrderByDescending(t => t.CreatedOn)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Batch-load users
                var userIds = tickets.Select(t => t.UserId).Distinct().ToList();
                var adminIds = tickets
                    .Where(t => !string.IsNullOrEmpty(t.AssignedToAdminId))
                    .Select(t => t.AssignedToAdminId).Distinct().ToList();
                var allIds = userIds.Union(adminIds).ToList();

                var users = await _dbContext.Users
                    .Where(u => allIds.Contains(u.RecId))
                    .ToListAsync();

                var dtos = tickets.Select(t =>
                {
                    var u = users.FirstOrDefault(x => x.RecId == t.UserId);
                    var a = string.IsNullOrEmpty(t.AssignedToAdminId)
                        ? null
                        : users.FirstOrDefault(x => x.RecId == t.AssignedToAdminId);
                    return MapToDto(t, u, a);
                }).ToList();

                return Ok(new ServiceTicketListResponse
                {
                    Success = true,
                    Message = "Tickets retrieved.",
                    Tickets = dtos,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all tickets (admin)");
                return StatusCode(500, new ServiceTicketListResponse { Success = false, Message = "Internal server error." });
            }
        }

        /// <summary>
        /// Update ticket status / add admin notes (Admin only).
        /// PUT /api/ServiceTicket/{serviceTicketId}/status
        /// </summary>
        [HttpPut("{serviceTicketId}/status")]
        public async Task<IActionResult> UpdateTicketStatus(string serviceTicketId, [FromBody] UpdateServiceTicketRequestDto request)
        {
            try
            {
                if (!IsAdministrator())
                    return Forbid();

                if (!ModelState.IsValid)
                    return BadRequest(new ServiceTicketResponse { Success = false, Message = "Invalid request." });

                var ticket = await _dbContext.ServiceTickets
                    .FirstOrDefaultAsync(t => t.ServiceTicketId == serviceTicketId && t.Active == 1);

                if (ticket == null)
                    return NotFound(new ServiceTicketResponse { Success = false, Message = "Ticket not found." });

                var validStatuses = new[] { "Open", "InProgress", "Resolved", "Closed" };
                if (!validStatuses.Contains(request.Status))
                    return BadRequest(new ServiceTicketResponse { Success = false, Message = "Invalid status value." });

                ticket.Status = request.Status;
                if (!string.IsNullOrEmpty(request.AdminNotes))
                    ticket.AdminNotes = request.AdminNotes;
                if (!string.IsNullOrEmpty(request.ResolutionNotes))
                    ticket.ResolutionNotes = request.ResolutionNotes;
                if (!string.IsNullOrEmpty(request.AssignedToAdminId))
                    ticket.AssignedToAdminId = request.AssignedToAdminId;

                ticket.UpdatedOn = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                var user = await _dbContext.Users.FindAsync(ticket.UserId);
                Users adminUser = null;
                if (!string.IsNullOrEmpty(ticket.AssignedToAdminId))
                    adminUser = await _dbContext.Users.FindAsync(ticket.AssignedToAdminId);

                return Ok(new ServiceTicketResponse
                {
                    Success = true,
                    Message = $"Ticket {serviceTicketId} updated to '{request.Status}'.",
                    Ticket = MapToDto(ticket, user, adminUser)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ticket {TicketId}", serviceTicketId);
                return StatusCode(500, new ServiceTicketResponse { Success = false, Message = "Internal server error." });
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private string GetCurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("UserId");

        private string GetCurrentUserRole() =>
            User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("UserRole")
            ?? string.Empty;

        private bool IsAdministrator() =>
            GetCurrentUserRole() == "Administrator";

        private static string GenerateTicketId()
        {
            // Format: HYC-XXXXXXXX (8 uppercase hex chars from a new GUID)
            var hex = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            return $"HYC-{hex}";
        }

        private static ServiceTicketDto MapToDto(
            Database.EVCDTO.ServiceTicket ticket,
            Users user,
            Users adminUser)
        {
            return new ServiceTicketDto
            {
                RecId = ticket.RecId,
                ServiceTicketId = ticket.ServiceTicketId,
                UserId = ticket.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null,
                UserEmail = user?.EMailID,
                UserPhone = user?.PhoneNumber,
                Category = ticket.Category,
                Subject = ticket.Subject,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                RelatedSessionId = ticket.RelatedSessionId,
                AssignedToAdminId = ticket.AssignedToAdminId,
                AssignedToAdminName = adminUser != null
                    ? $"{adminUser.FirstName} {adminUser.LastName}".Trim()
                    : null,
                AdminNotes = ticket.AdminNotes,
                ResolutionNotes = ticket.ResolutionNotes,
                CreatedOn = ticket.CreatedOn,
                UpdatedOn = ticket.UpdatedOn
            };
        }
    }
}
