using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Poushec.UpdateCatalogParser.Exceptions;
using Poushec.UpdateCatalogParser.Serialization;

namespace Poushec.UpdateCatalogParser.Models
{
    /// <summary>
    /// Class represents the shared content of Update Details page of any Update type (classification)
    /// </summary>
    public class UpdateBase
    {
        protected HtmlDocument _detailsPage;

        private async Task<string> GetDownloadPageContent(HttpClient client)
        {
            string requestUri = "https://www.catalog.update.microsoft.com/DownloadDialog.aspx";

            using HttpRequestMessage request = new(HttpMethod.Post, requestUri);
                       
            DownloadPageContentPostObject downloadPageContentPostObject = new()
            {
                UidInfo = UpdateID,
                UpdateID = UpdateID
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

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (TaskCanceledException)
            {
                throw new RequestToCatalogTimedOutException();
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        protected async Task GetDetailsPage(HttpClient client)
        {
            string requestUri = $"https://www.catalog.update.microsoft.com/ScopedViewInline.aspx?updateid={UpdateID}";

            HttpResponseMessage response;

            try
            {
                response = await client.SendAsync(new HttpRequestMessage() { RequestUri = new Uri(requestUri) });
            }
            catch (TaskCanceledException ex)
            {
                throw new RequestToCatalogTimedOutException("Catalog was not responded", ex);
            }

            if (!response.IsSuccessStatusCode)
                throw new UnableToCollectUpdateDetailsException($"Catalog responded with {response.StatusCode} code");

            HtmlDocument tempPage = new();
            tempPage.Load(await response.Content.ReadAsStreamAsync());
            HtmlNode errorDiv = tempPage.GetElementbyId("errorPageDisplayedError");

            if (errorDiv != null)
            {
                string errorCode = errorDiv.LastChild.InnerText.Trim().Replace("]", "");

                if (errorCode == "8DDD0010")
                {
                    throw new UnableToCollectUpdateDetailsException("Catalog cannot proceed your request right now. Send request again later");
                }
                else if (errorCode == "8DDD0024")
                {
                    throw new UpdateWasNotFoundException("Update by this UpdateID does not exists or was removed");
                }
                else
                {
                    throw new CatalogErrorException($"Catalog returned unknown error code: {errorCode}");
                }
            }

            _detailsPage = tempPage;
        }

        protected void ParseCommonDetails()
        {
            if (_detailsPage is null)
            {
                throw new ParseHtmlPageException("_parseCommonDetails() failed. _detailsPage is null");
            }

            Description = _detailsPage.GetElementbyId("ScopedViewHandler_desc").InnerText;

            _detailsPage.GetElementbyId("archDiv")
                .LastChild
                .InnerText.Trim()
                .Split(",")
                .ToList()
                .ForEach(arch =>
                {
                    Architectures.Add(arch.Trim());
                });

            _detailsPage.GetElementbyId("languagesDiv")
                .LastChild
                .InnerText.Trim()
                .Split(",")
                .ToList()
                .ForEach(lang =>
                {
                    SupportedLanguages.Add(lang.Trim());
                });

            string moreInfoDivContent = _detailsPage.GetElementbyId("moreInfoDiv").InnerHtml;
            MatchCollection moreInfoUrlMatches = Validation.UrlValidators.BasicUrlRegex().Matches(moreInfoDivContent);

            if (moreInfoUrlMatches.Any())
            {
                MoreInformation = moreInfoUrlMatches.Select(match => match.Value)
                    .Distinct()
                    .ToList();
            }

            string supportUrlDivContent = _detailsPage.GetElementbyId("suportUrlDiv").InnerHtml;
            MatchCollection supportUrlMatches = Validation.UrlValidators.BasicUrlRegex().Matches(supportUrlDivContent);

            if (supportUrlMatches.Any())
            {
                SupportUrl = supportUrlMatches.Select(match => match.Value)
                    .Distinct()
                    .ToList();
            }

            RestartBehavior = _detailsPage.GetElementbyId("ScopedViewHandler_rebootBehavior").InnerText;

            MayRequestUserInput = _detailsPage.GetElementbyId("ScopedViewHandler_userInput").InnerText;

            MustBeInstalledExclusively = _detailsPage.GetElementbyId("ScopedViewHandler_installationImpact").InnerText;

            RequiresNetworkConnectivity = _detailsPage.GetElementbyId("ScopedViewHandler_connectivity").InnerText;

            HtmlNode uninstallNotesDiv = _detailsPage.GetElementbyId("uninstallNotesDiv");

            if (uninstallNotesDiv.ChildNodes.Count == 3)
            {
                UninstallNotes = uninstallNotesDiv.LastChild.InnerText.Trim();
            }
            else
            {
                UninstallNotes = _detailsPage.GetElementbyId("uninstallNotesDiv")
                    .ChildNodes[3]
                    .InnerText.Trim();
            }

            UninstallSteps = _detailsPage.GetElementbyId("uninstallStepsDiv")
                .LastChild
                .InnerText.Trim();
        }

        protected void ParseDownloadLinks(string downloadPageContent)
        {
            MatchCollection downloadLinkMatches = Validation.UrlValidators.DownloadLinkRegex().Matches(downloadPageContent);

            if (!downloadLinkMatches.Any())
                throw new UnableToCollectUpdateDetailsException("Downloads page does not contains any valid download links");

            DownloadLinks = downloadLinkMatches.Select(mt => mt.Value).ToList();
        }

        internal UpdateBase(CatalogSearchResult resultRow)
        {
            UpdateID = resultRow.UpdateID;
            Title = resultRow.Title;
            Classification = resultRow.Classification;
            LastUpdated = resultRow.LastUpdated;
            Size = resultRow.Size;
            SizeInBytes = resultRow.SizeInBytes;
            Products = resultRow.Products.Trim().Split(",").ToList();
        }

        internal UpdateBase(UpdateBase updateBase)
        {
            _detailsPage = updateBase._detailsPage;
            Title = updateBase.Title;
            UpdateID = updateBase.UpdateID;
            Products = updateBase.Products;
            Classification = updateBase.Classification;
            LastUpdated = updateBase.LastUpdated;
            Size = updateBase.Size;
            SizeInBytes = updateBase.SizeInBytes;
            DownloadLinks = updateBase.DownloadLinks;
            Description = updateBase.Description;
            Architectures = updateBase.Architectures;
            SupportedLanguages = updateBase.SupportedLanguages;
            MoreInformation = updateBase.MoreInformation;
            SupportUrl = updateBase.SupportUrl;
            RestartBehavior = updateBase.RestartBehavior;
            MayRequestUserInput = updateBase.MayRequestUserInput;
            MustBeInstalledExclusively = updateBase.MustBeInstalledExclusively;
            RequiresNetworkConnectivity = updateBase.RequiresNetworkConnectivity;
            UninstallNotes = updateBase.UninstallNotes;
            UninstallSteps = updateBase.UninstallSteps;
        }

        internal async Task ParseCommonDetails(HttpClient client)
        {
            await GetDetailsPage(client);
            string downloadPageContent = await GetDownloadPageContent(client);

            ParseCommonDetails();
            ParseDownloadLinks(downloadPageContent);
        }

        public List<string> Architectures { get; set; } = new();

        public string Classification { get; set; }

        // Info from details page
        public string Description { get; set; } = string.Empty;

        // Download links from download page
        public List<string> DownloadLinks { get; set; } = new();

        public DateOnly LastUpdated { get; set; }

        public string MayRequestUserInput { get; set; } = string.Empty;

        public List<string> MoreInformation { get; set; } = new();

        public string MustBeInstalledExclusively { get; set; } = string.Empty;

        public List<string> Products { get; set; }

        public string RequiresNetworkConnectivity { get; set; } = string.Empty;

        public string RestartBehavior { get; set; } = string.Empty;

        public string Size { get; set; }

        public int SizeInBytes { get; set; }

        public List<string> SupportedLanguages { get; set; } = new();

        public List<string> SupportUrl { get; set; } = new();

        // Info from search results
        public string Title { get; set; }

        public string UninstallNotes { get; set; } = string.Empty;

        public string UninstallSteps { get; set; } = string.Empty;

        public string UpdateID { get; set; }
    }
}
