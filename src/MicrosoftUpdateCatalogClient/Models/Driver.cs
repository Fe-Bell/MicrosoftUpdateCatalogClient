using System;
using System.Collections.Generic;
using System.Linq;
using Poushec.UpdateCatalogParser.Exceptions;

namespace Poushec.UpdateCatalogParser.Models
{
    public class Driver : UpdateBase
    {
        private void ParseDriverDetails()
        {
            if (_detailsPage is null)
            {
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");
            }

            try
            {
                HardwareIDs = ParseHardwareIDs();
                Company = _detailsPage.GetElementbyId("ScopedViewHandler_company").InnerText;
                DriverManufacturer = _detailsPage.GetElementbyId("ScopedViewHandler_manufacturer").InnerText;
                DriverClass = _detailsPage.GetElementbyId("ScopedViewHandler_driverClass").InnerText;
                DriverModel = _detailsPage.GetElementbyId("ScopedViewHandler_driverModel").InnerText;
                DriverProvider = _detailsPage.GetElementbyId("ScopedViewHandler_driverProvider").InnerText;
                DriverVersion = _detailsPage.GetElementbyId("ScopedViewHandler_version").InnerText;
                VersionDate = DateOnly.Parse(_detailsPage.GetElementbyId("ScopedViewHandler_versionDate").InnerText);
            }
            catch (Exception ex)
            {
                throw new ParseHtmlPageException("Failed to parse Driver details", ex);
            }
        }

        private List<string> ParseHardwareIDs()
        {
            if (_detailsPage is null)
            {
                throw new ParseHtmlPageException("Failed to parse update details. _details page is null");
            }

            var hwIdsDivs = _detailsPage.GetElementbyId("driverhwIDs");

            if (hwIdsDivs == null)
            {
                return new List<string>();
            }

            var hwIds = new List<string>();

            hwIdsDivs.ChildNodes
                .Where(node => node.Name == "div")
                .ToList()
                .ForEach(node =>
                {
                    var hid = node.ChildNodes
                        .First().InnerText
                        .Trim()
                        .Replace(@"\r\n", "")
                        .ToUpper();

                    if (!string.IsNullOrEmpty(hid))
                    {
                        hwIds.Add(hid);
                    }
                });

            return hwIds;
        }

        public string Company { get; set; } = string.Empty;

        public string DriverClass { get; set; } = string.Empty;

        public string DriverManufacturer { get; set; } = string.Empty;

        public string DriverModel { get; set; } = string.Empty;

        public string DriverProvider { get; set; } = string.Empty;

        public string DriverVersion { get; set; } = string.Empty;

        public List<string> HardwareIDs { get; set; } = new();

        public DateOnly VersionDate { get; set; } = DateOnly.MinValue;

        public Driver(UpdateBase updateBase) : base(updateBase) 
        {
            ParseDriverDetails();
        }
    }
}