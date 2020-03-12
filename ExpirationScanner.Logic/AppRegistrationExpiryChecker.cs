using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Logic.Extensions;
using ExpirationScanner.Logic.Notification;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
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

        public AppRegistrationExpiryChecker(
            IAzureHelper azureHelper,
            INotificationStrategy notificationService)
        {
            _azureHelper = azureHelper;
            _notificationService = notificationService;
        }

        public async Task CheckAsync(string[] appFilter, int certificateExpiryWaringInDays, int secretExpiryWaringInDays, CancellationToken cancellationToken)
        {
            var tenantId = await _azureHelper.GetTenantIdAsync(cancellationToken);
            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create("TODO")
                .WithTenantId(tenantId)
                .WithClientSecret("TODO")
                .Build();

            var authProvider = new ClientCredentialProvider(confidentialClientApplication);

            var graphServiceClient = new GraphServiceClient(authProvider);

            await foreach (var app in graphServiceClient.Applications.Request().ToAsyncEnumerable())
            {
                if (Matches(app.DisplayName, appFilter))
                    continue;

                var now = DateTimeOffset.UtcNow;

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

                    var stringBuilder = new StringBuilder();
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

                    await _notificationService.BroadcastNotificationAsync(stringBuilder.ToString(), cancellationToken);
                }
            }
        }

        private bool Matches(string name, string[] appFilter)
            => WhitelistHelper.Matches(name, appFilter);
    }
}
