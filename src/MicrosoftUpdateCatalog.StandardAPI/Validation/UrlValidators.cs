using System.Text.RegularExpressions;

namespace MicrosoftUpdateCatalog.StandardAPI.Validation
{
    internal partial class UrlValidators
    {
        [GeneratedRegex("https?:\\/\\/(www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b([-a-zA-Z0-9()@:%_\\+.~#?&//=]*)")]
        public static partial Regex BasicUrlRegex();

        [GeneratedRegex("(http[s]?\\://dl\\.delivery\\.mp\\.microsoft\\.com\\/[^\\'\\\"]*)|(http[s]?\\://download\\.windowsupdate\\.com\\/[^\\'\\\"]*)|(http[s]://catalog\\.s\\.download\\.windowsupdate\\.com.*?(?=\\'))")]
        public static partial Regex DownloadLinkRegex();
    }
}
