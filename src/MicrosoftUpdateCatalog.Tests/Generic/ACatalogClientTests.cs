using MicrosoftUpdateCatalog.Core.Contract;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MicrosoftUpdateCatalog.Tests.Generic
{
    internal abstract class ACatalogClientTests
    {
        protected ICatalogClient _catalogClient = null;

        protected ACatalogClientTests(ICatalogClient catalogClient)
        { 
            _catalogClient = catalogClient;
        }

        [Test]
        public async Task BasicSearchWithoutOptions()
        {
            IEnumerable<ICatalogEntry> entries = await _catalogClient.SearchAsync(".NET CORE");

            Assert.That(entries, Is.Not.Null);

            Assert.That(entries.Any(), Is.True);

            Assert.Pass();
        }
    }
}
