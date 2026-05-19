using System.ComponentModel.DataAnnotations;

namespace OCPP.Core.Management.Models.ServiceTicket
{
    public class CreateServiceTicketRequestDto
    {
        [Required]
        [MaxLength(50)]
        public string Category { get; set; }   // Charger | Payment | Account | Other

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; }

        [MaxLength(50)]
        public string Priority { get; set; } = "Medium";  // Low | Medium | High

        /// <summary>Optional – link to a specific charging session.</summary>
        [MaxLength(50)]
        public string RelatedSessionId { get; set; }
    }

    public class UpdateServiceTicketRequestDto
    {
        /// <summary>Open | InProgress | Resolved | Closed</summary>
        [Required]
        [MaxLength(50)]
        public string Status { get; set; }

        [MaxLength(2000)]
        public string AdminNotes { get; set; }

        [MaxLength(2000)]
        public string ResolutionNotes { get; set; }

        /// <summary>Admin to assign the ticket to (optional).</summary>
        [MaxLength(50)]
        public string AssignedToAdminId { get; set; }
    }

    public class ServiceTicketDto
    {
        public string RecId { get; set; }
        public string ServiceTicketId { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserPhone { get; set; }
        public string Category { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public string RelatedSessionId { get; set; }
        public string AssignedToAdminId { get; set; }
        public string AssignedToAdminName { get; set; }
        public string AdminNotes { get; set; }
        public string ResolutionNotes { get; set; }
        public System.DateTime CreatedOn { get; set; }
        public System.DateTime UpdatedOn { get; set; }
    }

    public class ServiceTicketListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public System.Collections.Generic.List<ServiceTicketDto> Tickets { get; set; }
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class ServiceTicketResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public ServiceTicketDto Ticket { get; set; }
    }
}
