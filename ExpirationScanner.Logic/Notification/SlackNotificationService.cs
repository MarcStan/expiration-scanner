using ExpirationScanner.Logic.Extensions;
using ExpirationScanner.Logic.Notification;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Services
{
    public class SlackNotificationService : INotificationService
    {
        private const string _webHookKey = "Notification_Slack_Webhook";
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public SlackNotificationService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public bool IsActive => !string.IsNullOrEmpty(_configuration[_webHookKey]);

        public Task SendNotificationAsync(string text, CancellationToken cancellationToken)
        {
            if (!IsActive)
                return Task.CompletedTask;

            var webHookUrl = _configuration[_webHookKey];

            return _httpClient.PostAsJsonAsync(webHookUrl, new { text }, cancellationToken);
        }
    }
}