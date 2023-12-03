using System;
using System.Collections.Generic;

namespace Poushec.UpdateCatalogParser.Models
{
    public class Driver :
        UpdateBase
    {
        public string Company { get; set; } = null;

        public string DriverClass { get; set; } = null;

        public string DriverManufacturer { get; set; } = null;

        public string DriverModel { get; set; } = null;

        public string DriverProvider { get; set; } = null;

        public string DriverVersion { get; set; } = null;

        public IEnumerable<string> HardwareIDs { get; set; } = null;

        public DateOnly VersionDate { get; set; } = DateOnly.MinValue;

        public Driver()
        {
            
        }
    }
}