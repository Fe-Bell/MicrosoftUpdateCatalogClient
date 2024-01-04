using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MicrosoftUpdateCatalog.Core.Contract
{
    public abstract class ACatalogClient :
        ICatalogClient
    {
        protected static readonly Uri BASE_URI = new("https://www.catalog.update.microsoft.com");

        public ACatalogClient()
        {
            
        }

        public abstract Task<IEnumerable<ICatalogEntry>> SearchAsync(string query, IQueryOptions options = null, CancellationToken cancellationToken = default);
    }
}
