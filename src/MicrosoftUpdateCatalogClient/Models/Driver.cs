using System;
using System.Collections.Generic;

namespace Poushec.UpdateCatalogParser.Models
{
    public class Driver :
        UpdateBase
    {
        public string Company { get; set; } = string.Empty;

        public string DriverClass { get; set; } = string.Empty;

        public string DriverManufacturer { get; set; } = string.Empty;

        public string DriverModel { get; set; } = string.Empty;

        public string DriverProvider { get; set; } = string.Empty;

        public string DriverVersion { get; set; } = string.Empty;

        public List<string> HardwareIDs { get; set; } = new();

        public DateOnly VersionDate { get; set; } = DateOnly.MinValue;

        public Driver()
        {
            
        }
    }
}