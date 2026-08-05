using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace UnityAssetsPatcher.LocalizationGenerator;

/// <summary>
/// Generates a culture-aware, strongly typed localization catalog from locale JSON supplied as additional files.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizedStringsGenerator : IIncrementalGenerator
{
    private const string PrimaryCultureName = "en-US";
    private const string DefaultOutputNamespace = "UnityAssetsPatcher.Localization";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var localeSources = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, cancellationToken) => new LocaleSource(file.Path, file.GetText(cancellationToken)));

        var outputNamespaces = context.AnalyzerConfigOptionsProvider.Select(static (provider, _) =>
        {
            bool hasRootNamespace = provider.GlobalOptions.TryGetValue(
                "build_property.RootNamespace",
                out string? rootNamespace);

            return hasRootNamespace && !string.IsNullOrWhiteSpace(rootNamespace)
                ? $"{rootNamespace}.Localization"
                : DefaultOutputNamespace;
        });

        var inputs = localeSources
            .Collect()
            .Combine(outputNamespaces);

        context.RegisterSourceOutput(inputs,
            static (productionContext, input) => { Execute(productionContext, input.Left, input.Right); });
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<LocaleSource> sources,
        string outputNamespace)
    {
        var locales = new List<LocaleDefinition>();
        bool hasErrors = false;
        bool primarySourceExists = sources.Any(source => string.Equals(
            System.IO.Path.GetFileName(source.Path),
            $"{PrimaryCultureName}.json",
            StringComparison.OrdinalIgnoreCase));

        foreach (LocaleDefinition? locale in sources.Select(source => LocaleFileParser.Parse(source, context)))
        {
            if (locale is null)
            {
                hasErrors = true;

                continue;
            }

            locales.Add(locale);
        }

        if (!ValidateUniqueCultures(context, locales))
        {
            hasErrors = true;
        }

        LocaleDefinition? primaryLocale = locales.FirstOrDefault(locale =>
            string.Equals(locale.CultureName, PrimaryCultureName, StringComparison.OrdinalIgnoreCase));

        if (primaryLocale is null)
        {
            if (!primarySourceExists)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.PrimaryLocaleNotFound, Location.None));
            }

            return;
        }

        if (primaryLocale.Entries.IsEmpty)
        {
            Location location = LocaleFileParser.CreateLocation(primaryLocale.Source, null);

            context.ReportDiagnostic(Diagnostic.Create(Descriptors.PrimaryLocaleEmpty, location));

            return;
        }

        if (!ValidateLocaleContracts(context, primaryLocale, locales))
        {
            hasErrors = true;
        }

        if (hasErrors)
        {
            return;
        }

        string generatedSource = GenerateSource(outputNamespace, primaryLocale, locales);

        context.AddSource("LocalizedStrings.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
    }

    private static bool ValidateUniqueCultures(SourceProductionContext context, List<LocaleDefinition> locales)
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool isValid = true;

        foreach (LocaleDefinition locale in locales)
        {
            if (cultures.Add(locale.CultureName))
            {
                continue;
            }

            Location location = LocaleFileParser.CreateLocation(locale.Source, null);

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.DuplicateLocale,
                location,
                locale.CultureName));

            isValid = false;
        }

        return isValid;
    }

    private static bool ValidateLocaleContracts(
        SourceProductionContext context,
        LocaleDefinition primaryLocale,
        List<LocaleDefinition> locales)
    {
        var primaryEntries = primaryLocale.Entries.ToDictionary(
            entry => entry.Key,
            StringComparer.Ordinal);

        bool isValid = true;

        foreach (LocaleDefinition locale in locales)
        {
            if (ReferenceEquals(locale, primaryLocale))
            {
                continue;
            }

            var localeEntries = locale.Entries.ToDictionary(
                entry => entry.Key,
                StringComparer.Ordinal);

            isValid &= ReportKeyDifferences(context, locale, primaryEntries, localeEntries);

            isValid &= ReportPlaceholderDifferences(context, locale, primaryEntries, localeEntries);
        }

        return isValid;
    }

    private static bool ReportKeyDifferences(
        SourceProductionContext context,
        LocaleDefinition locale,
        Dictionary<string, LocalizedEntry> primaryEntries,
        Dictionary<string, LocalizedEntry> localeEntries)
    {
        bool isValid = true;

        foreach (string key in localeEntries.Keys.Where(key => !primaryEntries.ContainsKey(key)))
        {
            ReportLocaleDiagnostic(context, Descriptors.LocaleHasExtraKey, locale, key);
            isValid = false;
        }

        foreach (string key in primaryEntries.Keys.Where(key => !localeEntries.ContainsKey(key)))
        {
            ReportLocaleDiagnostic(context, Descriptors.LocaleMissingKey, locale, key);
            isValid = false;
        }

        return isValid;
    }

    private static bool ReportPlaceholderDifferences(
        SourceProductionContext context,
        LocaleDefinition locale,
        Dictionary<string, LocalizedEntry> primaryEntries,
        Dictionary<string, LocalizedEntry> localeEntries)
    {
        bool isValid = true;

        foreach (var pair in primaryEntries)
        {
            if (!localeEntries.TryGetValue(pair.Key, out LocalizedEntry? localeEntry))
            {
                continue;
            }

            var primaryPlaceholders = new HashSet<string>(pair.Value.Placeholders, StringComparer.Ordinal);
            var localePlaceholders = new HashSet<string>(localeEntry.Placeholders, StringComparer.Ordinal);

            if (primaryPlaceholders.SetEquals(localePlaceholders))
            {
                continue;
            }

            ReportLocaleDiagnostic(context, Descriptors.PlaceholderMismatch, locale, pair.Key);
            isValid = false;
        }

        return isValid;
    }

    private static void ReportLocaleDiagnostic(
        SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        LocaleDefinition locale,
        string key)
    {
        string fileName = System.IO.Path.GetFileName(locale.Source.Path);
        Location location = LocaleFileParser.CreateLocation(locale.Source, key);

        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, fileName, key));
    }

    private static string GenerateSource(
        string outputNamespace,
        LocaleDefinition primaryLocale,
        List<LocaleDefinition> locales)
    {
        var builder = new StringBuilder();

        AppendHeader(builder, outputNamespace);

        AppendAccessors(builder, primaryLocale);

        AppendLookup(builder, primaryLocale, locales);

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void AppendHeader(StringBuilder builder, string outputNamespace)
    {
        builder.AppendLine("// <auto-generated />");

        builder.AppendLine("#nullable enable");

        builder.AppendLine();

        builder.AppendLine("using System;");

        builder.AppendLine("using System.Globalization;");

        builder.AppendLine();

        builder.AppendLine($"namespace {outputNamespace};");

        builder.AppendLine();

        builder.AppendLine("internal sealed class LocalizedStrings");

        builder.AppendLine("{");

        builder.AppendLine("    internal CultureInfo Culture { get; }");

        builder.AppendLine();

        builder.AppendLine("    internal LocalizedStrings(CultureInfo culture)");

        builder.AppendLine("    {");

        builder.AppendLine("        ArgumentNullException.ThrowIfNull(culture);");

        builder.AppendLine();

        builder.AppendLine("        Culture = culture;");

        builder.AppendLine("    }");

        builder.AppendLine();
    }

    private static void AppendAccessors(StringBuilder builder, LocaleDefinition primaryLocale)
    {
        foreach (LocalizedEntry entry in primaryLocale.Entries)
        {
            string keyLiteral = SymbolDisplay.FormatLiteral(entry.Key, quote: true);

            if (entry.Placeholders.IsEmpty)
            {
                builder.AppendLine($"    internal string {entry.Key} => GetString({keyLiteral});");

                builder.AppendLine();

                continue;
            }

            string parameters = string.Join(
                ", ",
                entry.Placeholders.Select(placeholder => $"object? {placeholder}"));

            string arguments = string.Join(", ", entry.Placeholders);

            builder.AppendLine($"    internal string {entry.Key}({parameters})");

            builder.AppendLine("    {");

            builder.AppendLine(
                $"        return string.Format(this.Culture, GetString({keyLiteral}), " +
                $"new object?[] {{ {arguments} }});");

            builder.AppendLine("    }");

            builder.AppendLine();
        }
    }

    private static void AppendLookup(
        StringBuilder builder,
        LocaleDefinition primaryLocale,
        List<LocaleDefinition> locales)
    {
        builder.AppendLine("    internal string GetFormat(string key)");

        builder.AppendLine("    {");

        builder.AppendLine("        return GetString(key);");

        builder.AppendLine("    }");

        builder.AppendLine();

        builder.AppendLine("    private string GetString(string key)");

        builder.AppendLine("    {");

        builder.AppendLine("        CultureInfo currentCulture = Culture;");

        builder.AppendLine();

        builder.AppendLine("        while (!Equals(currentCulture, CultureInfo.InvariantCulture))");

        builder.AppendLine("        {");

        builder.AppendLine("            string? value = GetStringOrDefault(currentCulture.Name, key);");

        builder.AppendLine();

        builder.AppendLine("            if (value is not null)");

        builder.AppendLine("            {");

        builder.AppendLine("                return value;");

        builder.AppendLine("            }");

        builder.AppendLine();

        builder.AppendLine("            currentCulture = currentCulture.Parent;");

        builder.AppendLine("        }");

        builder.AppendLine();

        builder.AppendLine($"        return GetStringOrDefault(\"{PrimaryCultureName}\", key) ?? key;");

        builder.AppendLine("    }");

        builder.AppendLine();

        builder.AppendLine("    private static string? GetStringOrDefault(string cultureName, string key)");

        builder.AppendLine("    {");

        builder.AppendLine("        return cultureName switch");

        builder.AppendLine("        {");

        foreach (LocaleDefinition locale in locales.OrderBy(
                     locale => locale.CultureName,
                     StringComparer.Ordinal))
        {
            AppendLocaleLookup(builder, primaryLocale, locale);
        }

        builder.AppendLine("            _ => null");

        builder.AppendLine("        };");

        builder.AppendLine("    }");
    }

    private static void AppendLocaleLookup(
        StringBuilder builder,
        LocaleDefinition primaryLocale,
        LocaleDefinition locale)
    {
        string cultureLiteral = SymbolDisplay.FormatLiteral(locale.CultureName, quote: true);
        var entries = locale.Entries.ToDictionary(
            entry => entry.Key,
            StringComparer.Ordinal);

        builder.AppendLine($"            {cultureLiteral} => key switch");

        builder.AppendLine("            {");

        foreach (LocalizedEntry primaryEntry in primaryLocale.Entries)
        {
            LocalizedEntry entry = entries[primaryEntry.Key];
            string keyLiteral = SymbolDisplay.FormatLiteral(entry.Key, quote: true);
            string value = CreateGeneratedValue(primaryEntry, entry);
            string valueLiteral = SymbolDisplay.FormatLiteral(value, quote: true);

            builder.AppendLine($"                {keyLiteral} => {valueLiteral},");
        }

        builder.AppendLine("                _ => null");

        builder.AppendLine("            },");
    }

    private static string CreateGeneratedValue(LocalizedEntry primaryEntry, LocalizedEntry localizedEntry)
    {
        if (primaryEntry.Placeholders.IsEmpty)
        {
            return LocalizationFormatParser.CreateDisplayText(localizedEntry.Value);
        }

        var placeholderIndexes = primaryEntry.Placeholders
            .Select((placeholder, index) => new KeyValuePair<string, int>(placeholder, index))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

        return LocalizationFormatParser.CreateCompositeFormat(localizedEntry.Value, placeholderIndexes);
    }
}
