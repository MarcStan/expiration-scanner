using ExpirationScanner.Azure;
using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Services;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly IConfiguration _config;
        private readonly IAzureHelper _azureHelper;
        private readonly ISlackService _slackService;
        private readonly AzureManagementTokenProvider _azureManagementTokenProvider;

        public KeyVaultCheckFunctions(
            IConfiguration config,
            IAzureHelper azureHelper,
            ISlackService slackService,
            AzureManagementTokenProvider azureManagementTokenProvider)
        {
            _config = config;
            _azureHelper = azureHelper;
            _slackService = slackService;
            _azureManagementTokenProvider = azureManagementTokenProvider;
        }

        [FunctionName("CheckVaultExpiry")]
        public async Task Run(
            [TimerTrigger("0 0 8 * * *"
#if DEBUG
            // , RunOnStartup=true
#endif
            )]TimerInfo myTimer,
            ILogger log,
            CancellationToken cancellationToken)
        {
            var ignoreFilter = (_config["IGNORED_KEYVAULTS"] ?? string.Empty).Split(',').Select(v => v.Trim());

            var certificateExpiryWarningInDays = int.Parse(_config["CERTIFICATE_WARNING_THRESHOLD"] ?? "30");
            var secretExpiryWarningInDays = int.Parse(_config["SECRET_WARNING_THRESHOLD"] ?? "30");

            var factory = new MSITokenProviderFactory(new MSILoginInformation(MSIResourceType.AppService));
            KeyVaultClient kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(async (authority, resource, scope) =>
            {
                return (await factory.Create(resource).GetAuthenticationHeaderAsync(cancellationToken)).Parameter;
            }));

            var tokenCredentials = new TokenCredentials(_azureManagementTokenProvider);

            var tenantId = await _azureHelper.GetTenantIdAsync(cancellationToken);
            var subscriptionId = _azureHelper.GetSubscriptionId();

            var azureCredentials = new AzureCredentials(
                    tokenCredentials,
                    tokenCredentials,
                    tenantId,
                    AzureEnvironment.AzureGlobalCloud);

            var client = RestClient
                    .Configure()
                    .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .WithCredentials(azureCredentials)
                    .Build();

            var azure = Microsoft.Azure.Management.Fluent.Azure
                .Authenticate(client, tenantId)
                .WithSubscription(subscriptionId);

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

                    await _slackService.SendSlackMessageAsync(sbSlack.ToString());
                }
            }

            await _slackService.SendSlackMessageAsync(string.Join("\n", errors));
        }
    }
}
