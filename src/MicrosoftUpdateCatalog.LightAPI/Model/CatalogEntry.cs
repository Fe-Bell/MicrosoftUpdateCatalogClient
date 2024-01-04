using MicrosoftUpdateCatalog.Core.Contract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MicrosoftUpdateCatalog.LightAPI.Model
{
    /// <summary>
    /// Class represents the shared content of Update Details page of any Update type (classification)
    /// </summary>
    public class CatalogEntry :
        ICatalogEntry
    {
        [JsonConstructor]
        public CatalogEntry()
        {

        }

        public CatalogEntry(CatalogEntry entry)
        {
            Title = entry.Title;
            UpdateID = entry.UpdateID;
            LastUpdated = entry.LastUpdated;
            DownloadLinks = entry.DownloadLinks;
            Size = entry.Size;
            Version = entry.Version;
            EntryType = entry.EntryType;
        }

        public IEnumerable<string> DownloadLinks { get; set; } = Enumerable.Empty<string>();

        public EntryType EntryType { get; set; } = EntryType.Unknown;

        public string Title { get; set; } = null;

        public string UpdateID { get; set; } = null;

        public DateOnly LastUpdated { get; set; } = default;

        public long Size { get; set; } = -1;

        public string Version { get; set; } = null;

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
