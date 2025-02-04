using System;
using System.Collections.Generic;
using System.Linq;

namespace MicrosoftUpdateCatalogClient.Models
{
    public class Driver :
        CatalogEntry
    {
        public string Company { get; set; } = null;

        public string DriverClass { get; set; } = null;

        public string DriverManufacturer { get; set; } = null;

        public string DriverModel { get; set; } = null;

        public string DriverProvider { get; set; } = null;

        public string DriverVersion { get; set; } = null;

        public IEnumerable<string> HardwareIDs { get; set; } = Enumerable.Empty<string>();

        public DateOnly VersionDate { get; set; } = DateOnly.MinValue;

        public Driver()
        {
            
        }
    }
}