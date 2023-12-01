using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Poushec.UpdateCatalogParser.Models
{
    public class CatalogResponse
    {
        private List<CatalogSearchResult> _searchResults = null;

        internal string EventArgument { get; set; }

        internal string EventValidation { get; set; }

        internal string SearchQueryUri { get; set; }

        internal string ViewState { get; set; }

        internal string ViewStateGenerator { get; set; }

        [JsonConstructor]
        internal CatalogResponse()
        {

        }

        internal List<CatalogSearchResult> GetSearchResults()
                    => _searchResults;

        public bool IsFinalPage { get; internal set; }

        public int ResultsCount { get; internal set; }

        public IEnumerable<CatalogSearchResult> SearchResults 
        { 
            get => _searchResults;
            internal set
            {
                if (value is List<CatalogSearchResult> lst)
                    _searchResults = lst;
                else
                    _searchResults = value.ToList();
            }
        }
    }
}