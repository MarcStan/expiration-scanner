using ExpirationScanner.Graph;
using ExpirationScanner.Services;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpirationScanner.Endpoints.ServicePrincipalCheck
{
    public class ServicePrincipalCheckFunctions
    {
        private readonly IConfiguration config;
        private readonly ISlackService slackService;
        private readonly TenantOptions tenantOptions;

        public ServicePrincipalCheckFunctions(
            IConfiguration config,
            IOptionsSnapshot<TenantOptions> tenantOptionsSapshot,
            ISlackService slackService)
        {
            this.config = config;
            this.tenantOptions = tenantOptionsSapshot.Value;
            this.slackService = slackService;
        }

        [FunctionName("CheckServicePrincipalExpiry")]
        public async Task Run(
            [TimerTrigger("0 0 8 * * *"
#if DEBUG
            // , RunOnStartup=true
#endif
            )]TimerInfo myTimer,
            ILogger log)
        {
            var secretExpiryWaringInDays = int.Parse(config["SECRET_WARNING_THRESHOLD"] ?? "30");
            var ignoreFilter = (config["IGNORED_APPS"] ?? string.Empty).Split(',').Select(v => v.Trim());

            if ((tenantOptions.Tenants ?? Array.Empty<TenantAccessor>()).Length == 0)
            {
                log.LogWarning("No tenants configured to scan for expired service principal credentials");
            }

            foreach (var tenant in tenantOptions.Tenants)
            {
                IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                    .Create(tenant.ClientId)
                    .WithTenantId(tenant.TenantId)
                    .WithClientSecret(tenant.ClientSecret)
                    .Build();

                ClientCredentialProvider authProvider = new ClientCredentialProvider(confidentialClientApplication);

                var graphServiceClient = new GraphServiceClient(authProvider);

                await foreach (var app in graphServiceClient.Applications.Request().Expand("owners").ToAsyncEnumerable())
                {
                    if (!IsServicePrincipalAppOwner(app, tenant.ClientId) || ignoreFilter.Contains(app.DisplayName))
                    {
                        continue;
                    }

                    var expiringCertificates = app.KeyCredentials.Where(k => k.EndDateTime < DateTime.Now.AddDays(secretExpiryWaringInDays));
                    var expiringSecrets = app.PasswordCredentials.Where(k => k.EndDateTime < DateTime.Now.AddDays(secretExpiryWaringInDays));

                    if (expiringCertificates.Any() || expiringSecrets.Any())
                    {
                        var warning = new ServicePrincipalWarning(app);
                        warning.ExpiringCertificates = expiringCertificates;
                        warning.ExpiringSecrets = expiringSecrets;

                        var sbSlack = new StringBuilder();
                        sbSlack.AppendLine($"Application {app.DisplayName} in Tenant {tenant.TenantId} has credentials about to expire:");

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

                        await slackService.SendSlackMessageAsync(sbSlack.ToString());
                    }
                }
            }
        }

        private bool IsServicePrincipalAppOwner(Application app, string servicePrincipalApplicationId)
        {
            return app.Owners.Any(o =>
                    {
                        if (o is ServicePrincipal sp)
                        {
                            return sp.AppId == servicePrincipalApplicationId;
                        }
                        else
                        {
                            return false;
                        }
                    });
        }
    }
}
