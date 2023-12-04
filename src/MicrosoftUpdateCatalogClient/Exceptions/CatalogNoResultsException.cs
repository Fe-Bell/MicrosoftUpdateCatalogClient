namespace MicrosoftUpdateCatalogClient.Exceptions;

public class CatalogNoResultsException : System.Exception
{
    public CatalogNoResultsException() : base() { }
    public CatalogNoResultsException(string message) : base(message) { }
} 