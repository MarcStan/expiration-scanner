using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Azure
{
    public class AzureManagementTokenProvider : ITokenProvider
    {
        private static Lazy<AzureServiceTokenProvider> _lazyAzureServiceTokenProvider =
            new Lazy<AzureServiceTokenProvider>(() => new AzureServiceTokenProvider(null, "https://login.microsoftonline.com/"));

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var token = await _lazyAzureServiceTokenProvider.Value.GetAccessTokenAsync("https://management.core.windows.net");
            return new AuthenticationHeaderValue("Bearer", token);
        }
    }
}