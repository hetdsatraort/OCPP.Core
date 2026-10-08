using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Database.EVCDTO;
using OCPP.Core.Management.Controllers;
using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static QuestPDF.Helpers.Colors;

namespace OCPP.Core.Management.Services
{
    public interface IEmailTemplateSevices
    {

        Task<(string message, bool success)> CallEmailServiceTemplate(string email, string emailBody, string subject);
    }


    public class EmailTemplateSevices : IEmailTemplateSevices
    {
        private readonly OCPPCoreContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public EmailTemplateSevices(OCPPCoreContext dbContext, IConfiguration configuration, ILogger logger)
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




    }

}
