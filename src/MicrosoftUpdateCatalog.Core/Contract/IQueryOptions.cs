using MicrosoftUpdateCatalog.Core.Enums;

namespace MicrosoftUpdateCatalog.Core.Contract
{
    public interface IQueryOptions
    {
        SortBy GetSortOrder();

        SortDirection GetSortDirection();

        int GetMaxResults();

        bool ShouldIgnoreDuplicates();
    }
}
