using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using HtmlAgilityPack;
using System.Linq;
using Poushec.UpdateCatalogParser.Models;
using Poushec.UpdateCatalogParser.Exceptions;
using Poushec.UpdateCatalogParser.Enums;
using System.Threading;
using System.IO;
using System.Web;
using System.Drawing;

namespace Poushec.UpdateCatalogParser
{
    /// <summary>
    /// Class that handles all communications with catalog.update.microsoft.com
    /// </summary>
    public class CatalogClient
    {
        private readonly byte _pageReloadAttempts = 0;
        private readonly HttpClient _httpClient = null;

        public static CatalogSearchResult ParseCatalogSearchResultFromResultsTableRow(HtmlNode resultsRow)
        {
            HtmlNodeCollection rowCells = resultsRow.SelectNodes("td");

            string title = rowCells[1].InnerText.Trim();
            string products = rowCells[2].InnerText.Trim();
            string classification = rowCells[3].InnerText.Trim();
            DateOnly lastUpdated = DateOnly.Parse(rowCells[4].InnerText.Trim());
            string version = rowCells[5].InnerText.Trim();
            string size = rowCells[6].SelectNodes("span")[0].InnerText;
            int sizeInBytes = int.Parse(rowCells[6].SelectNodes("span")[1].InnerHtml);
            string updateID = rowCells[7].SelectNodes("input")[0].Id;

            return new CatalogSearchResult()
            {
                Title = title,
                Products = products,
                Classification = classification,
                LastUpdated = lastUpdated,
                Version = version,
                Size = size,
                SizeInBytes = sizeInBytes,
                UpdateID = updateID
            };
        }

        private static CatalogResponse ParseCatalogResponseFromHtmlPage(HtmlDocument htmlDoc, string searchQueryUri)
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

            foreach (HtmlNode resultsRow in searchResultsRows.Skip(1)) // First row is always a headerRow 
            {
                CatalogSearchResult catalogSearchResult = ParseCatalogSearchResultFromResultsTableRow(resultsRow);
                searchResults.Add(catalogSearchResult);
            }

            return new CatalogResponse()
            {
                SearchResults = searchResults,
                SearchQueryUri = searchQueryUri,
                EventArgument = eventArgument,
                EventValidation = eventValidation,
                ViewState = viewState,
                ViewStateGenerator = viewStateGenerator,
                ResultsCount = resultsCount,
                IsFinalPage = nextPage is null
            };
        }

        /// <summary>
        /// Loads and parses the next page of the search results. If this method is called 
        /// on a final page - CatalogNoResultsException will be thrown
        /// </summary>
        /// <returns>CatalogResponse object representing search query results from the next page</returns>
        private async Task<CatalogResponse> ParseNextCatalogResponseAsync(CatalogResponse lastCatalogResponse, CancellationToken cancellationToken = default)
        {
            if (lastCatalogResponse.IsFinalPage)
                throw new CatalogNoResultsException("No more search results available. This is a final page.");

            Dictionary<string, string> formData = new()
            {
                { "__EVENTTARGET",          "ctl00$catalogBody$nextPageLinkText" },
                { "__EVENTARGUMENT",        lastCatalogResponse.EventArgument },
                { "__VIEWSTATE",            lastCatalogResponse.ViewState },
                { "__VIEWSTATEGENERATOR",   lastCatalogResponse.ViewStateGenerator },
                { "__EVENTVALIDATION",      lastCatalogResponse.EventValidation }
            };

            using FormUrlEncodedContent requestContent = new(formData);

            using HttpResponseMessage response = await _httpClient.PostAsync(lastCatalogResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            htmlDoc.Load(stream);

            return ParseCatalogResponseFromHtmlPage(htmlDoc, lastCatalogResponse.SearchQueryUri);
        }

        public CatalogClient(byte pageReloadAttemptsAllowed = 3)
        {
            _httpClient = new HttpClient();
            _pageReloadAttempts = pageReloadAttemptsAllowed;
        }

        public CatalogClient(HttpClient httpClient, byte pageReloadAttemptsAllowed = 3)
        {
            _httpClient = httpClient;
            _pageReloadAttempts = pageReloadAttemptsAllowed;
        }
        
        /// <summary>
        /// Sends search query to catalog.update.microsoft.com
        /// </summary>
        /// <param name="Query">Search Query</param>
        /// <param name="ignoreDuplicates">
        /// (Optional)
        /// TRUE - founded updates that have the same Title and SizeInBytes
        /// fields as any of already founded updates will be ignored.
        /// FALSE - collects every founded update.
        /// </param>
        /// <param name="sortBy">
        /// (Optional)
        /// Use this argument if you want Catalog to sort search results.
        /// Available values are the same as in catalog: Title, Products, Classification, LastUpdated, Version, Size 
        /// By default results are sorted by LastUpdated
        /// </param>
        /// <param name="sortDirection">Sorting direction. Ascending or Descending</param>
        /// <returns>List of objects derived from UpdateBase class (Update or Driver)</returns>
        public async Task<IEnumerable<CatalogSearchResult>> SendSearchQueryAsync(
            string Query, 
            bool ignoreDuplicates = true, 
            SortBy sortBy = SortBy.None, 
            SortDirection sortDirection = SortDirection.Descending,
            CancellationToken cancellationToken = default
        )
        {
            const string catalogBaseUrl = "https://www.catalog.update.microsoft.com/Search.aspx";
            string searchQueryUrl = $"{catalogBaseUrl}?q={HttpUtility.UrlEncode(Query)}"; 
            
            CatalogResponse lastCatalogResponse = null;
            byte pageReloadAttemptsLeft = _pageReloadAttempts;
            
            while (lastCatalogResponse is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {_pageReloadAttempts} attempts to refresh it");

                try
                {
                    lastCatalogResponse = await SendSearchQueryAsync(searchQueryUrl, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Request timed out - it happens. We'll try to reload a page
                    pageReloadAttemptsLeft--;
                    continue;
                }
                catch (CatalogFailedToLoadSearchResultsPageException)
                {
                    // Sometimes catalog responses with an empty search results table.
                    // Refreshing a page usually helps, so that's what we'll try to do
                    pageReloadAttemptsLeft--;
                    continue;
                }
                catch (CatalogNoResultsException)
                {
                    // Search query returned no results
                    return new List<CatalogSearchResult>();
                }
            }

            if (sortBy is not SortBy.None)
            {
                // This will sort results in the ascending order
                lastCatalogResponse = await SortSearchResults(Query, lastCatalogResponse, sortBy, cancellationToken);
            
                if (sortDirection is SortDirection.Descending)
                {
                    // The only way to sort results in the descending order is to send the same request again 
                    lastCatalogResponse = await SortSearchResults(Query, lastCatalogResponse, sortBy, cancellationToken);
                }
            }

            pageReloadAttemptsLeft = _pageReloadAttempts;
            
            while (!lastCatalogResponse.IsFinalPage)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"One of the search result pages was not successfully loaded after {_pageReloadAttempts} attempts to refresh it");

                try
                {
                    lastCatalogResponse = await ParseNextCatalogResponseAsync(lastCatalogResponse, cancellationToken);
                    lastCatalogResponse.GetSearchResults().AddRange(lastCatalogResponse.GetSearchResults());
                    pageReloadAttemptsLeft = _pageReloadAttempts; // Reset page refresh attempts count
                }
                catch (TaskCanceledException) 
                {
                    // Request timed out - it happens
                    pageReloadAttemptsLeft--;
                    continue;
                }
                catch (CatalogFailedToLoadSearchResultsPageException)
                {
                    // Sometimes catalog responses with an empty search results table.
                    // Refreshing a page usually helps, so that's what we'll try to do
                    pageReloadAttemptsLeft--;
                    continue;
                }
            }

            if (ignoreDuplicates)
                return lastCatalogResponse.GetSearchResults().DistinctBy(result => (result.SizeInBytes, result.Title));

            return lastCatalogResponse.GetSearchResults();
        }

        /// <summary>
        /// Sends search query to catalog.update.microsoft.com and returns a CatalogResponse
        /// object representing the first results page. Other pages can be requested later by
        /// calling CatalogResponse.ParseNextPageAsync method
        /// </summary>
        /// <param name="Query">Search Query</param>
        /// <param name="sortBy">
        /// (Optional)
        /// Use this argument if you want Catalog to sort search results.
        /// Available values are the same as in catalog: Title, Products, Classification, LastUpdated, Version, Size 
        /// By default results are sorted by LastUpdated
        /// </param>
        /// <param name="sortDirection">Sorting direction. Ascending or Descending</param>
        /// <returns>CatalogResponse object representing the first results page</returns>
        public async Task<CatalogResponse> GetFirstPageFromSearchQueryAsync(
            string Query, 
            SortBy sortBy = SortBy.None, 
            SortDirection sortDirection = SortDirection.Descending,
            CancellationToken cancellationToken = default
        )
        {
            string catalogBaseUrl = "https://www.catalog.update.microsoft.com/Search.aspx";
            string searchQueryUrl = $"{catalogBaseUrl}?q={HttpUtility.UrlEncode(Query)}"; 
            
            CatalogResponse catalogFirstPage = null;
            byte pageReloadAttemptsLeft = _pageReloadAttempts;
            
            while (catalogFirstPage is null)
            {
                if (pageReloadAttemptsLeft == 0)
                {
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {_pageReloadAttempts} attempts to refresh it");
                }

                try
                {
                    catalogFirstPage = await SendSearchQueryAsync(searchQueryUrl, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Request timed out - it happens. We'll try to reload a page
                    pageReloadAttemptsLeft--;
                    continue;
                }
                catch (CatalogFailedToLoadSearchResultsPageException)
                {
                    // Sometimes catalog responses with an empty search results table.
                    // Refreshing a page usually helps, so that's what we'll try to do
                    pageReloadAttemptsLeft--;
                    continue;
                }
            }

            if (sortBy is not SortBy.None)
            {
                // This will sort results in the ascending order
                catalogFirstPage = await SortSearchResults(Query, catalogFirstPage, sortBy, cancellationToken);
            
                if (sortDirection is SortDirection.Descending)
                {
                    // The only way to sort results in the descending order is to send the same request again 
                    catalogFirstPage = await SortSearchResults(Query, catalogFirstPage, sortBy, cancellationToken);
                }
            }

            return catalogFirstPage;
        }
        
        /// <summary>
        /// Attempts to collect update details from Update Details Page and Download Page 
        /// </summary>
        /// <param name="searchResult">CatalogSearchResult from search query</param>
        /// <returns>Null is request was unsuccessful or UpdateBase (Driver/Update) object with all collected details</returns>
        public async Task<UpdateBase> TryGetUpdateDetailsAsync(CatalogSearchResult searchResult, CancellationToken cancellationToken = default)
        {
            try
            {
                UpdateBase update = await GetUpdateDetailsAsync(searchResult, cancellationToken);
                return update;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Collect update details from Update Details Page and Download Page 
        /// </summary>
        /// <param name="searchResult">CatalogSearchResult from search query</param>
        /// <returns>Ether Driver of Update object derived from UpdateBase class with all collected details</returns>
        /// <exception cref="UnableToCollectUpdateDetailsException">Thrown when catalog response with an error page or request was unsuccessful</exception>
        /// <exception cref="UpdateWasNotFoundException">Thrown when catalog response with an error page with error code 8DDD0024 (Not found)</exception>
        /// <exception cref="CatalogErrorException">Thrown when catalog response with an error page with unknown error code</exception>
        /// <exception cref="RequestToCatalogTimedOutException">Thrown when request to catalog was canceled due to timeout</exception>
        /// <exception cref="ParseHtmlPageException">Thrown when function was not able to parse ScopedView HTML page</exception>
        public async Task<UpdateBase> GetUpdateDetailsAsync(CatalogSearchResult searchResult, CancellationToken cancellationToken = default)
        {
            UpdateBase updateBase = new(searchResult);

            byte pageReloadAttemptsLeft = _pageReloadAttempts;

            while (true)
            {
                try 
                {
                    await updateBase.ParseCommonDetails(_httpClient, cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    pageReloadAttemptsLeft--;

                    if (pageReloadAttemptsLeft == 0)
                    {
                        throw new UnableToCollectUpdateDetailsException($"Failed to properly parse update details page after {_pageReloadAttempts} attempts", ex);
                    }
                }
                
            }

            if (updateBase.Classification.Contains("Driver"))
                return new Driver(updateBase);

            switch (updateBase.Classification)
            {
                case "Security Updates":
                case "Critical Updates":
                case "Definition Updates":
                case "Feature Packs": 
                case "Service Packs":
                case "Update Rollups":
                case "Updates": 
                case "Hotfix":
                    var update = new Update(updateBase);
                    return update;

                default: throw new NotImplementedException();
            }
        }
        
        private async Task<CatalogResponse> SortSearchResults(string searchQuery, CatalogResponse unsortedResponse, SortBy sortBy, CancellationToken cancellationToken = default)
        {
            string eventTarget = sortBy switch 
            {
                SortBy.Title =>             "ctl00$catalogBody$updateMatches$ctl02$titleHeaderLink",
                SortBy.Products =>          "ctl00$catalogBody$updateMatches$ctl02$productsHeaderLink",
                SortBy.Classification =>    "ctl00$catalogBody$updateMatches$ctl02$classHeaderLink",
                SortBy.LastUpdated =>       "ctl00$catalogBody$updateMatches$ctl02$dateHeaderLink",
                SortBy.Version =>           "ctl00$catalogBody$updateMatches$ctl02$versionHeaderLink",
                SortBy.Size =>              "ctl00$catalogBody$updateMatches$ctl02$sizeHeaderLink",
                _ => throw new NotImplementedException("Failed to sort search results. Unknown sortBy value")
            };

            Dictionary<string, string> formData = new() 
            {
                { "__EVENTTARGET",          eventTarget },
                { "__EVENTARGUMENT",        unsortedResponse.EventArgument },
                { "__VIEWSTATE",            unsortedResponse.ViewState },
                { "__VIEWSTATEGENERATOR",   unsortedResponse.ViewStateGenerator },
                { "__EVENTVALIDATION",      unsortedResponse.EventValidation },
                { "ctl00$searchTextBox",    searchQuery }
            };

            FormUrlEncodedContent requestContent = new(formData); 

            using HttpResponseMessage response = await _httpClient.PostAsync(unsortedResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            HtmlDocument htmlDoc = new ();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            return ParseCatalogResponseFromHtmlPage(htmlDoc, unsortedResponse.SearchQueryUri);
        }

        private async Task<CatalogResponse> SendSearchQueryAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            using HttpResponseMessage response = await _httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            if (htmlDoc.GetElementbyId("ctl00_catalogBody_noResultText") is not null)
                throw new CatalogNoResultsException();

            return ParseCatalogResponseFromHtmlPage(htmlDoc, requestUri);
        }
    }
}
