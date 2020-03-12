using ExpirationScanner.Azure;
using ExpirationScanner.Logic.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic.Azure
{
    /// <summary>
    /// Helpers to get well known ids from azure.
    /// </summary>
    public class AzureHelper : IAzureHelper
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private ConcurrentDictionary<string, string> _tenantIdLookup = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="handler">Used for testing overrides</param>
        public AzureHelper(
            IConfiguration configuration,
            HttpMessageHandler handler = null)
        {
            _httpClient = new HttpClient(handler ?? new HttpClientHandler());
            _configuration = configuration;
        }

        public string GetSubscriptionId()
            => _configuration.GetRequiredValue<string>("SubscriptionId");

        /// <summary>
        /// Returns the tenant id by making an unauthorized call to azure.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> GetTenantIdAsync(CancellationToken cancellationToken)
        {
            var subscriptionId = GetSubscriptionId();
            if (_tenantIdLookup.ContainsKey(subscriptionId))
                return _tenantIdLookup[subscriptionId];

            // unauthorized call yields header with tenant id
            // works because every subscription is only ever tied to one tenant (and MSI auth is limited to said tenant)
            // could have also added to config (like subscriptionId), but thisway user must only set one value
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}?api-version=2015-01-01";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var header = response.Headers.WwwAuthenticate.FirstOrDefault();
            var regex = new Regex("authorization_uri=\"https:\\/\\/login\\.windows\\.net\\/([A-Za-z0-9-]*)\"");
            var match = regex.Match(header.Parameter);
            if (!match.Success)
                throw new NotSupportedException("Azure endpoint failed to return the tenantId!");

            var tenantId = match.Groups[1].Value;
            _tenantIdLookup.AddOrUpdate(subscriptionId, tenantId, (key, old) => tenantId);
            return tenantId;
        }

        public async Task<ITokenProvider> GetAuthenticatedARMClientAsync(CancellationToken cancellationToken)
            => new AzureManagementTokenProvider(await GetTenantIdAsync(cancellationToken));
    }
}
