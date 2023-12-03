using System;
using System.Collections.Generic;
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
