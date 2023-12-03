using System.Collections.Generic;

namespace Poushec.UpdateCatalogParser.Models
{
    public class Update : 
        UpdateBase
    {
        public string KBArticleNumbers { get; set; } = null;

        public string MSRCNumber { get; set; } = null;

        public string MSRCSeverity { get; set; } = null;

        public IEnumerable<string> SupersededBy { get; set; } = null;

        public IEnumerable<string> Supersedes { get; set; } = null;

        public Update()
        {
            
        }
    }
}