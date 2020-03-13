using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Logic.Extensions;
using ExpirationScanner.Logic.Notification;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic
{
    public class KeyVaultExpiryChecker
    {
        private readonly IAzureHelper _azureHelper;
        private readonly INotificationStrategy _notificationService;
        private readonly ILogger<KeyVaultExpiryChecker> _logger;

        public KeyVaultExpiryChecker(
            IAzureHelper azureHelper,
            INotificationStrategy notificationService,
            ILogger<KeyVaultExpiryChecker> logger)
        {
            _azureHelper = azureHelper;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task CheckAsync(string[] keyVaultFilter, int certificateExpiryWarningInDays, int secretExpiryWarningInDays, CancellationToken cancellationToken)
        {
            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback));

            var tokenProvider = await _azureHelper.GetAuthenticatedARMClientAsync(cancellationToken);
            var tokenCredentials = new TokenCredentials(tokenProvider);

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

            var errors = new List<string>();
            var builder = new StringBuilder();

            await foreach (var vault in azure.Vaults.ToAsyncEnumerable())
            {
                if (!InWhitelist(vault.Name, keyVaultFilter))
                    continue;

                var keyVaultWarning = new KeyVaultWarning(vault);

                _logger.LogInformation($"Processing keyvault {vault.Name}");

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
                    certificates.AddRange(certificatesPage.ToList());
                }

                keyVaultWarning.ExpiringCertificates = certificates
                    .Where(c => c.Attributes.Expires != null && c.Attributes.Expires < DateTime.UtcNow.AddDays(-certificateExpiryWarningInDays))
                    .ToArray();

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
                    secrets.AddRange(secretsPage.ToList());
                }

                var unmanagedSecrets = secrets.Where(s => s.Managed != true && s.ContentType != "application/x-pkcs12");
                keyVaultWarning.ExpiringSecrets = unmanagedSecrets
                    .Where(c => c.Attributes.Expires != null && c.Attributes.Expires < DateTime.UtcNow.AddDays(secretExpiryWarningInDays))
                    .ToArray();
                var legacyCertificates = secrets.Where(s => s.Managed != true && s.ContentType == "application/x-pkcs12");
                keyVaultWarning.ExpiringLegacyCertificates = legacyCertificates
                    .Where(c => c.Attributes.Expires != null && c.Attributes.Expires < DateTime.UtcNow.AddDays(certificateExpiryWarningInDays))
                    .ToArray();

                if (keyVaultWarning.ExpiringCertificates.Any() || keyVaultWarning.ExpiringLegacyCertificates.Any() || keyVaultWarning.ExpiringSecrets.Any())
                {
                    builder
                        .Append("🔑 KeyVault ")
                        .Append(vault.Name)
                        .AppendLine(" has entries about to expire");

                    if (keyVaultWarning.ExpiringCertificates.Any() || keyVaultWarning.ExpiringLegacyCertificates.Any())
                    {
                        builder.AppendLine("Certificates:");
                        foreach (var cert in keyVaultWarning.ExpiringCertificates)
                        {
                            builder
                                .Append("\t•")
                                .Append(cert.Attributes.Expires < DateTime.UtcNow ? " ⚠️ EXPIRED ⚠️" : "")
                                .Append(' ')
                                .Append(cert.Identifier.Name).Append(" - Created: ")
                                .Append(cert.Attributes.Created)
                                .Append("\tExpires: ")
                                .Append(cert.Attributes.Expires)
                                .AppendLine();
                        }
                        foreach (var cert in keyVaultWarning.ExpiringLegacyCertificates)
                        {
                            builder
                                .Append("\t•")
                                .Append(cert.Attributes.Expires < DateTime.UtcNow ? " ⚠️ EXPIRED ⚠️" : "")
                                .Append(' ').Append(cert.Identifier.Name)
                                .Append(" - Created: ")
                                .Append(cert.Attributes.Created)
                                .Append("\tExpires: ")
                                .Append(cert.Attributes.Expires)
                                .AppendLine();
                        }
                    }
                    if (keyVaultWarning.ExpiringSecrets.Any())
                    {
                        builder.AppendLine("Secrets:");
                        foreach (var secret in keyVaultWarning.ExpiringSecrets)
                        {
                            builder
                                .Append("\t•")
                                .Append(secret.Attributes.Expires < DateTime.UtcNow ? " ⚠️ EXPIRED ⚠️" : "")
                                .Append(' ')
                                .Append(secret.Identifier.Name)
                                .Append(' ')
                                .Append(!string.IsNullOrWhiteSpace(secret.ContentType) ? $"({secret.ContentType})" : "")
                                .Append(" - Created: ")
                                .Append(secret.Attributes.Created)
                                .Append(", Expires: ")
                                .Append(secret.Attributes.Expires)
                                .AppendLine();
                        }
                    }
                }
            }

            if (builder.Length > 0)
                await _notificationService.BroadcastNotificationAsync(builder.ToString(), cancellationToken);

            if (errors.Any())
                await _notificationService.BroadcastNotificationAsync(string.Join("\n", errors), cancellationToken);
        }

        private bool InWhitelist(string name, string[] keyVaultFilter)
            => WhitelistHelper.Matches(name, keyVaultFilter);
    }
}
