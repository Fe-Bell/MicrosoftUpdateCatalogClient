using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Poushec.UpdateCatalogParser.Models
{
    /// <summary>
    /// Class represents the shared content of Update Details page of any Update type (classification)
    /// </summary>
    [JsonDerivedType(typeof(Driver), nameof(Driver))]
    [JsonDerivedType(typeof(Update), nameof(Update))]
    public class UpdateBase
    {
        protected UpdateBase()
        {

        }

        protected UpdateBase(UpdateBase updateBase)
        {
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

        public int SizeInBytes { get; set; } = default;

        public IEnumerable<string> SupportedLanguages { get; set; } = Enumerable.Empty<string>();

        public IEnumerable<string> SupportUrl { get; set; } = Enumerable.Empty<string>();

        // Info from search results
        public string Title { get; set; }

        public string UninstallNotes { get; set; } = null;

        public string UninstallSteps { get; set; } = null;

        public string UpdateID { get; set; }
    }
}
