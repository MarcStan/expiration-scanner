using ExpirationScanner.Logic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Functions
{
    public class AppRegistrationExpiry
    {
        private readonly IConfiguration _configuration;
        private readonly AppRegistrationExpiryChecker _appRegistrationExpiryChecker;

        public AppRegistrationExpiry(
            IConfiguration configuration,
            AppRegistrationExpiryChecker appRegistrationExpiryChecker)
        {
            _configuration = configuration;
            _appRegistrationExpiryChecker = appRegistrationExpiryChecker;
        }

        [FunctionName("appregistration-expiry")]
        public async Task Run(
            [TimerTrigger(Constants.AppRegistrationCredentialExpirySchedule
#if DEBUG
            , RunOnStartup = true
#endif
            )]TimerInfo myTimer,
            ILogger log,
            CancellationToken cancellationToken)
        {
            var secretExpiryWaringInDays = int.Parse(_configuration["AppRegistration_Secret_WarningThresholdInDays"] ?? Constants.ExpiryWarningThresholdInDays.ToString());
            var certificateExpiryWaringInDays = int.Parse(_configuration["AppRegistration_Certificate_WarningThresholdInDays"] ?? Constants.ExpiryWarningThresholdInDays.ToString());
            var whitelist = _configuration["AppRegistration_Whitelist"];
            var appFilter = (string.IsNullOrEmpty(whitelist) ? "*" : whitelist)
                .Split(',')
                .Select(v => v.Trim())
                .ToArray();

            await _appRegistrationExpiryChecker.CheckAsync(appFilter, certificateExpiryWaringInDays, secretExpiryWaringInDays, cancellationToken);
        }
    }
}
