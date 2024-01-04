namespace MicrosoftUpdateCatalog.StandardAPI.Result
{
    public interface IResult<TType>
    {
        TType GetResult();
    }
}
