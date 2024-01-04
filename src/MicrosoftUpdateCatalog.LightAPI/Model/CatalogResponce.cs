using MicrosoftUpdateCatalog.Core.Contract;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MicrosoftUpdateCatalog.LightAPI.Model
{
    public class CatalogResponse :
        ICatalogResponse
    {
        internal string EventArgument { get; set; } = null;

        internal string EventValidation { get; set; } = null;

        internal string SearchQueryUri { get; set; } = null;

        internal string ViewState { get; set; } = null;

        internal string ViewStateGenerator { get; set; } = null;

        public bool IsFinalPage { get; internal set; } = true;

        public IEnumerable<CatalogEntry> Results { get; internal set; } = Enumerable.Empty<CatalogEntry>();

        public int ResultsCount { get; internal set; } = default;

        [JsonConstructor]
        public CatalogResponse()
        {

        }
      
        public IEnumerable<ICatalogEntry> GetResults()
            => Results;

        public long GetResultsCount()
            => ResultsCount;

        public bool IsLastPage()
            => IsFinalPage;
    }
}