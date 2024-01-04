using MicrosoftUpdateCatalog.Core.Contract;

namespace MicrosoftUpdateCatalog.LightAPI
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
