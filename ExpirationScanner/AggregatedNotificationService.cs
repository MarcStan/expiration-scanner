using ExpirationScanner.Logic.Notification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner
{
    public class AggregatedNotificationService : INotificationService
    {
        private readonly INotificationService[] _notificationServices;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AggregatedNotificationService> _logger;

        public AggregatedNotificationService(
            IEnumerable<INotificationService> notificationServices,
            IConfiguration configuration,
            ILogger<AggregatedNotificationService> logger)
        {
            _notificationServices = notificationServices.ToArray();

            if (_notificationServices.Length == 0)
                throw new InvalidOperationException($"At least one {typeof(INotificationService)} must be configured for the function to run as intended but none where.");
            _configuration = configuration;
            _logger = logger;
        }

        public bool IsActive => _notificationServices.Any(x => x.IsActive);

        public async Task SendNotificationAsync(string text, CancellationToken cancellationToken)
        {
            if (!"true".Equals(_configuration["Notificaton_Logger_Disable"], StringComparison.OrdinalIgnoreCase))
                _logger.LogInformation(text);

            await Task.WhenAll(_notificationServices
                  .Where(service => service.IsActive)
                  .Select(service => service.SendNotificationAsync(text, cancellationToken)));
        }
    }
}
