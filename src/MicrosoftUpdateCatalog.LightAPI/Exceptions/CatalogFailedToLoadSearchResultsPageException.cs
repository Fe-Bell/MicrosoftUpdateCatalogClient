namespace MicrosoftUpdateCatalog.LightAPI.Exceptions;

public class CatalogFailedToLoadSearchResultsPageException : System.Exception
{
    public CatalogFailedToLoadSearchResultsPageException() : base() { }
    public CatalogFailedToLoadSearchResultsPageException(string message) : base(message) { }
}