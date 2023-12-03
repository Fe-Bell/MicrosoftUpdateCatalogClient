using System;
using System.Collections.Generic;
using System.Linq;
using Poushec.UpdateCatalogParser.Exceptions;

namespace Poushec.UpdateCatalogParser.Models
{
    public class Update : 
        UpdateBase
    {
        public string KBArticleNumbers { get; set; } = string.Empty;

        public string MSRCNumber { get; set; } = string.Empty;

        public string MSRCSeverity { get; set; } = string.Empty;

        public List<string> SupersededBy { get; set; } = new();

        public List<string> Supersedes { get; set; } = new();

        public Update()
        {
            
        }
    }
}