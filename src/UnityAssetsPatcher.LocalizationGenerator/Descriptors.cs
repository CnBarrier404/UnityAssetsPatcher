using Microsoft.CodeAnalysis;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal static class Descriptors
{
    internal static readonly DiagnosticDescriptor PrimaryLocaleNotFound = Create(
        "LOC001",
        "Primary locale file not found",
        "Primary locale file 'en-US.json' was not found");

    internal static readonly DiagnosticDescriptor PrimaryLocaleEmpty = Create(
        "LOC002",
        "Primary locale file is empty",
        "Primary locale file 'en-US.json' contains no entries");

    internal static readonly DiagnosticDescriptor LocaleHasExtraKey = Create(
        "LOC004",
        "Locale file contains an extra key",
        "Locale file '{0}' contains key '{1}', which is not defined by en-US.json");

    internal static readonly DiagnosticDescriptor LocaleMissingKey = Create(
        "LOC005",
        "Locale file is missing a key",
        "Locale file '{0}' is missing key '{1}' defined by en-US.json");

    internal static readonly DiagnosticDescriptor DuplicateKey = Create(
        "LOC006",
        "Locale file contains a duplicate key",
        "Locale file '{0}' contains duplicate key '{1}'");

    internal static readonly DiagnosticDescriptor InvalidKey = Create(
        "LOC007",
        "Locale key cannot be generated",
        "Locale key '{1}' in '{0}' is not a supported C# member name");

    internal static readonly DiagnosticDescriptor InvalidFormat = Create(
        "LOC009",
        "Locale value contains an invalid format",
        "Locale value for key '{1}' in '{0}' has an invalid format: {2}");

    internal static readonly DiagnosticDescriptor DuplicateLocale = Create(
        "LOC010",
        "Locale is defined more than once",
        "Culture '{0}' is defined by more than one locale file");

    internal static readonly DiagnosticDescriptor PlaceholderMismatch = Create(
        "LOC012",
        "Locale placeholders do not match",
        "Placeholders for key '{1}' in '{0}' do not match en-US.json");

    internal static readonly DiagnosticDescriptor LocaleCannotBeRead = Create(
        "LOC013",
        "Locale file cannot be read",
        "Locale file '{0}' could not be read");

    internal static readonly DiagnosticDescriptor InvalidCultureName = Create(
        "LOC014",
        "Locale filename is not a culture name",
        "Locale filename '{0}' is not a recognized culture name");

    private static DiagnosticDescriptor Create(string id, string title, string messageFormat)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            messageFormat,
            "Localization",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
