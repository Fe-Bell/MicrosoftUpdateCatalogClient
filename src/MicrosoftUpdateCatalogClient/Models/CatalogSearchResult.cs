using System;
using System.Text.Json.Serialization;

namespace Poushec.UpdateCatalogParser.Models
{
    public class CatalogSearchResult
    {
        public string Classification { get; set; }

        public DateOnly LastUpdated { get; set; }

        public string Products { get; set; }

        public string Size { get; set; }

        public int SizeInBytes { get; set; }

        public string Title { get; set; }

        public string UpdateID { get; set; }

        public string Version { get; set; }

        [JsonConstructor]
        public CatalogSearchResult()
        {
            
        }
    }
} 
