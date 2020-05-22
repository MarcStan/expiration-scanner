using ExpirationScanner.Azure;
using ExpirationScanner.Logic;
using ExpirationScanner.Logic.Azure;
using ExpirationScanner.Logic.Notification;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

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

            var configBuilder = new ConfigurationBuilder()
                .AddJsonFile("local.settings.json", optional: true);

            if (!string.IsNullOrEmpty(keyVaultName))
                configBuilder.AddAzureKeyVault($"https://{keyVaultName}.vault.azure.net");

            var config = configBuilder
                    .AddEnvironmentVariables()
                    .Build();

            builder.Services.AddSingleton<IConfiguration>(config);
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
    }
}