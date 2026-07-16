using Microsoft.CodeAnalysis;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal static class Descriptors
{
    internal static readonly DiagnosticDescriptor PrimaryLocaleNotFound = new(
        "LOC001",
        "Primary locale file not found",
        "Primary locale file '{0}' was not found in AdditionalFiles",
        "Localization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor PrimaryLocaleEmpty = new(
        "LOC002",
        "Primary locale file is empty",
        "Primary locale file '{0}' contains no keys",
        "Localization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LocaleHasExtraKey = new(
        "LOC004",
        "Locale file has extra key not in primary",
        "Locale file '{0}' contains key '{1}' which is not defined in the primary locale",
        "Localization",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor LocaleMissingKey = new(
        "LOC005",
        "Locale file is missing a key from primary",
        "Locale file '{0}' is missing key '{1}' defined in the primary locale",
        "Localization",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
