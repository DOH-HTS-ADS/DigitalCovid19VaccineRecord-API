using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IMessagingService
    {
        string SendMessage(string toPhoneNumber, string text, string requestId, CancellationToken cancellationToken);

        Task<string> SendMessageAsync(string toPhoneNumber, string text, string requestId, CancellationToken cancellationToken);
    }
}