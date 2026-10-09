using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;
using System.IO;
using System.Threading.Tasks;
using static OCPP.Core.Management.Services.EmailNotificationServices;

namespace OCPP.Core.Management.Services
{


    public interface IEmailNotificationServices
    {

        Task SendSignUpConfirmationAsync(SignUpEmailNotificationRequest request);
        Task SendForgotPasswordAsync(ForgotPasswordEmailNotificationRequest request);
        Task SendLoginAlertAsync(LoginAlertEmailNotificationRequest request);
        Task SendChargingSessionSummaryAsync(ChargingSessionSummaryEmailNotificationRequest request);
        Task SendWalletRechargeConfirmationAsync(WalletRechargeEmailNotificationRequest request);
        Task SendPaymentDeductedAsync(PaymentDeductedEmailNotificationRequest request);
        Task SendIssueTicketCreatedAsync(IssueTicketCreatedEmailNotificationRequest request);
        Task SendIssueAssignedAsync(IssueAssignedEmailNotificationRequest request);
        Task SendIssueResolvedAsync(IssueResolvedEmailNotificationRequest request);

    }


    public class EmailNotificationServices : IEmailNotificationServices
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailNotificationServices> _logger;
        private readonly IEmailTemplateSevices _emailTemplateServices;

        public EmailNotificationServices(OCPPCoreContext dbContext, IConfiguration configuration, ILogger<EmailNotificationServices> logger, IEmailTemplateSevices emailTemplateServices)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
            _emailTemplateServices = emailTemplateServices;
        }



        public async Task SendSignUpConfirmationAsync(SignUpEmailNotificationRequest request)
        {
            // Send welcome email template
            var mailsubject = "Welcome to HyCharge ⚡";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Sign_Up_Confirmation.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);
            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            //emailcontant = emailcontant.Replace("\"", "\\\"");
            //await _emailTemplateSevices.CallEmailServiceTemplate(user.EMailID, mailsubject, emailcontant);


            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }

        public async Task SendForgotPasswordAsync(ForgotPasswordEmailNotificationRequest request)
        {
            var mailsubject = "Reset your HyCharge password";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Forgot_Password.html");

            var emailcontant = await File.ReadAllTextAsync(templatePath);
            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{ResetLink}}", System.Net.WebUtility.HtmlEncode(request.ResetLink));
            emailcontant = emailcontant.Replace("{{ExpiryTime}}", request.ExpiryTime.ToString());

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }


        public async Task SendLoginAlertAsync(LoginAlertEmailNotificationRequest request)
        {
            var mailsubject = "New login to your HyCharge account";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Login_Alert.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{DateTime}}", System.Net.WebUtility.HtmlEncode(request.DateTime));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }


        public async Task SendChargingSessionSummaryAsync(ChargingSessionSummaryEmailNotificationRequest request)
        {
            var mailsubject = "Your charging session summary ⚡";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Charging_Session_Summary.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{StationName}}", System.Net.WebUtility.HtmlEncode(request.StationName));
            emailcontant = emailcontant.Replace("{{Date}}", System.Net.WebUtility.HtmlEncode(request.Date));
            emailcontant = emailcontant.Replace("{{StartTime}}", System.Net.WebUtility.HtmlEncode(request.StartTime));
            emailcontant = emailcontant.Replace("{{EndTime}}", System.Net.WebUtility.HtmlEncode(request.EndTime));
            emailcontant = emailcontant.Replace("{{Energy}}", request.Energy.ToString("0.##"));
            emailcontant = emailcontant.Replace("{{Duration}}", System.Net.WebUtility.HtmlEncode(request.Duration));
            emailcontant = emailcontant.Replace("{{Amount}}", request.Amount.ToString("0.00"));
            emailcontant = emailcontant.Replace("{{Balance}}", request.Balance.ToString("0.00"));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }


        public async Task SendWalletRechargeConfirmationAsync(WalletRechargeEmailNotificationRequest request)
        {
            var mailsubject = "Your wallet has been recharged successfully";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Wallet_Recharge_Confirmation.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{Amount}}", request.Amount.ToString("0.00"));
            emailcontant = emailcontant.Replace("{{TransactionID}}", System.Net.WebUtility.HtmlEncode(request.TransactionID));
            emailcontant = emailcontant.Replace("{{Balance}}", request.Balance.ToString("0.00"));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }

        public async Task SendPaymentDeductedAsync(PaymentDeductedEmailNotificationRequest request)
        {
            var mailsubject = "Payment confirmation for your charging session";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Payment_Deducted(Charging Session).html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{StationName}}", System.Net.WebUtility.HtmlEncode(request.StationName));
            emailcontant = emailcontant.Replace("{{Amount}}", request.Amount.ToString("0.00"));
            emailcontant = emailcontant.Replace("{{TransactionID}}", System.Net.WebUtility.HtmlEncode(request.TransactionID));
            emailcontant = emailcontant.Replace("{{Balance}}", request.Balance.ToString("0.00"));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }



        public async Task SendIssueTicketCreatedAsync(IssueTicketCreatedEmailNotificationRequest request)
        {
            var mailsubject = $"We've received your issue – Ticket {request.TicketID}";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Issue_Ticket_Created.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{TicketID}}", System.Net.WebUtility.HtmlEncode(request.TicketID));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }


        public async Task SendIssueAssignedAsync(IssueAssignedEmailNotificationRequest request)
        {
            var mailsubject = $"Your issue is being reviewed – Ticket {request.TicketID}";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Issue_Assigned.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{TicketID}}", System.Net.WebUtility.HtmlEncode(request.TicketID));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);
        }

        public async Task SendIssueResolvedAsync(IssueResolvedEmailNotificationRequest request)
        {
            var mailsubject = $"Your issue has been resolved – Ticket {request.TicketID}";
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "EmailTemplate", "Issue_Resolved.html");
            var emailcontant = await File.ReadAllTextAsync(templatePath);

            emailcontant = emailcontant.Replace("{{CustomerName}}", System.Net.WebUtility.HtmlEncode(request.CustomerName));
            emailcontant = emailcontant.Replace("{{TicketID}}", System.Net.WebUtility.HtmlEncode(request.TicketID));
            emailcontant = emailcontant.Replace("{{AdminRemark}}", System.Net.WebUtility.HtmlEncode(request.AdminRemark));

            await _emailTemplateServices.SendEmailAsync(request.ToEmail, mailsubject, emailcontant);

        }



    }
}
