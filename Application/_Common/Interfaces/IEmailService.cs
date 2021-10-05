using SendGrid.Helpers.Mail;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IEmailService
    {
        void SendEmail(SendGridMessage message, string recipient);

        Task<bool> SendEmailAsync(SendGridMessage message, string recipient);
    }
}