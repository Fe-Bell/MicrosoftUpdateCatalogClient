using System;
using System.Collections.Generic;

namespace MicrosoftUpdateCatalog.Core.Contract
{
    public interface ICatalogEntry
    {
        string GetId();

        string GetName();

        DateOnly GetReleaseDate();

        IEnumerable<string> GetDownloadLinks();
    }
}
