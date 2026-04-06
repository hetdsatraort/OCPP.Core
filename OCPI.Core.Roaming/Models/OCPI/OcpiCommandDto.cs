using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCPI.Core.Roaming.Models.OCPI
{
    /// <summary>
    /// OCPI Command DTO - for remote commands
    /// </summary>
    public class OcpiCommandDto
    {
        [Required]
        public string Id { get; set; }

        [Required]
        public string RequestId { get; set; }

        [Required]
        public string Type { get; set; } // START_SESSION, STOP_SESSION, RESERVE_NOW, etc.

        [Required]
        public string Status { get; set; } // PENDING, ACCEPTED, REJECTED, etc.

        [Required]
        public DateTime Timestamp { get; set; }

        public object Parameters { get; set; }

        public CommandResult Result { get; set; }

        [Required]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Command Result
    /// </summary>
    public class CommandResult
    {
        [Required]
        public string Result { get; set; }

        public List<DisplayText> Message { get; set; }
    }

    /// <summary>
    /// Start Session Command
    /// </summary>
    public class StartSessionCommand
    {
        [Required]
        public string ResponseUrl { get; set; }

        [Required]
        public OcpiCdrToken Token { get; set; }

        [Required]
        public string LocationId { get; set; }

        public string EvseUid { get; set; }
        public string ConnectorId { get; set; }

        public string AuthorizationReference { get; set; }
    }

    /// <summary>
    /// Stop Session Command
    /// </summary>
    public class StopSessionCommand
    {
        [Required]
        public string ResponseUrl { get; set; }

        [Required]
        public string SessionId { get; set; }
    }

    /// <summary>
    /// Reserve Now Command
    /// </summary>
    public class ReserveNowCommand
    {
        [Required]
        public string ResponseUrl { get; set; }

        [Required]
        public OcpiCdrToken Token { get; set; }

        [Required]
        public DateTime ExpiryDate { get; set; }

        [Required]
        public string ReservationId { get; set; }

        [Required]
        public string LocationId { get; set; }

        public string EvseUid { get; set; }
        public string AuthorizationReference { get; set; }
    }

    /// <summary>
    /// Cancel Reservation Command
    /// </summary>
    public class CancelReservationCommand
    {
        [Required]
        public string ResponseUrl { get; set; }

        [Required]
        public string ReservationId { get; set; }
    }

    /// <summary>
    /// Unlock Connector Command
    /// </summary>
    public class UnlockConnectorCommand
    {
        [Required]
        public string ResponseUrl { get; set; }

        [Required]
        public string LocationId { get; set; }

        [Required]
        public string EvseUid { get; set; }

        [Required]
        public string ConnectorId { get; set; }
    }
}
