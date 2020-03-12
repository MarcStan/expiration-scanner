using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Rest;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Azure
{
    public class AzureManagementTokenProvider : ITokenProvider
    {
        private readonly string _tenantId;

        public AzureManagementTokenProvider(string tenantId)
        {
            _tenantId = tenantId;
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var token = await new AzureServiceTokenProvider().GetAccessTokenAsync("https://management.core.windows.net", _tenantId);
            return new AuthenticationHeaderValue("Bearer", token);
        }
    }
}