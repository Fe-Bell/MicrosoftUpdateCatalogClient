using System.Text.RegularExpressions;

namespace Poushec.UpdateCatalogParser.Validation
{
    internal partial class NumericValidators
    {

        [GeneratedRegex("(?<=of )\\d{1,4}")]
        public static partial Regex ResultCountRegex();
    }
}
