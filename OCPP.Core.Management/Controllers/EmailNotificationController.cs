using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using OCPP.Core.Management.Services;
using System.Threading.Tasks;

namespace OCPP.Core.Management.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class EmailNotificationController : ControllerBase
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly ILogger<EmailNotificationController> _logger;
        private readonly IConfiguration _config;
        private readonly IEmailTemplateSevices _emailTemplateServices;
        private readonly IEmailNotificationServices _emailNotificationServices;

        public EmailNotificationController(
            OCPPCoreContext dbContext,
            ILogger<EmailNotificationController> logger,
            IConfiguration config,
            IEmailTemplateSevices emailTemplateServices,
            IEmailNotificationServices emailNotificationServices)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;
            _emailTemplateServices = emailTemplateServices;
            _emailNotificationServices = emailNotificationServices;
        }



        [HttpPost("sendemail")]
        public async Task<IActionResult> SendEmail([FromBody] EmailNotificationRequest request)
        {
            await _emailTemplateServices.SendEmailAsync(request.ToEmail, request.Subject, request.EmailContent);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Email sent successfully!"
            });
        }



        [HttpPost("signup-sendemail")]
        public async Task<IActionResult> SignUp([FromBody] SignUpEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendSignUpConfirmationAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Signup confirmation email sent successfully."
            });
        }

        [HttpPost("forgot-password-sendemail")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendForgotPasswordAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Password reset email sent successfully."
            });
        }

        [HttpPost("login-alert-sendemail")]
        public async Task<IActionResult> LoginAlert([FromBody] LoginAlertEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendLoginAlertAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Login alert email sent successfully."
            });
        }

        [HttpPost("charging-session-summary-sendemail")]
        public async Task<IActionResult> ChargingSessionSummary([FromBody] ChargingSessionSummaryEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendChargingSessionSummaryAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Charging session summary email sent successfully."
            });
        }

        [HttpPost("wallet-recharge-sendemail")]
        public async Task<IActionResult> WalletRecharge([FromBody] WalletRechargeEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendWalletRechargeConfirmationAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Wallet recharge email sent successfully."
            });
        }

        [HttpPost("payment-deducted-sendemail")]
        public async Task<IActionResult> PaymentDeducted([FromBody] PaymentDeductedEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendPaymentDeductedAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Payment confirmation email sent successfully."
            });
        }

        [HttpPost("issue-created-sendemail")]
        public async Task<IActionResult> IssueCreated([FromBody] IssueTicketCreatedEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendIssueTicketCreatedAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Issue ticket email sent successfully."
            });
        }

        [HttpPost("issue-assigned-sendemail")]
        public async Task<IActionResult> IssueAssigned([FromBody] IssueAssignedEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendIssueAssignedAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Issue assigned email sent successfully."
            });
        }

        [HttpPost("issue-resolved-sendemail")]
        public async Task<IActionResult> IssueResolved([FromBody] IssueResolvedEmailNotificationRequest request)
        {
            await _emailNotificationServices.SendIssueResolvedAsync(request);

            return Ok(new EmailResponse
            {
                Success = true,
                Message = "Issue resolved email sent successfully."
            });
        }


    }
}
