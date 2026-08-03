using Microsoft.CodeAnalysis.CSharp;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal static class CSharpIdentifier
{
    public static bool IsSupported(string value)
    {
        return SyntaxFacts.IsValidIdentifier(value) && SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
    }
}
