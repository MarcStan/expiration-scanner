using System.Threading.Tasks;

namespace ExpirationScanner.Services
{
    public interface ISlackService
    {
        Task SendSlackMessageAsync(string text);
    }
}