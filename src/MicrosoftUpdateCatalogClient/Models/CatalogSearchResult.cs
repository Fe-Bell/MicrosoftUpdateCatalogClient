using System;
using System.Text.Json.Serialization;

namespace Poushec.UpdateCatalogParser.Models
{
    public class CatalogSearchResult
    {
        public string Classification { get; set; } = null;

        public DateOnly LastUpdated { get; set; } = default;

        public string Products { get; set; } = null;

        public string Size { get; set; } = null;

        public int SizeInBytes { get; set; } = default;

        public string Title { get; set; } = null;

        public string UpdateID { get; set; } = null;

        public string Version { get; set; } = null;

        [JsonConstructor]
        internal CatalogSearchResult()
        {
            
        }
    }
} 
