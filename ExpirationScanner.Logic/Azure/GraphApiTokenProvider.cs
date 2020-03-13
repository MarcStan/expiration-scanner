using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Graph;
using Microsoft.Rest;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Azure
{
    public class GraphApiTokenProvider : IAuthenticationProvider, ITokenProvider
    {
        private readonly string _tenantId;

        public GraphApiTokenProvider(string tenantId)
        {
            _tenantId = tenantId;
        }

        public async Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            request.Headers.Authorization = await GetAuthenticationHeaderAsync(CancellationToken.None);
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var token = await new AzureServiceTokenProvider().GetAccessTokenAsync("https://graph.microsoft.com/", _tenantId);
            return new AuthenticationHeaderValue("Bearer", token);
        }
    }
}