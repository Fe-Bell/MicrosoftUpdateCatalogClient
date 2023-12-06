namespace MicrosoftUpdateCatalogClient.Result
{
    public interface IResult<TType>
    {
        TType GetResult();
    }
}
