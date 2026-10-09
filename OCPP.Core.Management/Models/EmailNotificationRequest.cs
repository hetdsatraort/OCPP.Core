namespace OCPP.Core.Management.Models
{
    public class EmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string EmailContent { get; set; } = string.Empty;
    }

    public class SignUpEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
          public string CustomerName { get; set; } = string.Empty;

    }


    public class ForgotPasswordEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ResetLink { get; set; } = string.Empty;
        public int ExpiryTime { get; set; }
    }

    public class LoginAlertEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string DateTime { get; set; } = string.Empty;
    }

    public class ChargingSessionSummaryEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public decimal Energy { get; set; }
        public string Duration { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Balance { get; set; }
    }

    public class WalletRechargeEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TransactionID { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }


    public class PaymentDeductedEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string StationName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string TransactionID { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }


    public class IssueTicketCreatedEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string TicketID { get; set; } = string.Empty;
    }


    public class IssueAssignedEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string TicketID { get; set; } = string.Empty;
    }


    public class IssueResolvedEmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string TicketID { get; set; } = string.Empty;
        public string AdminRemark { get; set; } = string.Empty;
    }

    public class EmailResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

}
