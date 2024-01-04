﻿using System.Text.Json.Serialization;

namespace MicrosoftUpdateCatalog.LightAPI.Model
{
    internal sealed class DownloadPageContentPostObject
    {
        public string Languages { get; set; } = null;

        public long Size { get; set; } = 0;

        public string UidInfo { get; set; } = null;

        public string UpdateID { get; set; } = null;

        [JsonConstructor]
        public DownloadPageContentPostObject()
        {
            
        }
    }
}
