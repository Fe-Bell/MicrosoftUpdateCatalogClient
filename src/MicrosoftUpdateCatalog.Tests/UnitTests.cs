using MicrosoftUpdateCatalog.StandardAPI;
using MicrosoftUpdateCatalog.StandardAPI.Models;

namespace MicrosoftUpdateCatalog.Tests
{
    public class UnitTests
    {
        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public async Task FirstPageQueryAsync()
        {
            CatalogClient catalogClient = new();

            CatalogResponse result = await catalogClient.SearchFirstPageAsync(".NET CORE");
            Assert.That(result, Is.Not.Null);
            Assert.That(result.SearchResults.Any(), Is.True);
                        
            CatalogEntry entry = await catalogClient.GetResultDetailsAsync(result.SearchResults.FirstOrDefault());
            Assert.That(entry, Is.Not.Null);

            Assert.Pass();
        }

    }
}