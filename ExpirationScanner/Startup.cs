using ExpirationScanner.Azure;
using ExpirationScanner.Extensions;
using ExpirationScanner.Graph;
using ExpirationScanner.Services;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(ExpirationScanner.Startup))]

namespace ExpirationScanner
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var executionContextOptions = builder.Services.BuildServiceProvider().GetService<IOptions<ExecutionContextOptions>>().Value;
            var appDirectory = executionContextOptions.AppDirectory;

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            builder.Services.AddSingleton(config);

            builder.Services.AddHttpClient();

            builder.Services.Configure<AzureManagementOptions>(config.GetSection("AzureManagement"));
            builder.Services.Configure<TenantOptions>(config.GetSection("TenantConfig"));

            builder.Services.AddSingleton<AzureManagementTokenProvider>();

            builder.Services.AddOptions<SlackOptions>().Configure(o => o.SlackWebhookUrl = config.GetRequiredValue<string>("SLACK_WEBHOOK_URL"));
            builder.Services.AddHttpClient<ISlackService, SlackService>();
        }
    }
}