using ExpirationScanner.Logic.Notification;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner
{
    public class AggregatedNotificationService : INotificationService
    {
        private readonly INotificationService[] _notificationServices;

        public AggregatedNotificationService(
            IEnumerable<INotificationService> notificationServices)
        {
            _notificationServices = notificationServices.ToArray();
        }

        public bool IsActive => _notificationServices.Any(x => x.IsActive);

        public Task SendNotificationAsync(string text, CancellationToken cancellationToken)
            => Task.WhenAll(_notificationServices
                .Where(service => service.IsActive)
                .Select(service => service.SendNotificationAsync(text, cancellationToken)));
    }
}
