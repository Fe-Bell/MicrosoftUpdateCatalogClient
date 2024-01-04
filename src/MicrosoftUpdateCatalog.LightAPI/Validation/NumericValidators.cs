using System.Text.RegularExpressions;

namespace MicrosoftUpdateCatalog.LightAPI.Valodation
{
    internal partial class NumericValidators
    {

        [GeneratedRegex("(?<=of )\\d{1,4}")]
        public static partial Regex ResultCountRegex();
    }
}
