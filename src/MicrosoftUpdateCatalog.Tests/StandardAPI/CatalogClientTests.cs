using MicrosoftUpdateCatalog.StandardAPI;
using MicrosoftUpdateCatalog.StandardAPI.Models;
using System.Linq;
using System.Threading.Tasks;

namespace MicrosoftUpdateCatalog.Tests.StandardAPI
{
    internal class CatalogClientTests :
        Generic.ACatalogClientTests
    {
        public CatalogClientTests() : base(new CatalogClient())
        {
            
        }

        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public async Task FirstPageQueryAsync()
        {
            if (_catalogClient is CatalogClient catalogClient)
            {
                CatalogResponse result = await catalogClient.SearchFirstPageAsync(".NET CORE");
                Assert.That(result, Is.Not.Null);
                Assert.That(result.SearchResults.Any(), Is.True);

                CatalogEntry entry = await catalogClient.GetResultDetailsAsync(result.SearchResults.FirstOrDefault());
                Assert.That(entry, Is.Not.Null);

                Assert.Pass();
            }
            else
                Assert.Fail("CatalogClient is not from StandardAPI");
        }

    }
}