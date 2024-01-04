using MicrosoftUpdateCatalog.Core.Contract;
using System.Collections.Generic;
using System.IO;

namespace MicrosoftUpdateCatalog.LightAPI.Result
{
    public class DownloadResult :
        IResult<IEnumerable<FileSystemInfo>>
    {
        private readonly IEnumerable<FileSystemInfo> result;

        public DownloadResult(params FileSystemInfo[] results)
        {
            result = results;
        }

        public IEnumerable<FileSystemInfo> GetResult() 
            => result;
    }
}
