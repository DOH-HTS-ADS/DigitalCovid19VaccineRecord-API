using Application.Common.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;

namespace Infrastructure
{
    public class EmailService : IEmailService
    {
        private readonly SendGridClient _client;
        public readonly ILogger<SendGridClient> _logger;
        private readonly SendGridSettings _sgSettings;

        public EmailService(SendGridClient client, ILogger<SendGridClient> logger, SendGridSettings sgSettings)
        {
            _client = client;
            _logger = logger;
            _sgSettings = sgSettings;
        }

        #region IEmailService Implementation

        public bool SendEmail(SendGridMessage message, string emailRecipient, string requestId)
        {
            if (_sgSettings.SandBox != "0")
            {
                message.SetSandBoxMode(true);
            }
            var resp = _client.SendEmailAsync(message);
            _logger.LogInformation($"SENDEMAIL: StatusCode={resp.Result.StatusCode}; IsSuccessStatusCode={resp.Result.IsSuccessStatusCode};  Timestamp={DateTime.Now.ToString()}; emailRecipient={emailRecipient}; request.Id={requestId}");
            return resp.Result.IsSuccessStatusCode;
        }

        public async Task<bool> SendEmailAsync(SendGridMessage message, string emailRecipient, string requestId)
        {
            if (_sgSettings.SandBox != "0")
            {
                message.SetSandBoxMode(true);
            }
            var resp = await _client.SendEmailAsync(message);
            _logger.LogInformation($"SENDEMAIL: StatusCode={resp.StatusCode}; IsSuccessStatusCode={resp.IsSuccessStatusCode}; Timestamp={DateTime.Now.ToString()}; emailRecipient={emailRecipient}; request.Id={requestId}");
            return resp.IsSuccessStatusCode;
        }

        #endregion IEmailService Implementation
    }
}