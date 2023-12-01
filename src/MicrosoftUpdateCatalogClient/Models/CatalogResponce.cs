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
        private readonly HttpClient _httpClient;
        private readonly HtmlNode _nextPage;

        [JsonConstructor]
        public CatalogResponse()
        {
            
        }

        private CatalogResponse(
            HttpClient client,
            string searchQueryUri,
            List<CatalogSearchResult> searchResults,
            string eventArgument,
            string eventValidation,
            string viewState,
            string viewStateGenerator,
            HtmlNode nextPage,
            int resultsCount
        )
        {
            _httpClient = client;
            SearchQueryUri = searchQueryUri;

            SearchResults = searchResults;
            EventArgument = eventArgument;
            EventValidation = eventValidation;
            ViewState = viewState;
            ViewStateGenerator = viewStateGenerator;
            this._nextPage = nextPage;
            ResultsCount = resultsCount;
        }

        internal string EventArgument;
        internal string EventValidation;
        internal string SearchQueryUri;
        internal string ViewState;
        internal string ViewStateGenerator;

        internal static CatalogResponse ParseFromHtmlPage(HtmlDocument htmlDoc, HttpClient client, string searchQueryUri)
        {
            string eventArgument = htmlDoc.GetElementbyId("__EVENTARGUMENT")?.FirstChild?.Attributes["value"]?.Value ?? string.Empty;
            string eventValidation = htmlDoc.GetElementbyId("__EVENTVALIDATION").GetAttributes().Where(att => att.Name == "value").First().Value;
            string viewState = htmlDoc.GetElementbyId("__VIEWSTATE").GetAttributes().Where(att => att.Name == "value").First().Value;
            string viewStateGenerator = htmlDoc.GetElementbyId("__VIEWSTATEGENERATOR").GetAttributes().Where(att => att.Name == "value").First().Value;
            HtmlNode nextPage = htmlDoc.GetElementbyId("ctl00_catalogBody_nextPageLinkText");

            string resultsCountString = htmlDoc.GetElementbyId("ctl00_catalogBody_searchDuration").InnerText;
            int resultsCount = int.Parse(Validation.NumericValidators.ResultCountRegex().Match(resultsCountString).Value);

            HtmlNode table = htmlDoc.GetElementbyId("ctl00_catalogBody_updateMatches")
                ?? throw new CatalogFailedToLoadSearchResultsPageException("Catalog response does not contains a search results table");

            HtmlNodeCollection searchResultsRows = table.SelectNodes("tr");

            List<CatalogSearchResult> searchResults = new();

            foreach (var resultsRow in searchResultsRows.Skip(1)) // First row is always a headerRow 
            {
                searchResults.Add(CatalogSearchResult.ParseFromResultsTableRow(resultsRow));
            }

            return new CatalogResponse(
                client,
                searchQueryUri,
                searchResults,
                eventArgument,
                eventValidation,
                viewState,
                viewStateGenerator,
                nextPage,
                resultsCount
            );
        }

        public int ResultsCount;

        public List<CatalogSearchResult> SearchResults;

        public bool IsFinalPage => _nextPage is null;

        /// <summary>
        /// Loads and parses the next page of the search results. If this method is called 
        /// on a final page - CatalogNoResultsException will be thrown
        /// </summary>
        /// <returns>CatalogResponse object representing search query results from the next page</returns>
        public async Task<CatalogResponse> ParseNextPageAsync(CancellationToken cancellationToken = default)
        {
            if (IsFinalPage)
                throw new CatalogNoResultsException("No more search results available. This is a final page.");

            Dictionary<string, string> formData = new() 
            {
                { "__EVENTTARGET",          "ctl00$catalogBody$nextPageLinkText" },
                { "__EVENTARGUMENT",        EventArgument },
                { "__VIEWSTATE",            ViewState },
                { "__VIEWSTATEGENERATOR",   ViewStateGenerator },
                { "__EVENTVALIDATION",      EventValidation }
            };

            using FormUrlEncodedContent requestContent = new (formData); 

            using HttpResponseMessage response = await _httpClient.PostAsync(SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            htmlDoc.Load(stream);

            return ParseFromHtmlPage(htmlDoc, _httpClient, SearchQueryUri);
        }
    }
}