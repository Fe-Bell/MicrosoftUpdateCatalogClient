using HtmlAgilityPack;
using Poushec.UpdateCatalogParser.Exceptions;
using Poushec.UpdateCatalogParser.Models;
using System.IO;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Poushec.UpdateCatalogParser.Serialization;
using System.Text.Json;
using System.Drawing;
using System.Xml.Linq;

namespace Poushec.UpdateCatalogParser
{
    internal static class Parser
    {
        private static async Task<HtmlDocument> GetDetailsPageAsync(string updateId, CancellationToken cancellationToken = default)
        {
            string requestUri = $"https://www.catalog.update.microsoft.com/ScopedViewInline.aspx?updateid={updateId}";

            try
            {
                using HttpClient httpClient = new();

                using HttpResponseMessage response = await httpClient.GetAsync(requestUri, cancellationToken);

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
            string requestUri = "https://www.catalog.update.microsoft.com/DownloadDialog.aspx";

            using HttpClient httpClient = new();

            using HttpRequestMessage request = new(HttpMethod.Post, requestUri);

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

        private static CatalogSearchResult ParseCatalogSearchResultFromResultsTableRow(HtmlNode resultsRow)
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

        private static void ParseCommonDetails(HtmlDocument detailsPage, UpdateBase updateBase)
        {
            if (detailsPage is null)
                throw new ParseHtmlPageException("_parseCommonDetails() failed. _detailsPage is null");

            updateBase.Description = detailsPage.GetElementbyId("ScopedViewHandler_desc").InnerText;

            foreach (string arch in detailsPage.GetElementbyId("archDiv").LastChild.InnerText.Trim().Split(","))
            {
                updateBase.Architectures.Add(arch.Trim());
            }

            foreach (string lang in detailsPage.GetElementbyId("languagesDiv").LastChild.InnerText.Trim().Split(","))
            {
                updateBase.SupportedLanguages.Add(lang.Trim());
            }

            string moreInfoDivContent = detailsPage.GetElementbyId("moreInfoDiv").InnerHtml;
            MatchCollection moreInfoUrlMatches = Validation.UrlValidators.BasicUrlRegex().Matches(moreInfoDivContent);

            if (moreInfoUrlMatches.Any())
            {
                updateBase.MoreInformation = moreInfoUrlMatches.Select(match => match.Value)
                    .Distinct()
                    .ToList();
            }

            string supportUrlDivContent = detailsPage.GetElementbyId("suportUrlDiv").InnerHtml;
            MatchCollection supportUrlMatches = Validation.UrlValidators.BasicUrlRegex().Matches(supportUrlDivContent);

            if (supportUrlMatches.Any())
            {
                updateBase.SupportUrl = supportUrlMatches.Select(match => match.Value)
                    .Distinct()
                    .ToList();
            }

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
                        .Select(x => x.InnerText.Trim())
                        .ToList();
                }

                HtmlNode supersededByDivs = detailsPage.GetElementbyId("supersededbyInfo");
                // If first child isn't a div - than it's just a n/a and there's nothing to gather
                if (!supersededByDivs.FirstChild.InnerText.Trim().Equals("n/a", StringComparison.OrdinalIgnoreCase))
                {
                    update.SupersededBy = supersededByDivs.ChildNodes.Where(node => node.Name == "div").Select(x =>
                    {
                        return x.ChildNodes[1].GetAttributeValue("href", "").Replace("ScopedViewInline.aspx?updateid=", "");
                    }).ToList();
                }
            }
            catch (Exception ex)
            {
                throw new ParseHtmlPageException("Failed to parse Update details", ex);
            }
        }

        private static void ParseDownloadLinks(string downloadPageContent, UpdateBase updateBase)
        {
            if (updateBase == null)
                ArgumentException.ThrowIfNullOrEmpty(nameof(updateBase));

            MatchCollection downloadLinkMatches = Validation.UrlValidators.DownloadLinkRegex().Matches(downloadPageContent);
            if (!downloadLinkMatches.Any())
                throw new UnableToCollectUpdateDetailsException("Downloads page does not contains any valid download links");

            updateBase.DownloadLinks = downloadLinkMatches.Select(mt => mt.Value).ToList();
        }

        private static void ParseDriverDetails(HtmlDocument detailsPage, Driver driver)
        {
            if (detailsPage is null)
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");

            try
            {
                if (ParseHardwareIDs(detailsPage) is List<string> hw)
                    driver.HardwareIDs = hw;
                else
                    driver.HardwareIDs = new(ParseHardwareIDs(detailsPage));

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

        internal static async Task<UpdateBase> CreateUpdateObjectAsync(CatalogSearchResult catalogSearchResult, byte pageReloadAttempts = 3, CancellationToken cancellationToken = default)
        {
            HtmlDocument _detailsPage = await Parser.GetDetailsPageAsync(catalogSearchResult.UpdateID, cancellationToken);
            string downloadPageContent = await Parser.GetDownloadPageContentAsync(catalogSearchResult.UpdateID, cancellationToken);

            UpdateBase obj;
            if (catalogSearchResult.Classification.Contains("Driver", StringComparison.OrdinalIgnoreCase))
            {
                Driver driver = new();
                Parser.ParseDriverDetails(_detailsPage, driver);
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
                            Parser.ParseDefaultUpdateDetails(_detailsPage, update);
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
            obj.Products = catalogSearchResult.Products.Trim().Split(",").ToList();

            //Parser.ParseCommonDetails(_detailsPage, obj);
            Parser.ParseDownloadLinks(downloadPageContent, obj);

            byte pageReloadAttemptsLeft = pageReloadAttempts;
            while (true)
            {
                try
                {
                    Parser.ParseCommonDetails(_detailsPage, obj);
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
        
        internal static CatalogResponse ParseCatalogResponseFromHtmlPage(HtmlDocument htmlDoc, string searchQueryUri)
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
                CatalogSearchResult catalogSearchResult = Parser.ParseCatalogSearchResultFromResultsTableRow(resultsRow);
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
        internal static async Task<CatalogResponse> ParseNextCatalogResponseAsync(CatalogResponse lastCatalogResponse, CancellationToken cancellationToken = default)
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

            using HttpClient httpClient = new();
            using HttpResponseMessage response = await httpClient.PostAsync(lastCatalogResponse.SearchQueryUri, requestContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            HtmlDocument htmlDoc = new();

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            htmlDoc.Load(stream);

            return ParseCatalogResponseFromHtmlPage(htmlDoc, lastCatalogResponse.SearchQueryUri);
        }
    }
}
