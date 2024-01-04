using System.Collections.Generic;
using System.Linq;

namespace MicrosoftUpdateCatalog.StandardAPI.Models
{
    public class Update : 
        CatalogEntry
    {
        public string KBArticleNumbers { get; set; } = null;

        public string MSRCNumber { get; set; } = null;

        public string MSRCSeverity { get; set; } = null;

        public IEnumerable<string> SupersededBy { get; set; } = Enumerable.Empty<string>();

        public IEnumerable<string> Supersedes { get; set; } = Enumerable.Empty<string>();

        public Update()
        {
            
        }
    }
}