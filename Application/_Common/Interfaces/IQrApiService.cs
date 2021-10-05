using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IQrApiService
    {
        Task<byte[]> GetQrCodeAsync(string shc);
    }
}