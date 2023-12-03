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

namespace Poushec.UpdateCatalogParser
{
    /// <summary>
    /// Class that handles all communications with catalog.update.microsoft.com
    /// </summary>
    public class CatalogClient
    {
        public byte PageReloadAttempts { get; set; } = 3;
             
        private static async Task<CatalogResponse> SendSearchQueryAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            using HttpClient httpClient = new();

            using HttpResponseMessage response = await httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            if (htmlDoc.GetElementbyId("ctl00_catalogBody_noResultText") is not null)
                throw new CatalogNoResultsException();

            return Parser.ParseCatalogResponseFromHtmlPage(htmlDoc, requestUri);
        }

        private static async Task<CatalogResponse> SortSearchResults(string searchQuery, CatalogResponse unsortedResponse, SortBy sortBy, CancellationToken cancellationToken = default)
        {
            string eventTarget = sortBy switch
            {
                SortBy.Title => "ctl00$catalogBody$updateMatches$ctl02$titleHeaderLink",
                SortBy.Products => "ctl00$catalogBody$updateMatches$ctl02$productsHeaderLink",
                SortBy.Classification => "ctl00$catalogBody$updateMatches$ctl02$classHeaderLink",
                SortBy.LastUpdated => "ctl00$catalogBody$updateMatches$ctl02$dateHeaderLink",
                SortBy.Version => "ctl00$catalogBody$updateMatches$ctl02$versionHeaderLink",
                SortBy.Size => "ctl00$catalogBody$updateMatches$ctl02$sizeHeaderLink",
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

            using HttpClient httpClient = new();

            using HttpResponseMessage response = await httpClient.PostAsync(unsortedResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            return Parser.ParseCatalogResponseFromHtmlPage(htmlDoc, unsortedResponse.SearchQueryUri);
        }

        public CatalogClient()
        {
           
        }

        /// <summary>
        /// Sends search query to catalog.update.microsoft.com and returns a CatalogResponse
        /// object representing the first results page. Other pages can be requested later by
        /// calling CatalogResponse.ParseNextPageAsync method
        /// </summary>
        /// <param name="query">Search Query</param>
        /// <param name="sortBy">
        /// (Optional)
        /// Use this argument if you want Catalog to sort search results.
        /// Available values are the same as in catalog: Title, Products, Classification, LastUpdated, Version, Size 
        /// By default results are sorted by LastUpdated
        /// </param>
        /// <param name="sortDirection">Sorting direction. Ascending or Descending</param>
        /// <returns>CatalogResponse object representing the first results page</returns>
        public async Task<CatalogResponse> GetFirstPageFromSearchQueryAsync(
            string query,
            SortBy sortBy = SortBy.None,
            SortDirection sortDirection = SortDirection.Descending,
            CancellationToken cancellationToken = default)
        {
            string catalogBaseUrl = "https://www.catalog.update.microsoft.com/Search.aspx";
            string searchQueryUrl = $"{catalogBaseUrl}?q={HttpUtility.UrlEncode(query)}";

            CatalogResponse catalogFirstPage = null;
            byte pageReloadAttemptsLeft = PageReloadAttempts;

            while (catalogFirstPage is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {PageReloadAttempts} attempts to refresh it");

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
                catalogFirstPage = await SortSearchResults(query, catalogFirstPage, sortBy, cancellationToken);

                if (sortDirection is SortDirection.Descending)
                {
                    // The only way to sort results in the descending order is to send the same request again 
                    catalogFirstPage = await SortSearchResults(query, catalogFirstPage, sortBy, cancellationToken);
                }
            }

            return catalogFirstPage;
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
            UpdateBase updateBase = await Parser.CreateUpdateObjectAsync(searchResult, PageReloadAttempts, cancellationToken);
            return updateBase;
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
            CancellationToken cancellationToken = default)
        {
            const string catalogBaseUrl = "https://www.catalog.update.microsoft.com/Search.aspx";
            string searchQueryUrl = $"{catalogBaseUrl}?q={HttpUtility.UrlEncode(Query)}"; 
            
            CatalogResponse lastCatalogResponse = null;
            byte pageReloadAttemptsLeft = PageReloadAttempts;
            
            while (lastCatalogResponse is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {PageReloadAttempts} attempts to refresh it");

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

            pageReloadAttemptsLeft = PageReloadAttempts;
            
            while (!lastCatalogResponse.IsFinalPage)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"One of the search result pages was not successfully loaded after {PageReloadAttempts} attempts to refresh it");

                try
                {
                    lastCatalogResponse = await Parser.ParseNextCatalogResponseAsync(lastCatalogResponse, cancellationToken);
                    lastCatalogResponse.GetSearchResults().AddRange(lastCatalogResponse.GetSearchResults());
                    pageReloadAttemptsLeft = PageReloadAttempts; // Reset page refresh attempts count
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
    }
}
