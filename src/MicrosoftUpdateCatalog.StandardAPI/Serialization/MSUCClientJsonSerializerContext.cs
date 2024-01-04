using System.Text.Json.Serialization;

namespace MicrosoftUpdateCatalog.StandardAPI.Serialization
{
    /// <summary>
    /// This class represents a source generator for all serializable objects to be used with System.Text.Json.
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true, 
        PropertyNamingPolicy = JsonKnownNamingPolicy.Unspecified,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    //[JsonSerializable(typeof(Models.CatalogResponse))]
    //[JsonSerializable(typeof(Models.CatalogSearchResult))]
    //[JsonSerializable(typeof(Models.Driver))]
    [JsonSerializable(typeof(Models.DownloadPageContentPostObject))]
    //[JsonSerializable(typeof(Models.Update))]
    //[JsonSerializable(typeof(Models.UpdateBase))]
    internal partial class MSUCClientJsonSerializerContext :
        JsonSerializerContext
    {
        //Please read through https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation?pivots=dotnet-8-0
    
        //Source generators are required in order to make libraries compatible with AoT compilation
        //By default, System.Text.Json will use reflection to resolve POCO classes to json
        //We disable it by adding a setting to the project file
        //This is very important for .NET 8 projects that derive from this.
    }
}
