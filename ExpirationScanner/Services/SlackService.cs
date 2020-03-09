using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Threading.Tasks;

namespace ExpirationScanner.Services
{
    public class SlackService : ISlackService
    {
        private readonly HttpClient _httpClient;
        private readonly SlackOptions _slackOptions;

        public SlackService(HttpClient httpClient, IOptionsSnapshot<SlackOptions> slackOptionsSnapshot)
        {
            _httpClient = httpClient;
            _slackOptions = slackOptionsSnapshot.Value;
        }

        public Task SendSlackMessageAsync(string text)
        {
            return _httpClient.PostAsJsonAsync(_slackOptions.SlackWebhookUrl, new { text });
        }
    }
}