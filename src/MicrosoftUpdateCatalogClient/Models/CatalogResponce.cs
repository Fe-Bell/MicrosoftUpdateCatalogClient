using HtmlAgilityPack;
using Poushec.UpdateCatalogParser.Exceptions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Poushec.UpdateCatalogParser.Models
{
    public partial class CatalogResponse
    {
        private readonly List<CatalogSearchResult> _searchResults = null;

        internal string EventArgument { get; private set; }

        internal string EventValidation { get; private set; }

        internal string SearchQueryUri { get; private set; }

        internal string ViewState { get; private set; }

        internal string ViewStateGenerator { get; private set; }

        internal List<CatalogSearchResult> SearchResultsI 
            => _searchResults;

        internal CatalogResponse(
            string searchQueryUri,
            List<CatalogSearchResult> searchResults,
            string eventArgument,
            string eventValidation,
            string viewState,
            string viewStateGenerator,
            int resultsCount,
            bool isFinalPage
        )
        {
            SearchQueryUri = searchQueryUri;
            _searchResults = searchResults;
            EventArgument = eventArgument;
            EventValidation = eventValidation;
            ViewState = viewState;
            ViewStateGenerator = viewStateGenerator;
            ResultsCount = resultsCount;
            IsFinalPage = isFinalPage;
        }

        public bool IsFinalPage { get; private set; }

        public int ResultsCount { get; private set; }

        public IEnumerable<CatalogSearchResult> SearchResults
            => _searchResults;

        [JsonConstructor]
        internal CatalogResponse()
        {
            
        }
    }
}