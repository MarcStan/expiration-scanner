using Microsoft.Graph;
using System.Collections.Generic;

namespace ExpirationScanner.Logic.Extensions
{
    public static class AzureGraphExtensions
    {
        public static async IAsyncEnumerable<Application> ToAsyncEnumerable(this IGraphServiceApplicationsCollectionRequest request)
        {
            var nextPageRequest = request;

            while (nextPageRequest != null)
            {
                var page = await nextPageRequest.GetAsync();
                foreach (var item in page.CurrentPage)
                {
                    yield return item;
                }
                nextPageRequest = page.NextPageRequest;
            }
        }
    }
}