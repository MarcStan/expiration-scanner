using ExpirationScanner.Logic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Functions
{
    public class KeyVaultExpiry
    {
        private readonly IConfiguration _configuration;
        private readonly KeyVaultExpiryChecker _keyVaultExpiryChecker;

        public KeyVaultExpiry(
            IConfiguration configuration,
            KeyVaultExpiryChecker keyVaultExpiryChecker)
        {
            _configuration = configuration;
            _keyVaultExpiryChecker = keyVaultExpiryChecker;
        }

        [FunctionName("keyvault-expiry")]
        public async Task Run(
            [TimerTrigger(Constants.KeyVaultExpirySchedule
#if DEBUG
            , RunOnStartup = true
#endif
            )] TimerInfo myTimer,
            CancellationToken cancellationToken)
        {
            var certificateExpiryWarningInDays = int.Parse(_configuration["KeyVault_Certificate_WarningThresholdInDays"] ?? Constants.DefaultExpiry.ToString());
            var secretExpiryWarningInDays = int.Parse(_configuration["KeyVault_Secret_WarningThresholdInDays"] ?? Constants.DefaultExpiry.ToString());

            var whitelist = _configuration["KeyVault_Whitelist"];
            var keyVaultFilter = (string.IsNullOrEmpty(whitelist) ? "*" : whitelist)
                .Split(',')
                .Select(v => v.Trim())
                .ToArray();

            await _keyVaultExpiryChecker.CheckAsync(keyVaultFilter, certificateExpiryWarningInDays, secretExpiryWarningInDays, cancellationToken);
        }
    }
}
