using MicrosoftUpdateCatalog.Core.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MicrosoftUpdateCatalogClient.Models
{
    /// <summary>
    /// Class represents the shared content of Update Details page of any Update type (classification)
    /// </summary>
    [JsonDerivedType(typeof(Driver), nameof(Driver))]
    [JsonDerivedType(typeof(Update), nameof(Update))]
    public class CatalogEntry :
        ICatalogEntry
    {
        protected CatalogEntry()
        {

        }

        protected CatalogEntry(CatalogEntry entry)
        {
            Title = entry.Title;
            UpdateID = entry.UpdateID;
            Products = entry.Products;
            Classification = entry.Classification;
            LastUpdated = entry.LastUpdated;
            Size = entry.Size;
            SizeInBytes = entry.SizeInBytes;
            DownloadLinks = entry.DownloadLinks;
            Description = entry.Description;
            Architectures = entry.Architectures;
            SupportedLanguages = entry.SupportedLanguages;
            MoreInformation = entry.MoreInformation;
            SupportUrl = entry.SupportUrl;
            RestartBehavior = entry.RestartBehavior;
            MayRequestUserInput = entry.MayRequestUserInput;
            MustBeInstalledExclusively = entry.MustBeInstalledExclusively;
            RequiresNetworkConnectivity = entry.RequiresNetworkConnectivity;
            UninstallNotes = entry.UninstallNotes;
            UninstallSteps = entry.UninstallSteps;
        }

        public IEnumerable<string> Architectures { get; set; } = Enumerable.Empty<string>();

        public string Classification { get; set; } = null;

        // Info from details page
        public string Description { get; set; } = null;

        // Download links from download page
        public IEnumerable<string> DownloadLinks { get; set; } = Enumerable.Empty<string>();

        public DateOnly LastUpdated { get; set; } = default;

        public string MayRequestUserInput { get; set; } = null;

        public IEnumerable<string> MoreInformation { get; set; } = Enumerable.Empty<string>();

        public string MustBeInstalledExclusively { get; set; } = null;

        public IEnumerable<string> Products { get; set; } = Enumerable.Empty<string>();

        public string RequiresNetworkConnectivity { get; set; } = null;

        public string RestartBehavior { get; set; } = null;

        public string Size { get; set; } = null;

        public long SizeInBytes { get; set; } = default;

        public IEnumerable<string> SupportedLanguages { get; set; } = Enumerable.Empty<string>();

        public IEnumerable<string> SupportUrl { get; set; } = Enumerable.Empty<string>();

        // Info from search results
        public string Title { get; set; }

        public string UninstallNotes { get; set; } = null;

        public string UninstallSteps { get; set; } = null;

        public string UpdateID { get; set; }

        public IEnumerable<string> GetDownloadLinks()
             => DownloadLinks;

        public string GetId()
            => UpdateID;

        public string GetName()
            => Title;

        public DateOnly GetReleaseDate()
            => LastUpdated;
    }
}
