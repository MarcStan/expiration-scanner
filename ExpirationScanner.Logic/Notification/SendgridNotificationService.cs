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
        private const string _apiKeyKey = "Notification_SendGrid_Key", _fromKey = "Notification_SendGrid_From", _toKey = "Notification_SendGrid_To";

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
            var subject = _configuration.GetRequiredValue<string>("Notification_SendGrid_Subject");

            var client = new SendGridClient(key);
            var mail = MailHelper.CreateSingleEmail(new EmailAddress(from), new EmailAddress(to), subject ?? DefaultSubject, text, null);

            await client.SendEmailAsync(mail, cancellationToken);
        }
    }
}
