using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal static class LocalizationKeyExtractor
{
    private static readonly Regex PropertyPattern =
        new("^\\s*\"(?<key>[A-Za-z_][A-Za-z0-9_]*)\"\\s*:", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public static List<string> Extract(string content)
    {
        return (from Match match in PropertyPattern.Matches(content) select match.Groups["key"].Value).ToList();
    }
}
