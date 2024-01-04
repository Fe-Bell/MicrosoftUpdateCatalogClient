using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace MicrosoftUpdateCatalog.Core.Contract
{
    public interface ICatalogClient
    {
        Task<IEnumerable<ICatalogEntry>> SearchAsync(string query, IQueryOptions options = null, CancellationToken cancellationToken = default);
    }
}
