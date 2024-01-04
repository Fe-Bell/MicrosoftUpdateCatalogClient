using MicrosoftUpdateCatalog.Core.Contract;

namespace MicrosoftUpdateCatalogClient
{
    public class ClientConfiguration :
        IClientConfiguration
    {
        public byte PageReloadAttempts { get; set; } = 3;

        public ClientConfiguration()
        {
            
        }
    }
}
