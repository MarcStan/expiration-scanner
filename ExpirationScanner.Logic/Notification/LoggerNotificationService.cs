using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic.Notification
{
    public class LoggerNotificationService : INotificationService
    {
        private readonly ILogger _logger;

        public LoggerNotificationService(
            ILogger logger)
        {
            _logger = logger;
        }

        public bool IsActive => true;

        public Task SendNotificationAsync(string text, CancellationToken cancellationToken)
        {
            _logger.LogInformation(text);
            return Task.CompletedTask;
        }
    }
}
