using MicrosoftUpdateCatalog.LightAPI;
using MicrosoftUpdateCatalog.LightAPI.Model;
using System.Linq;
using System.Threading.Tasks;

namespace MicrosoftUpdateCatalog.Tests.LightAPI
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
                CatalogResponse result = await catalogClient.SearchFirstPageLightAsync(".NET CORE");
                Assert.Multiple(() =>
                {
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.Results.Any(), Is.True);
                    Assert.That(result.Results.FirstOrDefault(), Is.Not.Null);
                });

                Assert.Pass();
            }
            else
                Assert.Fail("CatalogClient is not from StandardAPI");
        }

    }
}