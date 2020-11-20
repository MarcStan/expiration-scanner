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
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Net.Http;

[assembly: FunctionsStartup(typeof(ExpirationScanner.Startup))]
namespace ExpirationScanner
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            InjectKeyVaultIntoConfiguration(builder);

            builder.Services.Scan(scan =>
            {
                scan.FromAssemblyOf<INotificationService>()
                    .AddClasses(classes => classes.AssignableTo<INotificationService>())
                    .AsSelfWithInterfaces()
                    .WithSingletonLifetime();
            });
            builder.Services.AddSingleton(new HttpClient());
            builder.Services.AddSingleton<IAzureHelper, AzureHelper>();
            builder.Services.AddSingleton<INotificationStrategy, AggregatedNotificationService>();
            builder.Services.AddSingleton<AzureManagementTokenProvider>();
            builder.Services.AddSingleton<KeyVaultExpiryChecker>();
            builder.Services.AddSingleton<AppRegistrationExpiryChecker>();
        }

        private void InjectKeyVaultIntoConfiguration(IFunctionsHostBuilder builder)
        {
            // https://stackoverflow.com/a/60349484
            var serviceProvider = builder.Services.BuildServiceProvider();
            var configurationRoot = serviceProvider.GetService<IConfiguration>();
            var configurationBuilder = new ConfigurationBuilder();

            if (configurationRoot is IConfigurationRoot)
            {
                configurationBuilder.AddConfiguration(configurationRoot);
            }

            var tokenProvider = new AzureServiceTokenProvider();
            var kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));
            var keyVaultName = configurationRoot["KeyVaultName"];
            if (string.IsNullOrEmpty(keyVaultName))
                throw new NotSupportedException("KeyVaultName must be set as an environment variable");

            configurationBuilder.AddAzureKeyVault($"https://{keyVaultName}.vault.azure.net", kvClient, new DefaultKeyVaultSecretManager());

            var configuration = configurationBuilder.Build();

            builder.Services.Replace(ServiceDescriptor.Singleton(typeof(IConfiguration), configuration));
        }
    }
}