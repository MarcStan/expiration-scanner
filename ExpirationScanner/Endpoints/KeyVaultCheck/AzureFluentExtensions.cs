using Microsoft.Azure.Management.ResourceManager.Fluent.Core.CollectionActions;
using System.Collections.Generic;

namespace ExpirationScanner.Endpoints.KeyVaultCheck
{
    public static class AzureFluentExtensions
    {
        public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this ISupportsListing<T> supportsListing)
        {
            var page = await supportsListing.ListAsync(loadAllPages: false);
            while (page != null)
            {
                foreach (var item in page)
                {
                    yield return item;
                }
                page = await page.GetNextPageAsync();
            }
        }
    }
}