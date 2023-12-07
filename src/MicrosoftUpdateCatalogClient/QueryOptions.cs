using MicrosoftUpdateCatalogClient.Enums;

namespace MicrosoftUpdateCatalogClient
{
    public struct QueryOptions
    {
        public SortBy SortOrder { get; set; } = SortBy.None;

        public SortDirection SortDirection { get; set; } = SortDirection.Descending;

        public QueryOptions()
        {
            
        }
    }
}
