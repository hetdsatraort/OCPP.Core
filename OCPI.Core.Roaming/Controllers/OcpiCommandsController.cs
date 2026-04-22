using Microsoft.AspNetCore.Mvc;
using OCPI.Contracts;
using OCPI.Core.Roaming.Services;

namespace OCPI.Core.Roaming.Controllers
{
    /// <summary>
    /// OCPI Commands Receiver Controller (CPO role)
    /// Receives remote command requests from eMSPs: StartSession, StopSession,
    /// ReserveNow, CancelReservation, UnlockConnector.
    /// </summary>
    [OcpiEndpoint(OcpiModule.Commands, "Receiver", "2.2.1")]
    [Route("2.2.1/commands")]
    [OcpiAuthorize]
    public class OcpiCommandsController : OcpiController
    {
        private readonly IOcpiCommandService _commandService;
        private readonly ILogger<OcpiCommandsController> _logger;

        public OcpiCommandsController(
            IOcpiCommandService commandService,
            ILogger<OcpiCommandsController> logger)
        {
            _commandService = commandService;
            _logger = logger;
        }

        /// <summary>POST a START_SESSION command</summary>
        [HttpPost("START_SESSION")]
        public async Task<IActionResult> StartSession([FromBody] OcpiStartSessionCommand command)
        {
            OcpiValidate(command);
            _logger.LogInformation("Received START_SESSION for location={LocationId}", command.LocationId);

            var result = await _commandService.HandleStartSessionAsync(command);

            return OcpiOk(new OcpiCommandResponse
            {
                Result  = result,
                Timeout = 30,
                Message = new[] { new OcpiDisplayText { Language = "en", Text = result.ToString() } }
            });
        }

        /// <summary>POST a STOP_SESSION command</summary>
        [HttpPost("STOP_SESSION")]
        public async Task<IActionResult> StopSession([FromBody] OcpiStopSessionCommand command)
        {
            OcpiValidate(command);
            _logger.LogInformation("Received STOP_SESSION for session={SessionId}", command.SessionId);

            var result = await _commandService.HandleStopSessionAsync(command);

            return OcpiOk(new OcpiCommandResponse
            {
                Result  = result,
                Timeout = 30,
                Message = new[] { new OcpiDisplayText { Language = "en", Text = result.ToString() } }
            });
        }

        /// <summary>POST a RESERVE_NOW command</summary>
        [HttpPost("RESERVE_NOW")]
        public async Task<IActionResult> ReserveNow([FromBody] OcpiReserveNowCommand command)
        {
            OcpiValidate(command);
            _logger.LogInformation("Received RESERVE_NOW for location={LocationId}", command.LocationId);

            var result = await _commandService.HandleReserveNowAsync(command);

            return OcpiOk(new OcpiCommandResponse
            {
                Result  = result,
                Timeout = 30,
                Message = new[] { new OcpiDisplayText { Language = "en", Text = result.ToString() } }
            });
        }

        /// <summary>POST a CANCEL_RESERVATION command</summary>
        [HttpPost("CANCEL_RESERVATION")]
        public async Task<IActionResult> CancelReservation([FromBody] OcpiCancelReservationCommand command)
        {
            OcpiValidate(command);
            _logger.LogInformation("Received CANCEL_RESERVATION for reservation={ReservationId}", command.ReservationId);

            var result = await _commandService.HandleCancelReservationAsync(command);

            return OcpiOk(new OcpiCommandResponse
            {
                Result  = result,
                Timeout = 30,
                Message = new[] { new OcpiDisplayText { Language = "en", Text = result.ToString() } }
            });
        }

        /// <summary>POST an UNLOCK_CONNECTOR command</summary>
        [HttpPost("UNLOCK_CONNECTOR")]
        public async Task<IActionResult> UnlockConnector([FromBody] OcpiUnlockConnectorCommand command)
        {
            OcpiValidate(command);
            _logger.LogInformation("Received UNLOCK_CONNECTOR for location={LocationId} evse={EvseUid}",
                command.LocationId, command.EvseUid);

            var result = await _commandService.HandleUnlockConnectorAsync(command);

            return OcpiOk(new OcpiCommandResponse
            {
                Result  = result,
                Timeout = 30,
                Message = new[] { new OcpiDisplayText { Language = "en", Text = result.ToString() } }
            });
        }
    }
}
