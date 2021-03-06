using ExpirationScanner.Azure;
using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Logic.Extensions;
using ExpirationScanner.Logic.Notification;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic
{
    public class AppRegistrationExpiryChecker
    {
        private readonly IAzureHelper _azureHelper;
        private readonly INotificationStrategy _notificationService;
        private readonly ILogger<AppRegistrationExpiryChecker> _logger;

        public AppRegistrationExpiryChecker(
            IAzureHelper azureHelper,
            INotificationStrategy notificationService,
            ILogger<AppRegistrationExpiryChecker> logger)
        {
            _azureHelper = azureHelper;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task CheckAsync(string[] appFilter, int certificateExpiryWaringInDays, int secretExpiryWaringInDays, CancellationToken cancellationToken)
        {
            var tenantId = await _azureHelper.GetTenantIdAsync(cancellationToken);

            var graphServiceClient = new GraphServiceClient(new GraphApiTokenProvider(tenantId));
            var stringBuilder = new StringBuilder();
            var now = DateTimeOffset.UtcNow;

            await foreach (var app in graphServiceClient.Applications.Request().ToAsyncEnumerable())
            {
                if (!InWhitelist(app.DisplayName, appFilter))
                    continue;

                _logger.LogInformation($"Processing app registration {app.DisplayName}");

                var expiringSecrets = app.PasswordCredentials
                    .Where(k => k.EndDateTime.HasValue && k.EndDateTime <= now.AddDays(secretExpiryWaringInDays))
                    .ToArray();
                var expiringCertificates = app.KeyCredentials
                    .Where(k => k.EndDateTime.HasValue && k.EndDateTime <= now.AddDays(certificateExpiryWaringInDays))
                    .ToArray();

                if (expiringCertificates.Any() ||
                    expiringSecrets.Any())
                {
                    var warning = new ServicePrincipalWarning
                    {
                        ExpiringCertificates = expiringCertificates,
                        ExpiringSecrets = expiringSecrets
                    };

                    stringBuilder
                        .Append("Application ")
                        .Append(app.DisplayName)
                        .Append(" in tenant ")
                        .Append(tenantId)
                        .AppendLine(" has credentials about to expire:");

                    foreach (var certificate in warning.ExpiringCertificates)
                    {
                        var description = Convert.ToBase64String(certificate.CustomKeyIdentifier);
                        stringBuilder
                            .Append("\t• Certificate: ")
                            .Append(certificate.EndDateTime <= now ? "⚠️ EXPIRED ⚠️" : "")
                            .Append(' ').Append(description)
                            .Append(" - Created: ")
                            .Append(certificate.StartDateTime?.ToString("g"))
                            .Append("\t Expires: ")
                            .AppendLine(certificate.EndDateTime?.ToString("g"));
                    }

                    foreach (var secret in warning.ExpiringSecrets)
                    {
                        var description = secret.CustomKeyIdentifier != null ? Encoding.Unicode.GetString(secret.CustomKeyIdentifier) : "No description";
                        stringBuilder
                            .Append("\t• Secret: ")
                            .Append(secret.EndDateTime <= now ? "⚠️ EXPIRED ⚠️" : "")
                            .Append(' ')
                            .Append(description)
                            .Append(" - Created: ")
                            .Append(secret.StartDateTime?.ToString("g")).Append("\t Expires: ")
                            .AppendLine(secret.EndDateTime?.ToString("g"));
                    }
                }
            }
            if (stringBuilder.Length > 0)
                await _notificationService.BroadcastNotificationAsync(stringBuilder.ToString(), cancellationToken);
        }

        private bool InWhitelist(string name, string[] appFilter)
            => WhitelistHelper.Matches(name, appFilter);
    }
}
