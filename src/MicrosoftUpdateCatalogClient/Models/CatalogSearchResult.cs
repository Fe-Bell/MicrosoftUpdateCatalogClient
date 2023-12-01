using HtmlAgilityPack;
using System;
using System.Text.Json.Serialization;

namespace Poushec.UpdateCatalogParser.Models
{
    public class CatalogSearchResult
    {
        public string Title { get; set; }

        public string Products { get; set; }

        public string Classification { get; set; }

        public DateOnly LastUpdated { get; set; }

        public string Version { get; set; }

        public string Size { get; set; }

        public int SizeInBytes { get; set; }

        public string UpdateID { get; set; }

        [JsonConstructor]
        public CatalogSearchResult()
        {
            
        }

        private CatalogSearchResult(
            string title, 
            string products, 
            string classification, 
            DateOnly lastUpdated, 
            string version, 
            string size, 
            int sizeInBytes, 
            string updateId) 
        {
            Title = title;
            Products = products;
            Classification = classification;
            LastUpdated = lastUpdated;
            Version = version;
            Size = size;
            SizeInBytes = sizeInBytes;
            UpdateID = updateId;
        }

        public static CatalogSearchResult ParseFromResultsTableRow(HtmlNode resultsRow)
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

            return new CatalogSearchResult(title, products, classification, lastUpdated, version, size, sizeInBytes, updateID);
        }
    }
} 
