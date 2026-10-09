using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Controllers;
using System;
using System.Net.Http;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit.Security;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace OCPP.Core.Management.Services
{
    public interface IEmailTemplateSevices
    {

        Task<(string message, bool success)> CallEmailServiceTemplate(string email, string emailBody, string subject);
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }


    public class EmailTemplateSevices : IEmailTemplateSevices
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailTemplateSevices> _logger;

        public EmailTemplateSevices(OCPPCoreContext dbContext, IConfiguration configuration, ILogger<EmailTemplateSevices> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;

        }



        public async Task<(string message, bool success)> CallEmailServiceTemplate(string email, string emailBody, string subject)
        {
            var mailsent = string.Empty;
            bool success = true;

            try
            {
                var toEmail = email;
                var mailtype = _configuration["EmailAPIDetails:MailType"];
                var apiUrl = _configuration["EmailAPIDetails:EmailAPIURL"];
                var apiToken = _configuration["EmailAPIDetails:EmailAPIToken"];
                var fromEmail = _configuration["EmailAPIDetails:FromEmail"];
                var replyToEmail = _configuration["EmailAPIDetails:ReplyToEmail"];


                var jsonContent = $@"{{
                                        ""apiver"": ""1.0"",
                                        ""email"": {{
                                            ""api"": true,
                                            ""ver"": ""1.0"",
                                            ""messages"": [
                                                {{
                                                    ""addresses"": [
                                                        {{
                                                            ""to"": [
                                                                {{
                                                                    ""emailid"": ""{toEmail}""
                                                                }}
                                                            ]
                                                        }}
                                                    ],
                                                   ""subject"": ""{subject}"",
                                                   ""mailtype"": ""{mailtype}"",
                                                    ""content"": [
                                                        {{
                                                            ""type"": ""text/html"",
                                                            ""value"":  ""{emailBody}""
                                                        }}
                                                    ],
                                                    ""from"": {{
                                                        ""emailid"": ""{fromEmail}""
                                                    }},
                                                    ""reply_to"": {{
                                                        ""emailid"": ""{replyToEmail}""
                                                    }}
                                                }}
                                            ]
                                        }}
                                    }}";


                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiToken);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync(apiUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Response:");
                        Console.WriteLine(responseContent);
                        mailsent = "SUCCESS";
                        _logger.LogInformation($"Sending Email Sent Successfully to {toEmail} and {response.StatusCode}");

                    }
                    else
                    {
                        Console.WriteLine("Error: " + response.StatusCode);
                        mailsent = "ERROR";
                        _logger.LogInformation($"Error Sending Email to {toEmail}: {response.StatusCode}");

                    }
                }

            }
            catch (Exception ex)
            {
                mailsent = "EXCEPTION";
                _logger.LogError($"Exception Sending Email: {ex.Message}");
            }

            return (mailsent, success);
        }




        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var senderEmail = _configuration["SmtpSettings:SenderEmail"]!;
                var password = _configuration["SmtpSettings:Password"]!;
                var host = _configuration["SmtpSettings:Host"]!;
                var port = int.Parse(_configuration["SmtpSettings:Port"]!);

                var message = new MimeMessage();

                message.From.Add(new MailboxAddress("HyCharge", senderEmail));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                message.Body = new BodyBuilder
                {
                    HtmlBody = htmlBody
                }.ToMessageBody();

                using var smtp = new SmtpClient();

                //await smtp.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect); // gmail requires SSL
                await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls); // use StartTLS for other SMTP servers
                await smtp.AuthenticateAsync(senderEmail, password);
                //await smtp.SendAsync(message);
                var response = await smtp.SendAsync(message);

                Console.WriteLine($"SMTP response: {response}");
                _logger.LogInformation($"SMTP response: {response}");

                await smtp.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email: {ex.Message}");
                throw;
            }

        }


    }

}
