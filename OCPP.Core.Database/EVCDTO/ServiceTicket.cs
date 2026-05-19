using System;

namespace OCPP.Core.Database.EVCDTO
{
    public class ServiceTicket
    {
        /// <summary>Internal GUID primary key.</summary>
        public string RecId { get; set; }

        /// <summary>Human-readable ticket ID shown to users, e.g. HYC-A1B2C3D4.</summary>
        public string ServiceTicketId { get; set; }

        /// <summary>FK → Users.RecId</summary>
        public string UserId { get; set; }

        /// <summary>Charger | Payment | Account | Other</summary>
        public string Category { get; set; }

        public string Subject { get; set; }

        public string Description { get; set; }

        /// <summary>Open | InProgress | Resolved | Closed</summary>
        public string Status { get; set; }

        /// <summary>Low | Medium | High</summary>
        public string Priority { get; set; }

        /// <summary>Optional reference to a ChargingSession.RecId.</summary>
        public string RelatedSessionId { get; set; }

        /// <summary>FK → Users.RecId – admin who is handling the ticket.</summary>
        public string AssignedToAdminId { get; set; }

        /// <summary>Admin-only internal notes.</summary>
        public string AdminNotes { get; set; }

        /// <summary>Resolution summary written by admin when closing.</summary>
        public string ResolutionNotes { get; set; }

        public int Active { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime UpdatedOn { get; set; }
    }
}
