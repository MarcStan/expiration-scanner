using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Logic.Notification;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Endpoints.ServicePrincipalCheck
{
    public class ServicePrincipalCheckFunctions
    {
        private readonly IConfiguration _configuration;
        private readonly IAzureHelper _azureHelper;
        private readonly INotificationService _notificationService;

        public ServicePrincipalCheckFunctions(
            IConfiguration configuration,
            IAzureHelper azureHelper,
            INotificationService notificationService)
        {
            _configuration = configuration;
            _azureHelper = azureHelper;
            _notificationService = notificationService;
        }

        [FunctionName("CheckServicePrincipalExpiry")]
        public async Task Run(
            [TimerTrigger("0 0 8 * * *"
#if DEBUG
            // , RunOnStartup=true
#endif
            )]TimerInfo myTimer,
            ILogger log,
            CancellationToken cancellationToken)
        {
            var secretExpiryWaringInDays = int.Parse(_configuration["SECRET_WARNING_THRESHOLD"] ?? "30");
            var appFilter = (_configuration["ApplicationWhitelist"] ?? string.Empty).Split(',').Select(v => v.Trim());

            var tenantId = await _azureHelper.GetTenantIdAsync(cancellationToken);
            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create("TODO")
                .WithTenantId(tenantId)
                .WithClientSecret("TODO")
                .Build();

            ClientCredentialProvider authProvider = new ClientCredentialProvider(confidentialClientApplication);

            var graphServiceClient = new GraphServiceClient(authProvider);

            await foreach (var app in graphServiceClient.Applications.Request().Expand("owners").ToAsyncEnumerable())
            {
                if (appFilter.Contains(app.DisplayName))
                    continue;

                var expiringCertificates = app.KeyCredentials.Where(k => k.EndDateTime < DateTime.Now.AddDays(secretExpiryWaringInDays));
                var expiringSecrets = app.PasswordCredentials.Where(k => k.EndDateTime < DateTime.Now.AddDays(secretExpiryWaringInDays));

                if (expiringCertificates.Any() || expiringSecrets.Any())
                {
                    var warning = new ServicePrincipalWarning(app);
                    warning.ExpiringCertificates = expiringCertificates;
                    warning.ExpiringSecrets = expiringSecrets;

                    var sbSlack = new StringBuilder();
                    sbSlack.AppendLine($"Application {app.DisplayName} in Tenant {tenantId} has credentials about to expire:");

                    foreach (var certificate in warning.ExpiringCertificates)
                    {
                        var description = Convert.ToBase64String(certificate.CustomKeyIdentifier);
                        sbSlack.AppendLine($"\t• Certificate: {(certificate.EndDateTime < DateTime.UtcNow ? "⚠️ EXPIRED ⚠️" : "")} {description} - Created: {certificate.StartDateTime?.ToString("g")}\t Expires: {certificate.EndDateTime?.ToString("g")}");
                    }

                    foreach (var secret in warning.ExpiringSecrets)
                    {
                        var description = secret.CustomKeyIdentifier != null ? Encoding.Unicode.GetString(secret.CustomKeyIdentifier) : "No description";
                        sbSlack.AppendLine($"\t• Secret: {(secret.EndDateTime < DateTime.UtcNow ? "⚠️ EXPIRED ⚠️" : "")} {description} - Created: {secret.StartDateTime?.ToString("g")}\t Expires: {secret.EndDateTime?.ToString("g")}");
                    }

                    await _notificationService.SendNotificationAsync(sbSlack.ToString(), cancellationToken);
                }
            }
        }
    }
}
