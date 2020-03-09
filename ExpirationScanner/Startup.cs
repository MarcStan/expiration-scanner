using ExpirationScanner.Azure;
using ExpirationScanner.Logic.Notification;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(ExpirationScanner.Startup))]
namespace ExpirationScanner
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            builder.Services.AddSingleton(config);
            builder.Services.AddHttpClient();
            builder.Services.Scan(scan =>
            {
                scan.FromAssemblyOf<INotificationService>()
                .AddClasses(classes => classes.AssignableTo<INotificationService>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime();
            });
            builder.Services.AddSingleton<INotificationService, AggregatedNotificationService>();
            builder.Services.AddSingleton<AzureManagementTokenProvider>();
        }
    }
}