using System.Collections.Generic;

namespace MicrosoftUpdateCatalog.Core.Contract
{
    public interface ICatalogResponse
    {
        IEnumerable<ICatalogEntry> GetResults();

        long GetResultsCount();

        bool IsLastPage();
    }
}
