namespace MicrosoftUpdateCatalog.LightAPI.Exceptions;

public class CatalogNoResultsException : System.Exception
{
    public CatalogNoResultsException() : base() { }
    public CatalogNoResultsException(string message) : base(message) { }
} 