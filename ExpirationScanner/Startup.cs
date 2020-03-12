using ExpirationScanner.Azure;
using ExpirationScanner.Logic;
using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Logic.Notification;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureKeyVault;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

[assembly: FunctionsStartup(typeof(ExpirationScanner.Startup))]
namespace ExpirationScanner
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var keyVaultName = new ConfigurationBuilder()
                .AddJsonFile("local.settings.json", optional: true)
                .AddEnvironmentVariables()
                .Build()["KeyVaultName"];

            var configBuilder = new ConfigurationBuilder();
            if (!string.IsNullOrEmpty(keyVaultName))
            {
                var tokenProvider = new AzureServiceTokenProvider();
                var kvClient = new KeyVaultClient((authority, resource, scope)
                    => tokenProvider.KeyVaultTokenCallback(authority, resource, scope));

                configBuilder
                    .AddAzureKeyVault($"https://{keyVaultName}.vault.azure.net", kvClient, new DefaultKeyVaultSecretManager())
                    .AddEnvironmentVariables();
            }
            var config = configBuilder.Build();

            builder.Services.AddSingleton(config);
            builder.Services.AddHttpClient();
            builder.Services.Scan(scan =>
            {
                scan.FromAssemblyOf<INotificationService>()
                    .AddClasses(classes => classes.AssignableTo<INotificationService>())
                    .AsSelf()
                    .WithSingletonLifetime();
            });
            builder.Services.AddSingleton<IAzureHelper, AzureHelper>();
            builder.Services.AddSingleton<INotificationService>(p
                => new AggregatedNotificationService(GetConfiguredNotificationServices(p), config, p.GetRequiredService<ILogger<AggregatedNotificationService>>()));
            builder.Services.AddSingleton<AzureManagementTokenProvider>();
            builder.Services.AddSingleton<KeyVaultExpiryChecker>();
            builder.Services.AddSingleton<AppRegistrationExpiryChecker>();
        }

        private IEnumerable<INotificationService> GetConfiguredNotificationServices(IServiceProvider serviceProvider)
        {
            // TODO: GetServices would contain them but would iterate 180+ services
            var serviceTypes = new[]
            {
                typeof(SendGridNotificationService),
                typeof(SlackNotificationService)
            };

            foreach (var serviceType in serviceTypes)
            {
                var instance = (INotificationService)serviceProvider.GetRequiredService(serviceType);
                if (instance.IsActive)
                    yield return instance;
            }
        }
    }
}