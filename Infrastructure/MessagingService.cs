using Application.Common.Interfaces;
using Application.Options;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace Infrastructure
{
    public class MessagingService : IMessagingService
    {
        private readonly ILogger<MessagingService> _logger;
        private readonly TwilioSettings _twilioSettings;

        #region Constructor

        public MessagingService(ILogger<MessagingService> logger, TwilioSettings twilioSettings)
        {
            _logger = logger;
            _twilioSettings = twilioSettings;
        }

        #endregion Constructor

        #region IMessageService Implementation

        public string SendMessage(string toPhoneNumber, string text, string requestId, CancellationToken cancellationToken)
        {
            if (_twilioSettings.SandBox != "0")
            {
                return "1";
            }

            string response = null;
            TwilioClient.Init(_twilioSettings.AccountSID, _twilioSettings.AuthToken);

            try
            {
                var message = MessageResource.CreateAsync(
                body: text,
                from: new Twilio.Types.PhoneNumber(_twilioSettings.FromPhone),
                to: new Twilio.Types.PhoneNumber(toPhoneNumber));
                response = message.Result.Sid;
                _logger.LogInformation($"SENDMESSAGE: Status={message.Status}; Timestamp={DateTime.Now.ToString()}; to={toPhoneNumber}; sid={message.Result.Sid}; request.Id={requestId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendMessage Exception: Message={ex.Message} StackTrace={ex.StackTrace}; request.Id={requestId}");
                response = "FAILED";
            }

            return response;
        }

        public async Task<string> SendMessageAsync(string toPhoneNumber, string text, string requestId, CancellationToken cancellationToken)
        {
            if (_twilioSettings.SandBox != "0")
            {
                return "1";
            }
            string response = null;
            TwilioClient.Init(_twilioSettings.AccountSID, _twilioSettings.AuthToken);

            try
            {
                var message = await MessageResource.CreateAsync(
                body: text,
                from: new Twilio.Types.PhoneNumber(_twilioSettings.FromPhone),
                to: new Twilio.Types.PhoneNumber(toPhoneNumber));
                response = message.Sid;
                _logger.LogInformation($"SENDMESSAGE: Status={message.Status}; Timestamp={DateTime.Now.ToString()}; to={toPhoneNumber}; sid={message.Sid}; request.Id={requestId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"SendMessage Exception: Message={ex.Message} StackTrace={ex.StackTrace}; request.Id={requestId}");
                response = "FAILED";
            }

            return response;
        }

        #endregion IMessageService Implementation
    }
}