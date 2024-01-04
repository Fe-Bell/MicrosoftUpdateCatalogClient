using MicrosoftUpdateCatalog.Core.Contract;
using MicrosoftUpdateCatalog.Core.Enums;

namespace MicrosoftUpdateCatalog.LightAPI
{
    public struct QueryOptions :
        IQueryOptions
    {
        public SortBy SortOrder { get; set; } = SortBy.None;

        public SortDirection SortDirection { get; set; } = SortDirection.Descending;

        public int MaxResults { get; set; } = int.MaxValue;

        public bool IgnoreDuplicates { get; set; } = true;

        public QueryOptions()
        {

        }

        public readonly SortBy GetSortOrder()
            => SortOrder;

        public readonly SortDirection GetSortDirection()
            => SortDirection;

        public readonly int GetMaxResults()
            => MaxResults;

        public readonly bool ShouldIgnoreDuplicates()
            => IgnoreDuplicates;
    }
}
