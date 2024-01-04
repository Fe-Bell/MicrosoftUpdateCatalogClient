namespace MicrosoftUpdateCatalog.Core.Contract
{
    public interface IResult<TType>
    {
        TType GetResult();
    }
}
