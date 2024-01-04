using HtmlAgilityPack;
using MicrosoftUpdateCatalog.LightAPI.Exceptions;
using MicrosoftUpdateCatalog.Core.Enums;
using MicrosoftUpdateCatalog.LightAPI.Model;
using MicrosoftUpdateCatalog.Core.Progress;
using MicrosoftUpdateCatalog.LightAPI.Result;
using MicrosoftUpdateCatalog.LightAPI.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Linq;
using System.Threading;
using System.IO;
using System.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http.Handlers;
using System.Globalization;
using MicrosoftUpdateCatalog.Core.Contract;
using MicrosoftUpdateCatalog.LightAPI.Valodation;

namespace MicrosoftUpdateCatalog.LightAPI
{
    /// <summary>
    /// Class that handles all communications with catalog.update.microsoft.com
    /// </summary>
    public class CatalogClient :
        ACatalogClient
    {
        private static async Task<HtmlDocument> GetDetailsPageAsync(string updateId, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpClient httpClient = new()
                {
                    BaseAddress = BASE_URI
                };

                using HttpResponseMessage response = await httpClient.GetAsync($"ScopedViewInline.aspx?updateid={updateId}", cancellationToken);

                if (!response.IsSuccessStatusCode)
                    throw new UnableToCollectUpdateDetailsException($"Catalog responded with {response.StatusCode} code");

                HtmlDocument tempPage = new();

                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                tempPage.Load(stream);

                HtmlNode errorDiv = tempPage.GetElementbyId("errorPageDisplayedError");

                if (errorDiv != null)
                {
                    string errorCode = errorDiv.LastChild.InnerText.Trim().Replace("]", "");

                    if (errorCode.Equals("8DDD0010", StringComparison.OrdinalIgnoreCase))
                        throw new UnableToCollectUpdateDetailsException("Catalog cannot proceed your request right now. Send request again later");
                    else if (errorCode.Equals("8DDD0024", StringComparison.OrdinalIgnoreCase))
                        throw new UpdateWasNotFoundException("Update by this UpdateID does not exists or was removed");
                    else
                        throw new CatalogErrorException($"Catalog returned unknown error code: {errorCode}");
                }

                return tempPage;
            }
            catch (TaskCanceledException ex)
            {
                throw new RequestToCatalogTimedOutException("Catalog was not responded", ex);
            }
        }

        private static async Task<string> GetDownloadPageContentAsync(string updateId, CancellationToken cancellationToken = default)
        {
            using HttpClient httpClient = new()
            {
                BaseAddress = BASE_URI
            };

            using HttpRequestMessage request = new(HttpMethod.Post, "DownloadDialog.aspx");

            DownloadPageContentPostObject downloadPageContentPostObject = new()
            {
                UidInfo = updateId,
                UpdateID = updateId
            };

            //Set serialization options with source generators
            //This allows this library to be used by projects that are AoT publishing ready, specially on .NET 8.0 +
            //Reflection must be disabled for such usage, therefore we also included a setting at project level to enforce the removal of reflection
            JsonSerializerOptions jsonSerializerOptions = new()
            {
                TypeInfoResolver = MSUCClientJsonSerializerContext.Default
            };
            string post = JsonSerializer.Serialize(downloadPageContentPostObject, jsonSerializerOptions);

            string body = $"[{post}]";

            using MultipartFormDataContent requestContent = new()
            {
                { new StringContent(body), "updateIds" }
            };

            request.Content = requestContent;

            try
            {
                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException)
            {
                throw new RequestToCatalogTimedOutException();
            }
        }

        private static EntryType GetEntryTypeFromString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return EntryType.Unknown;

            return value switch
            {
                "Security Updates" => EntryType.SecurityUpdate,
                "Critical Updates" => EntryType.CriticalUpdate,
                "Definition Updates" => EntryType.DefinitionUpdate,
                "Feature Packs" => EntryType.FeaturePack,
                "Service Packs" => EntryType.ServicePack,
                "Update Rollups" => EntryType.UpdateRollup,
                "Updates" => EntryType.StandardUpdate,
                "Hotfix" => EntryType.Hotfix,
                _ => EntryType.Unknown,
            };
        }
       
        private static async Task<CatalogResponse> ParseCatalogResponseFromHtmlPage(HtmlDocument htmlDoc, string searchQueryUri, CancellationToken cancellationToken = default)
        {
            string eventArgument = htmlDoc.GetElementbyId("__EVENTARGUMENT")?.FirstChild?.Attributes["value"]?.Value ?? string.Empty;
            string eventValidation = htmlDoc.GetElementbyId("__EVENTVALIDATION").GetAttributes().Where(att => att.Name == "value").First().Value;
            string viewState = htmlDoc.GetElementbyId("__VIEWSTATE").GetAttributes().Where(att => att.Name == "value").First().Value;
            string viewStateGenerator = htmlDoc.GetElementbyId("__VIEWSTATEGENERATOR").GetAttributes().Where(att => att.Name == "value").First().Value;
            HtmlNode nextPage = htmlDoc.GetElementbyId("ctl00_catalogBody_nextPageLinkText");

            string resultsCountString = htmlDoc.GetElementbyId("ctl00_catalogBody_searchDuration").InnerText;
            int resultsCount = int.Parse(NumericValidators.ResultCountRegex().Match(resultsCountString).Value);

            HtmlNode table = htmlDoc.GetElementbyId("ctl00_catalogBody_updateMatches")
                ?? throw new CatalogFailedToLoadSearchResultsPageException("Catalog response does not contains a search results table");

            HtmlNodeCollection searchResultsRows = table.SelectNodes("tr");

            IEnumerable<CatalogEntry> searchResults = await Task.WhenAll(searchResultsRows
                .Skip(1) //First row is always a headerRow 
                .Select(async x => await ParseCatalogSearchResultFromResultsTableRow(x, cancellationToken)));

            return new CatalogResponse()
            {
                Results = searchResults,
                SearchQueryUri = searchQueryUri,
                EventArgument = eventArgument,
                EventValidation = eventValidation,
                ViewState = viewState,
                ViewStateGenerator = viewStateGenerator,
                ResultsCount = resultsCount,
                IsFinalPage = nextPage is null
            };
        }

        private static async Task<CatalogEntry> ParseCatalogSearchResultFromResultsTableRow(HtmlNode resultsRow, CancellationToken cancellationToken = default)
        {
            HtmlNodeCollection rowCells = resultsRow.SelectNodes("td");

            CatalogEntry catalogEntry = new()
            {
                Title = rowCells[1].InnerText.Trim(),
                EntryType = GetEntryTypeFromString(rowCells[2].InnerText.Trim()),
                LastUpdated = DateOnly.ParseExact(rowCells[4].InnerText.Trim(), "MM/dd/yyyy", CultureInfo.InvariantCulture),
                Version = rowCells[5].InnerText.Trim(),
                Size = long.Parse(rowCells[6].SelectNodes("span")[1].InnerHtml.Trim()),
                UpdateID = rowCells[7].SelectNodes("input")[0].Id.Trim()
            };

            HtmlDocument _detailsPage = await GetDetailsPageAsync(catalogEntry.UpdateID, cancellationToken);
            string downloadPageContent = await GetDownloadPageContentAsync(catalogEntry.UpdateID, cancellationToken);

            MatchCollection downloadLinkMatches = UrlValidators.DownloadLinkRegex().Matches(downloadPageContent);
            if (!downloadLinkMatches.Any())
                throw new UnableToCollectUpdateDetailsException("Downloads page does not contains any valid download links");

            catalogEntry.DownloadLinks = downloadLinkMatches.Select(mt => mt.Value);

            return catalogEntry;
        }

        private static async Task<CatalogResponse> SendSearchQueryAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            using HttpClient httpClient = new()
            {
                BaseAddress = BASE_URI
            };

            using HttpResponseMessage response = await httpClient.GetAsync($"Search.aspx?q={HttpUtility.UrlEncode(requestUri)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            if (htmlDoc.GetElementbyId("ctl00_catalogBody_noResultText") is not null)
                throw new CatalogNoResultsException();

            return await ParseCatalogResponseFromHtmlPage(htmlDoc, requestUri, cancellationToken);
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

            using HttpClient httpClient = new()
            {
                BaseAddress = BASE_URI
            };

            using HttpResponseMessage response = await httpClient.PostAsync(unsortedResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            return await ParseCatalogResponseFromHtmlPage(htmlDoc, unsortedResponse.SearchQueryUri, cancellationToken);
        }

        public ClientConfiguration Configuration { get; set; } = null;

        public CatalogClient(ClientConfiguration configuration = null)
        {
            Configuration = configuration ?? new();
        }

        public static async Task<DownloadResult> DownloadAsync(CatalogEntry entry, DirectoryInfo destination = null, ByteCountProgress progress = null, CancellationToken cancellationToken = default)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            if (!entry.DownloadLinks.Any())
                throw new NullReferenceException("Update has no download links!");

            HttpClient httpClient = null;
            try
            {
                destination ??= new DirectoryInfo(Path.GetTempPath());

                if (!destination.Exists)
                    destination.Create();

                if (progress != null)
                {
                    using ProgressMessageHandler handler = new();
                    progress.TotalSize = entry.Size;
                    handler.HttpReceiveProgress += (se, ev) => progress?.Report(ev.BytesTransferred);
                    httpClient = new HttpClient(handler);
                }
                else
                {
                    httpClient = new HttpClient();
                }
                httpClient.BaseAddress = BASE_URI;

                List<FileSystemInfo> lst = new();
                foreach (string linkStr in entry.DownloadLinks)
                {
                    Uri link = new(linkStr);

                    using HttpResponseMessage response = await httpClient.GetAsync(link, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                    string path = Path.Combine(destination.FullName, Path.GetFileName(link.LocalPath));
                    if (File.Exists(path))
                        File.Delete(path);

                    using FileStream fs = File.Create(path);
                    await stream.CopyToAsync(fs, cancellationToken);

                    lst.Add(new FileInfo(path));
                }

                return new(lst.ToArray());
            }
            catch
            {
                throw;
            }
            finally
            {
                httpClient?.Dispose();
            }
        }

        /// <summary>
        /// Loads and parses the next page of the search results. If this method is called 
        /// on a final page - CatalogNoResultsException will be thrown
        /// </summary>
        /// <returns>CatalogResponse object representing search query results from the next page</returns>
        public static async Task<CatalogResponse> ParseNextCatalogResponseAsync(CatalogResponse lastCatalogResponse, CancellationToken cancellationToken = default)
        {
            if (lastCatalogResponse == null)
                throw new ArgumentNullException(nameof(lastCatalogResponse));

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

            using HttpClient httpClient = new()
            {
                BaseAddress = BASE_URI
            };
            using HttpResponseMessage response = await httpClient.PostAsync(lastCatalogResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            htmlDoc.Load(stream);

            return await ParseCatalogResponseFromHtmlPage(htmlDoc, lastCatalogResponse.SearchQueryUri, cancellationToken);
        }
        
        /// <summary>
        /// Sends search query to catalog.update.microsoft.com
        /// </summary>
        /// <param name="query">Search Query</param>
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
        public override async Task<IEnumerable<ICatalogEntry>> SearchAsync(string query, IQueryOptions options = null, CancellationToken cancellationToken = default)
        {
            CatalogResponse lastCatalogResponse = null;
            byte pageReloadAttemptsLeft = Configuration.PageReloadAttempts;
            bool ignoreDuplicates = true;
            if (options != null)
                ignoreDuplicates = options.ShouldIgnoreDuplicates();

            while (lastCatalogResponse is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {Configuration.PageReloadAttempts} attempts to refresh it");

                try
                {
                    lastCatalogResponse = await SendSearchQueryAsync(query, cancellationToken);
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
                    return Enumerable.Empty<ICatalogEntry>();
                }
            }

            if (options?.GetSortOrder() is not SortBy.None)
            {
                // This will sort results in the ascending order
                lastCatalogResponse = await SortSearchResults(query, lastCatalogResponse, options.GetSortOrder(), cancellationToken);

                if (options.GetSortDirection() is SortDirection.Descending)
                {
                    // The only way to sort results in the descending order is to send the same request again 
                    lastCatalogResponse = await SortSearchResults(query, lastCatalogResponse, options.GetSortOrder(), cancellationToken);
                }
            }

            pageReloadAttemptsLeft = Configuration.PageReloadAttempts;

            while (!lastCatalogResponse.IsFinalPage)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"One of the search result pages was not successfully loaded after {Configuration.PageReloadAttempts} attempts to refresh it");

                try
                {
                    List<CatalogEntry> lst = new(lastCatalogResponse.Results);
                    lastCatalogResponse = await ParseNextCatalogResponseAsync(lastCatalogResponse, cancellationToken);
                    if (ignoreDuplicates)
                    {
                        lastCatalogResponse.Results = lastCatalogResponse.Results.DistinctBy(result => (result.Size, result.Title));
                    }

                    lst.AddRange(lastCatalogResponse.Results);
                    lastCatalogResponse.ResultsCount = lst.Count;

                    pageReloadAttemptsLeft = Configuration.PageReloadAttempts; // Reset page refresh attempts count

                    if (options != null && options.GetMaxResults() <= lastCatalogResponse.ResultsCount)
                        break;
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

            return lastCatalogResponse.Results;
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
        public async Task<CatalogResponse> SearchFirstPageLightAsync(string query, IQueryOptions options = null, CancellationToken cancellationToken = default)
        {
            CatalogResponse catalogFirstPage = null;
            byte pageReloadAttemptsLeft = Configuration.PageReloadAttempts;

            while (catalogFirstPage is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {Configuration.PageReloadAttempts} attempts to refresh it");

                try
                {
                    catalogFirstPage = await SendSearchQueryAsync(query, cancellationToken);
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

            if (options?.GetSortOrder() is not SortBy.None)
            {
                // This will sort results in the ascending order
                catalogFirstPage = await SortSearchResults(query, catalogFirstPage, options.GetSortOrder(), cancellationToken);

                if (options.GetSortDirection() is SortDirection.Descending)
                {
                    // The only way to sort results in the descending order is to send the same request again 
                    catalogFirstPage = await SortSearchResults(query, catalogFirstPage, options.GetSortOrder(), cancellationToken);
                }
            }

            return catalogFirstPage;
        }
    }
}
