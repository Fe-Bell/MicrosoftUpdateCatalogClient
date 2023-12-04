using System.Text.RegularExpressions;

namespace MicrosoftUpdateCatalogClient.Validation
{
    internal partial class NumericValidators
    {

        [GeneratedRegex("(?<=of )\\d{1,4}")]
        public static partial Regex ResultCountRegex();
    }
}
