using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic.Notification
{
    public interface INotificationService
    {
        /// <summary>
        /// Returns true if the service is configured and will send notifications, false otherwise.
        /// Does not check if the configuration is valid.
        /// </summary>
        bool IsActive { get; }

        Task SendNotificationAsync(string text, CancellationToken cancellationToken);
    }
}
