using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IEmailService
    {
        bool SendEmail(SendGridMessage message, string recipient, string requestId);

        Task<bool> SendEmailAsync(SendGridMessage message, string recipient, string requestId);
    }
}