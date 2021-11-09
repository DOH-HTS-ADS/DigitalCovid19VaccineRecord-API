using Application.Common;
using Application.Common.Interfaces;
using Application.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.VaccineCredential.Queries.GetVaccineStatus
{
    public class GetVaccineCredentialStatusQueryHandler : IRequestHandler<GetVaccineCredentialStatusQuery, StatusModel>
    {
        private readonly IAzureSynapseService _azureSynapseService;

        private readonly IEmailService _emailService;
        private readonly SendGridSettings _sendGridSettings;
        private readonly ILogger<GetVaccineCredentialStatusQueryHandler> _logger;
        private readonly IMessagingService _messagingService;
        private readonly AppSettings _appSettings;
        private readonly IAesEncryptionService _aesEncryptionService;
        private readonly IQueueService _queueService;
        private readonly IRateLimitService _rateLimitService;

        public GetVaccineCredentialStatusQueryHandler(IRateLimitService rateLimitService, IQueueService queueService, IAesEncryptionService aesEncryptionService, SendGridSettings sendGridSettings, IMessagingService messagingService, AppSettings appSettings, IEmailService emailService, IAzureSynapseService azureSynapseService, ILogger<GetVaccineCredentialStatusQueryHandler> logger)
        {
            _azureSynapseService = azureSynapseService;
            _logger = logger;
            _messagingService = messagingService;
            _appSettings = appSettings;
            _emailService = emailService;
            _sendGridSettings = sendGridSettings;
            _aesEncryptionService = aesEncryptionService;
            _queueService = queueService;
            _rateLimitService = rateLimitService;
        }

        public async Task<StatusModel> Handle(GetVaccineCredentialStatusQuery request, CancellationToken cancellationToken)
        {
            request.Id = Guid.NewGuid().ToString();
            var statusModel = new StatusModel();
            var rateLimiterContact = request.PhoneNumber;
            if (string.IsNullOrWhiteSpace(rateLimiterContact))
            {
                rateLimiterContact = request.EmailAddress;
            }
            var hash = _aesEncryptionService.Hash(rateLimiterContact.ToLower());
            var rateLimit = await _rateLimitService.RateLimitAsync(
                hash,
                Convert.ToInt32(_appSettings.MaxStatusTries),
                TimeSpan.FromSeconds(Convert.ToInt32(_appSettings.MaxStatusSeconds)));

            statusModel.RateLimit = rateLimit;

            if (rateLimit.Remaining < 0)
            {
                return statusModel;
            }

            var pinStatus = Utils.ValidatePin(request.Pin);
            if (pinStatus != 0)
            {
                switch(pinStatus)
                {
                    case 1:
                        _logger.LogInformation($"INVALID PIN: Pin cannot be null; request.Pin={request.Pin}; request.Id={request.Id};");
                        break;
                    case 2:
                        _logger.LogInformation($"INVALID PIN: Pin length must equal 4; request.Pin={request.Pin}; request.Id={request.Id}");
                        break;
                    case 3:
                        _logger.LogInformation($"INVALID PIN: Pin can only contain integers; request.Pin={request.Pin}; request.Id={request.Id}");
                        break;
                    case 4:
                        _logger.LogInformation($"INVALID PIN: Pin cannot have 4 or more of the same integer; request.Pin={request.Pin}; request.Id={request.Id}");
                        break;
                    case 5:
                        _logger.LogInformation($"INVALID PIN: Pin cannot have consecutive integers; request.Pin={request.Pin}; request.Id={request.Id}");
                        break;
                    default:
                        break;
                }
                statusModel.InvalidPin = true;
                return statusModel;
            }

            request.FirstName = request.FirstName.Trim();
            request.LastName = request.LastName.Trim();
            request.PhoneNumber = request.PhoneNumber.Trim();
            request.EmailAddress = request.EmailAddress.Trim();
            request.Language = request.Language.Trim();

            if (_appSettings.UseMessageQueue != "0")
            {
                statusModel.ViewStatus = await _queueService.AddMessageAsync(JsonConvert.SerializeObject(request));
                _logger.LogInformation($"REQUEST QUEUED: statusModel.ViewStatus={statusModel.ViewStatus}; searchCriteria={request.FirstName.Substring(0, 1)}.{request.LastName.Substring(0, 1)}. {((DateTime)request.DateOfBirth).ToString("MM/dd/yyyy")} {request.PhoneNumber}{request.EmailAddress} {request.Pin}; request.Id={request.Id}");
            }
            else
            {
                var r = await Utils.ProcessStatusRequest(_appSettings, _logger, _emailService, _sendGridSettings, _messagingService, _aesEncryptionService, request, _azureSynapseService, null, cancellationToken);
                statusModel.ViewStatus = r < 4;
            }
            return statusModel;
        }
    }
}