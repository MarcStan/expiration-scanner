using ExpirationScanner.Logic.Extensions;
using Microsoft.Extensions.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic.Notification
{
    public class SendGridNotificationService : INotificationService
    {
        private const string DefaultSubject = "Expiry notification";
        private const string _apiKeyKey = "Notification:SendGrid:Key", _fromKey = "Notification:SendGrid:From", _toKey = "Notification:SendGrid:To";

        private readonly IConfiguration _configuration;

        public SendGridNotificationService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool IsActive =>
            !string.IsNullOrEmpty(_configuration[_apiKeyKey]) &&
            !string.IsNullOrEmpty(_configuration[_fromKey]) &&
            !string.IsNullOrEmpty(_configuration[_toKey]);

        public async Task SendNotificationAsync(string text, CancellationToken cancellationToken)
        {
            if (!IsActive)
                return;

            var key = _configuration[_apiKeyKey];
            var from = _configuration.GetRequiredValue<string>(_fromKey);
            var to = _configuration.GetRequiredValue<string>(_toKey);

            // optional
            var subject = _configuration.GetValue<string>("Notification:SendGrid:Subject") ?? DefaultSubject;

            var client = new SendGridClient(key);
            var mail = MailHelper.CreateSingleEmail(new EmailAddress(from), new EmailAddress(to), subject, text, null);

            await client.SendEmailAsync(mail, cancellationToken);
        }
    }
}
