namespace MicrosoftUpdateCatalogClient.Exceptions;

public class CatalogErrorException : System.Exception
{
    public CatalogErrorException() : base() { }
    public CatalogErrorException(string message) : base(message) { }
}