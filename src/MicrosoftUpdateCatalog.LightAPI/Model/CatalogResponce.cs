using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MicrosoftUpdateCatalog.LightAPI.Model
{
    public class CatalogResponse
    {
        internal string EventArgument { get; set; } = null;

        internal string EventValidation { get; set; } = null;

        internal string SearchQueryUri { get; set; } = null;

        internal string ViewState { get; set; } = null;

        internal string ViewStateGenerator { get; set; } = null;

        [JsonConstructor]
        public CatalogResponse()
        {

        }

        public bool IsFinalPage { get; internal set; } = true;

        public int ResultsCount { get; internal set; } = default;

        public IEnumerable<CatalogEntry> SearchResults { get; set; } = Enumerable.Empty<CatalogEntry>();
    }
}