using HtmlAgilityPack;
using MicrosoftUpdateCatalogClient.Exceptions;
using MicrosoftUpdateCatalogClient.Enums;
using MicrosoftUpdateCatalogClient.Models;
using MicrosoftUpdateCatalogClient.Progress;
using MicrosoftUpdateCatalogClient.Result;
using MicrosoftUpdateCatalogClient.Serialization;
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

namespace MicrosoftUpdateCatalogClient
{
    /// <summary>
    /// Class that handles all communications with catalog.update.microsoft.com
    /// </summary>
    public class CatalogClient
    {
        private static readonly Uri BaseUrl = new("https://www.catalog.update.microsoft.com");

        private static async Task<CatalogEntry> CreateUpdateObjectAsync(CatalogSearchResult catalogSearchResult, byte pageReloadAttempts = 3, CancellationToken cancellationToken = default)
        {
            HtmlDocument _detailsPage = await GetDetailsPageAsync(catalogSearchResult.UpdateID, cancellationToken);
            string downloadPageContent = await GetDownloadPageContentAsync(catalogSearchResult.UpdateID, cancellationToken);

            CatalogEntry obj;
            if (catalogSearchResult.Classification.Contains("Driver", StringComparison.OrdinalIgnoreCase))
            {
                Driver driver = new();
                ParseDriverDetails(_detailsPage, driver);
                obj = driver;
            }
            else
            {
                switch (catalogSearchResult.Classification)
                {
                    case "Security Updates":
                    case "Critical Updates":
                    case "Definition Updates":
                    case "Feature Packs":
                    case "Service Packs":
                    case "Update Rollups":
                    case "Updates":
                    case "Hotfix":
                        {
                            Update update = new();
                            ParseDefaultUpdateDetails(_detailsPage, update);
                            obj = update;
                        }
                        break;
                    default: throw new NotImplementedException();
                }
            }

            obj.UpdateID = catalogSearchResult.UpdateID;
            obj.Title = catalogSearchResult.Title;
            obj.Classification = catalogSearchResult.Classification;
            obj.LastUpdated = catalogSearchResult.LastUpdated;
            obj.Size = catalogSearchResult.Size;
            obj.SizeInBytes = catalogSearchResult.SizeInBytes;
            obj.Products = catalogSearchResult.Products.Trim().Split(",");

            //ParseCommonDetails(_detailsPage, obj);
            ParseDownloadLinks(downloadPageContent, obj);

            byte pageReloadAttemptsLeft = pageReloadAttempts;
            while (true)
            {
                try
                {
                    ParseCommonDetails(_detailsPage, obj);
                    break;
                }
                catch (Exception ex)
                {
                    pageReloadAttemptsLeft--;

                    if (pageReloadAttemptsLeft == 0)
                        throw new UnableToCollectUpdateDetailsException($"Failed to properly parse update details page after {pageReloadAttempts} attempts", ex);
                }
            }

            return obj;
        }

        private static async Task<HtmlDocument> GetDetailsPageAsync(string updateId, CancellationToken cancellationToken = default)
        {      
            try
            {
                using HttpClient httpClient = new()
                {
                    BaseAddress = BaseUrl
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
                BaseAddress = BaseUrl
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

            IEnumerable<CatalogSearchResult> searchResults = searchResultsRows
                .Skip(1) //First row is always a headerRow 
                .Select(ParseCatalogSearchResultFromResultsTableRow);

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

        private static CatalogSearchResult ParseCatalogSearchResultFromResultsTableRow(HtmlNode resultsRow)
        {
            HtmlNodeCollection rowCells = resultsRow.SelectNodes("td");

            return new CatalogSearchResult()
            {
                Title = rowCells[1].InnerText.Trim(),
                Products = rowCells[2].InnerText.Trim(),
                Classification = rowCells[3].InnerText.Trim(),
                LastUpdated = DateOnly.ParseExact(rowCells[4].InnerText.Trim(), "MM/dd/yyyy", CultureInfo.InvariantCulture),
                Version = rowCells[5].InnerText.Trim(),
                Size = rowCells[6].SelectNodes("span")[0].InnerText.Trim(),
                SizeInBytes = long.Parse(rowCells[6].SelectNodes("span")[1].InnerHtml.Trim()),
                UpdateID = rowCells[7].SelectNodes("input")[0].Id.Trim()
            };
        }

        private static void ParseCommonDetails(HtmlDocument detailsPage, CatalogEntry updateBase)
        {
            if (detailsPage is null)
                throw new ParseHtmlPageException("_parseCommonDetails() failed. _detailsPage is null");

            updateBase.Description = detailsPage.GetElementbyId("ScopedViewHandler_desc").InnerText;

            updateBase.Architectures = detailsPage.GetElementbyId("archDiv").LastChild.InnerText.Trim().Split(",")
                .Select(x => x.Trim());

            updateBase.SupportedLanguages = detailsPage.GetElementbyId("languagesDiv").LastChild.InnerText.Trim().Split(",")
                .Select(x => x.Trim());

            string moreInfoDivContent = detailsPage.GetElementbyId("moreInfoDiv").InnerHtml;
            updateBase.MoreInformation = Validation.UrlValidators.BasicUrlRegex()
                .Matches(moreInfoDivContent)
                .Select(match => match.Value)
                .Distinct();

            string supportUrlDivContent = detailsPage.GetElementbyId("suportUrlDiv").InnerHtml;
            updateBase.SupportUrl = Validation.UrlValidators.BasicUrlRegex()
                .Matches(supportUrlDivContent)
                .Select(match => match.Value)
                .Distinct();

            updateBase.RestartBehavior = detailsPage.GetElementbyId("ScopedViewHandler_rebootBehavior").InnerText;

            updateBase.MayRequestUserInput = detailsPage.GetElementbyId("ScopedViewHandler_userInput").InnerText;

            updateBase.MustBeInstalledExclusively = detailsPage.GetElementbyId("ScopedViewHandler_installationImpact").InnerText;

            updateBase.RequiresNetworkConnectivity = detailsPage.GetElementbyId("ScopedViewHandler_connectivity").InnerText;

            HtmlNode uninstallNotesDiv = detailsPage.GetElementbyId("uninstallNotesDiv");

            if (uninstallNotesDiv.ChildNodes.Count == 3)
            {
                updateBase.UninstallNotes = uninstallNotesDiv.LastChild.InnerText.Trim();
            }
            else
            {
                updateBase.UninstallNotes = detailsPage.GetElementbyId("uninstallNotesDiv")
                    .ChildNodes[3]
                    .InnerText.Trim();
            }

            updateBase.UninstallSteps = detailsPage.GetElementbyId("uninstallStepsDiv")
                .LastChild
                .InnerText.Trim();
        }

        private static void ParseDefaultUpdateDetails(HtmlDocument detailsPage, Update update)
        {
            if (detailsPage is null)
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");

            if (update == null)
                ArgumentException.ThrowIfNullOrEmpty(nameof(update));

            try
            {
                update.MSRCNumber = detailsPage.GetElementbyId("securityBullitenDiv").LastChild.InnerText.Trim();
                update.MSRCSeverity = detailsPage.GetElementbyId("ScopedViewHandler_msrcSeverity").InnerText;
                update.KBArticleNumbers = detailsPage.GetElementbyId("kbDiv").LastChild.InnerText.Trim();

                //Superseded lst
                HtmlNode supersedesDivs = detailsPage.GetElementbyId("supersedesInfo");
                // If first child isn't a div - than it's just a n/a and there's nothing to gather
                if (!supersedesDivs.FirstChild.InnerText.Trim().Equals("n/a", StringComparison.OrdinalIgnoreCase))
                {
                    update.Supersedes = supersedesDivs
                        .ChildNodes
                        .Where(node => node.Name == "div")
                        .Select(x => x.InnerText.Trim());
                }

                HtmlNode supersededByDivs = detailsPage.GetElementbyId("supersededbyInfo");
                // If first child isn't a div - than it's just a n/a and there's nothing to gather
                if (!supersededByDivs.FirstChild.InnerText.Trim().Equals("n/a", StringComparison.OrdinalIgnoreCase))
                {
                    update.SupersededBy = supersededByDivs.ChildNodes.Where(node => node.Name == "div").Select(x =>
                    {
                        return x.ChildNodes[1].GetAttributeValue("href", "").Replace("ScopedViewInline.aspx?updateid=", "");
                    });
                }
            }
            catch (Exception ex)
            {
                throw new ParseHtmlPageException("Failed to parse Update details", ex);
            }
        }

        private static void ParseDownloadLinks(string downloadPageContent, CatalogEntry updateBase)
        {
            if (updateBase == null)
                ArgumentException.ThrowIfNullOrEmpty(nameof(updateBase));

            MatchCollection downloadLinkMatches = Validation.UrlValidators.DownloadLinkRegex().Matches(downloadPageContent);
            if (!downloadLinkMatches.Any())
                throw new UnableToCollectUpdateDetailsException("Downloads page does not contains any valid download links");

            updateBase.DownloadLinks = downloadLinkMatches.Select(mt => mt.Value);
        }

        private static void ParseDriverDetails(HtmlDocument detailsPage, Driver driver)
        {
            if (detailsPage is null)
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");

            try
            {
                driver.HardwareIDs = ParseHardwareIDs(detailsPage);
                driver.Company = detailsPage.GetElementbyId("ScopedViewHandler_company").InnerText;
                driver.DriverManufacturer = detailsPage.GetElementbyId("ScopedViewHandler_manufacturer").InnerText;
                driver.DriverClass = detailsPage.GetElementbyId("ScopedViewHandler_driverClass").InnerText;
                driver.DriverModel = detailsPage.GetElementbyId("ScopedViewHandler_driverModel").InnerText;
                driver.DriverProvider = detailsPage.GetElementbyId("ScopedViewHandler_driverProvider").InnerText;
                driver.DriverVersion = detailsPage.GetElementbyId("ScopedViewHandler_version").InnerText;
                driver.VersionDate = DateOnly.Parse(detailsPage.GetElementbyId("ScopedViewHandler_versionDate").InnerText);
            }
            catch (Exception ex)
            {
                throw new ParseHtmlPageException("Failed to parse Driver details", ex);
            }
        }

        private static IEnumerable<string> ParseHardwareIDs(HtmlDocument detailsPage)
        {
            if (detailsPage is null)
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");

            HtmlNode hwIdsDivs = detailsPage.GetElementbyId("driverhwIDs");
            if (hwIdsDivs == null)
                return Enumerable.Empty<string>();

            List<string> hwIds = new();
            foreach (HtmlNode node in hwIdsDivs.ChildNodes.Where(node => node.Name == "div"))
            {
                string hid = node.ChildNodes
                        .First().InnerText
                        .Trim()
                        .Replace(@"\r\n", "");

                if (!string.IsNullOrEmpty(hid))
                {
                    hwIds.Add(hid.ToUpper());
                }
            }

            return hwIds;
        }
     
        /// <summary>
        /// Loads and parses the next page of the search results. If this method is called 
        /// on a final page - CatalogNoResultsException will be thrown
        /// </summary>
        /// <returns>CatalogResponse object representing search query results from the next page</returns>
        private static async Task<CatalogResponse> ParseNextCatalogResponseAsync(CatalogResponse lastCatalogResponse, CancellationToken cancellationToken = default)
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

            using HttpClient httpClient = new()
            {
                BaseAddress = BaseUrl
            };
            using HttpResponseMessage response = await httpClient.PostAsync(lastCatalogResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            htmlDoc.Load(stream);

            return ParseCatalogResponseFromHtmlPage(htmlDoc, lastCatalogResponse.SearchQueryUri);
        }

        private static async Task<CatalogResponse> SendSearchQueryAsync(string requestUri, CancellationToken cancellationToken = default)
        {
            using HttpClient httpClient = new()
            {
                BaseAddress = BaseUrl
            };
            
            using HttpResponseMessage response = await httpClient.GetAsync($"Search.aspx?q={HttpUtility.UrlEncode(requestUri)}", cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            if (htmlDoc.GetElementbyId("ctl00_catalogBody_noResultText") is not null)
                throw new CatalogNoResultsException();

            return ParseCatalogResponseFromHtmlPage(htmlDoc, requestUri);
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
                BaseAddress = BaseUrl
            };

            using HttpResponseMessage response = await httpClient.PostAsync(unsortedResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            htmlDoc.Load(stream);

            return ParseCatalogResponseFromHtmlPage(htmlDoc, unsortedResponse.SearchQueryUri);
        }

        public byte PageReloadAttempts { get; set; } = 3;

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
            CatalogResponse catalogFirstPage = null;
            byte pageReloadAttemptsLeft = PageReloadAttempts;

            while (catalogFirstPage is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {PageReloadAttempts} attempts to refresh it");

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
        public async Task<CatalogEntry> GetUpdateDetailsAsync(CatalogSearchResult searchResult, bool throwOnError = true, CancellationToken cancellationToken = default)
        {
            CatalogEntry updateBase = null;
            try
            {
                updateBase = await CreateUpdateObjectAsync(searchResult, PageReloadAttempts, cancellationToken);
            }
            catch
            {
                if (throwOnError)
                    throw;
            }
            finally
            {

            }

            return updateBase;
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
        public async Task<IEnumerable<CatalogSearchResult>> SendSearchQueryAsync(
            string query, 
            bool ignoreDuplicates = true, 
            SortBy sortBy = SortBy.None, 
            SortDirection sortDirection = SortDirection.Descending,
            CancellationToken cancellationToken = default)
        {
            CatalogResponse lastCatalogResponse = null;
            byte pageReloadAttemptsLeft = PageReloadAttempts;
            
            while (lastCatalogResponse is null)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"Search results page was not successfully loaded after {PageReloadAttempts} attempts to refresh it");

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
                    return new List<CatalogSearchResult>();
                }
            }

            if (sortBy is not SortBy.None)
            {
                // This will sort results in the ascending order
                lastCatalogResponse = await SortSearchResults(query, lastCatalogResponse, sortBy, cancellationToken);
            
                if (sortDirection is SortDirection.Descending)
                {
                    // The only way to sort results in the descending order is to send the same request again 
                    lastCatalogResponse = await SortSearchResults(query, lastCatalogResponse, sortBy, cancellationToken);
                }
            }

            pageReloadAttemptsLeft = PageReloadAttempts;
            
            while (!lastCatalogResponse.IsFinalPage)
            {
                if (pageReloadAttemptsLeft == 0)
                    throw new CatalogErrorException($"One of the search result pages was not successfully loaded after {PageReloadAttempts} attempts to refresh it");

                try
                {
                    List<CatalogSearchResult> lst = new(lastCatalogResponse.SearchResults);
                    lastCatalogResponse = await ParseNextCatalogResponseAsync(lastCatalogResponse, cancellationToken);
                    lst.AddRange(lastCatalogResponse.SearchResults);
                    lastCatalogResponse.SearchResults = lst;
                    lastCatalogResponse.ResultsCount = lst.Count;

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
                return lastCatalogResponse.SearchResults.DistinctBy(result => (result.SizeInBytes, result.Title));

            return lastCatalogResponse.SearchResults;
        }
       
        public static async Task<DownloadResult> DownloadAsync(CatalogEntry update, DirectoryInfo destination = null, ByteCountProgress progress = null, CancellationToken cancellationToken = default)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            if (!update.DownloadLinks.Any())
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
                    progress.TotalSize = update.SizeInBytes;
                    handler.HttpReceiveProgress += (se, ev) => progress?.Report(ev.BytesTransferred);
                    httpClient = new HttpClient(handler);
                }
                else
                {
                    httpClient = new HttpClient();
                }
                httpClient.BaseAddress = BaseUrl;

                List<FileSystemInfo> lst = new();
                foreach(string linkStr in update.DownloadLinks)
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
    }
}
