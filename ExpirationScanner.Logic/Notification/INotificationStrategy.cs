using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic.Notification
{
    public interface INotificationStrategy
    {
        Task BroadcastNotificationAsync(string text, CancellationToken cancellationToken);
    }
}
