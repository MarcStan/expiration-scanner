using ExpirationScanner.Azure;
using ExpirationScanner.Services;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Endpoints.KeyVaultCheck
{
    public class KeyVaultCheckFunctions
    {
        private readonly IConfiguration config;
        private readonly ISlackService slackService;
        private readonly AzureManagementOptions azureManagementOptions;
        private readonly AzureManagementTokenProvider azureManagementTokenProvider;

        public KeyVaultCheckFunctions(
            IConfiguration config,
            ISlackService slackService,
            IOptionsSnapshot<AzureManagementOptions> azureManagementOptionsSnapshot,
            AzureManagementTokenProvider azureManagementTokenProvider)
        {
            this.config = config;
            this.slackService = slackService;
            this.azureManagementOptions = azureManagementOptionsSnapshot.Value;
            this.azureManagementTokenProvider = azureManagementTokenProvider;
        }

        [FunctionName("CheckVaultExpiry")]
        public async Task Run(
            [TimerTrigger("0 0 8 * * *"
#if DEBUG
            // , RunOnStartup=true
#endif
            )]TimerInfo myTimer,
            ILogger log)
        {
            var subscription = azureManagementOptions.SubscriptionId;

            var ignoreFilter = (config["IGNORED_KEYVAULTS"] ?? string.Empty).Split(',').Select(v => v.Trim());

            var certificateExpiryWarningInDays = int.Parse(config["CERTIFICATE_WARNING_THRESHOLD"] ?? "30");
            var secretExpiryWarningInDays = int.Parse(config["SECRET_WARNING_THRESHOLD"] ?? "30");

            var factory = new MSITokenProviderFactory(new MSILoginInformation(MSIResourceType.AppService));
            KeyVaultClient kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(async (authority, resource, scope) =>
            {
                return (await factory.Create(resource).GetAuthenticationHeaderAsync(default(CancellationToken))).Parameter;
            }));

            var tokenCredentials = new TokenCredentials(azureManagementTokenProvider);

            var azureCredentials = new AzureCredentials(
                    tokenCredentials,
                    tokenCredentials,
                    azureManagementOptions.TenantId,
                    AzureEnvironment.AzureGlobalCloud);

            var client = RestClient
                    .Configure()
                    .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .WithCredentials(azureCredentials)
                    .Build();

            var azure = Microsoft.Azure.Management.Fluent.Azure
                .Authenticate(client, azureManagementOptions.TenantId)
                .WithSubscription(azureManagementOptions.SubscriptionId);

            var errors = new List<String>();

            await foreach (var vault in azure.Vaults.ToAsyncEnumerable())
            {
                if (ignoreFilter.Contains(vault.Name))
                {
                    continue;
                }

                var keyVaultWarning = new KeyVaultWarning(vault);

                var certificates = new List<CertificateItem>();
                IPage<CertificateItem> certificatesPage = null;
                try
                {
                    certificatesPage = await kvClient.GetCertificatesAsync(vault.VaultUri);
                    certificates.AddRange(certificatesPage.ToList());
                }
                catch (KeyVaultErrorException ex)
                {
                    errors.Add($"Error: Could Not Access Vault Certificates {vault.Name} - {ex.Message}");
                }

                while (certificatesPage?.NextPageLink != null)
                {
                    certificatesPage = await kvClient.GetCertificatesNextAsync(certificatesPage?.NextPageLink);
                }

                keyVaultWarning.ExpiringCertificates = certificates
                    .Where(c => c.Attributes.Expires != null && c.Attributes.Expires < DateTime.UtcNow.AddDays(-certificateExpiryWarningInDays));

                var secrets = new List<SecretItem>();
                IPage<SecretItem> secretsPage = null;
                try
                {
                    secretsPage = await kvClient.GetSecretsAsync(vault.VaultUri);
                    secrets.AddRange(secretsPage.ToList());
                }
                catch (KeyVaultErrorException ex)
                {
                    errors.Add($"Error: Could Not Access Vault Secrets {vault.Name} - {ex.Message}");
                }

                while (secretsPage?.NextPageLink != null)
                {
                    secretsPage = await kvClient.GetSecretsNextAsync(secretsPage?.NextPageLink);
                }

                var unmanagedSecrets = secrets.Where(s => s.Managed != true && s.ContentType != "application/x-pkcs12");
                keyVaultWarning.ExpiringSecrets = unmanagedSecrets
                    .Where(c => c.Attributes.Expires != null && c.Attributes.Expires < DateTime.UtcNow.AddDays(secretExpiryWarningInDays));
                var legacyCertificates = secrets.Where(s => s.Managed != true && s.ContentType == "application/x-pkcs12");
                keyVaultWarning.ExpiringLegacyCertificates = legacyCertificates
                    .Where(c => c.Attributes.Expires != null && c.Attributes.Expires < DateTime.UtcNow.AddDays(certificateExpiryWarningInDays));

                if (keyVaultWarning.ExpiringCertificates.Any() || keyVaultWarning.ExpiringLegacyCertificates.Any() || keyVaultWarning.ExpiringSecrets.Any())
                {
                    var sbSlack = new StringBuilder();
                    sbSlack.AppendLine($"🔑 KeyVault {vault.Name} has entries about to expire");

                    if (keyVaultWarning.ExpiringCertificates.Any() || keyVaultWarning.ExpiringLegacyCertificates.Any())
                    {
                        sbSlack.AppendLine("Certificates:");
                        foreach (var cert in keyVaultWarning.ExpiringCertificates)
                        {
                            sbSlack.AppendLine($"\t•{(cert.Attributes.Expires < DateTime.UtcNow ? " ⚠️ EXPIRED ⚠️" : "")} {cert.Identifier.Name} - Created: {cert.Attributes.Created}\tExpires: {cert.Attributes.Expires}");

                        }
                        foreach (var cert in keyVaultWarning.ExpiringLegacyCertificates)
                        {
                            sbSlack.AppendLine($"\t•{(cert.Attributes.Expires < DateTime.UtcNow ? " ⚠️ EXPIRED ⚠️" : "")} {cert.Identifier.Name} - Created: {cert.Attributes.Created}\tExpires: {cert.Attributes.Expires}");
                        }
                    }
                    if (keyVaultWarning.ExpiringSecrets.Any())
                    {
                        sbSlack.AppendLine("Secrets:");
                        foreach (var secret in keyVaultWarning.ExpiringSecrets)
                        {
                            sbSlack.AppendLine($"\t•{(secret.Attributes.Expires < DateTime.UtcNow ? " ⚠️ EXPIRED ⚠️" : "")} {secret.Identifier.Name} {(!string.IsNullOrWhiteSpace(secret.ContentType) ? $"({secret.ContentType})" : "")} - Created: {secret.Attributes.Created}, Expires: {secret.Attributes.Expires}");
                        }
                    }

                    await slackService.SendSlackMessageAsync(sbSlack.ToString());
                }
            }

            await slackService.SendSlackMessageAsync(string.Join("\n", errors));
        }
    }
}
