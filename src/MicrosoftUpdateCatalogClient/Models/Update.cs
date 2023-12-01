using System;
using System.Collections.Generic;
using System.Linq;
using Poushec.UpdateCatalogParser.Exceptions;

namespace Poushec.UpdateCatalogParser.Models
{
    public class Update : UpdateBase
    {
        private List<string> ParseSupersededByList()
        {
            if (_detailsPage is null)
            {
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");
            }

            var supersededByDivs = _detailsPage.GetElementbyId("supersededbyInfo");
            var supersededBy = new List<string>();

            // If first child isn't a div - than it's just a n/a and there's nothing to gather
            if (supersededByDivs.FirstChild.InnerText.Trim() == "n/a")
            {
                return supersededBy;
            }

            supersededByDivs.ChildNodes
                .Where(node => node.Name == "div")
                .ToList()
                .ForEach(node =>
                {
                    var updateId = node.ChildNodes[1]
                        .GetAttributeValue("href", "")
                        .Replace("ScopedViewInline.aspx?updateid=", "");

                    supersededBy.Add(updateId);
                });

            return supersededBy;
        }

        private List<string> ParseSupersedesList()
        {
            if (_detailsPage is null)
            {
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");
            }

            var supersedesDivs = _detailsPage.GetElementbyId("supersedesInfo");
            var supersedes = new List<string>();

            // If first child isn't a div - than it's just a n/a and there's nothing to gather
            if (supersedesDivs.FirstChild.InnerText.Trim() == "n/a")
            {
                return supersedes;
            }

            supersedesDivs.ChildNodes
                .Where(node => node.Name == "div")
                .ToList()
                .ForEach(node =>
                {
                    supersedes.Add(node.InnerText.Trim());
                });

            return supersedes;
        }

        private void ParseUpdateDetails()
        {
            if (_detailsPage is null)
            {
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");
            }

            try
            {
                MSRCNumber = _detailsPage.GetElementbyId("securityBullitenDiv").LastChild.InnerText.Trim();
                MSRCSeverity = _detailsPage.GetElementbyId("ScopedViewHandler_msrcSeverity").InnerText;
                KBArticleNumbers = _detailsPage.GetElementbyId("kbDiv").LastChild.InnerText.Trim();
                SupersededBy = ParseSupersededByList();
                Supersedes = ParseSupersedesList();
            }
            catch (Exception ex)
            {
                throw new ParseHtmlPageException("Failed to parse Update details", ex);
            }
        }

        public string KBArticleNumbers { get; set; } = string.Empty;

        public string MSRCNumber { get; set; } = string.Empty;

        public string MSRCSeverity { get; set; } = string.Empty;

        public List<string> SupersededBy { get; set; } = new();

        public List<string> Supersedes { get; set; } = new();

        public Update(UpdateBase updateBase) : base(updateBase) 
        {
            ParseUpdateDetails();
        }
    }
}