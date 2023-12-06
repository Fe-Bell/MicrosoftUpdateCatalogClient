using System.IO;

namespace MicrosoftUpdateCatalogClient.Result
{
    public class DownloadResult :
        IResult<FileSystemInfo>
    {
        private readonly FileSystemInfo fileSystemInfo;

        public DownloadResult(FileSystemInfo fileSystemInfo)
        {
            this.fileSystemInfo = fileSystemInfo;
        }

        public FileSystemInfo GetResult() 
            => fileSystemInfo;
    }
}
