using Microsoft.Rest;
using System.Threading;
using System.Threading.Tasks;

namespace ExpirationScanner.Logic.Azure
{
    public interface IAzureHelper
    {
        string GetSubscriptionId();

        Task<string> GetTenantIdAsync(CancellationToken cancellationToken);

        Task<ITokenProvider> GetAuthenticatedARMClientAsync(CancellationToken cancellationToken);
    }
}
