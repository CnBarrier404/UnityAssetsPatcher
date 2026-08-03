using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal static class LocaleFileParser
{
    private static Regex EntryPattern { get; } = new(
        "\"(?<key>(?:\\\\(?:[\"\\\\/bfnrt]|u[0-9A-Fa-f]{4})|[^\"\\\\])*)\"\\s*:\\s*" +
        "\"(?<value>(?:\\\\(?:[\"\\\\/bfnrt]|u[0-9A-Fa-f]{4})|[^\"\\\\])*)\"",
        RegexOptions.CultureInvariant);

    private static HashSet<string> ReservedMemberNames { get; } = new(StringComparer.Ordinal)
    {
        "LocalizedStrings",
        "Culture",
        "GetString",
        "GetStringOrDefault"
    };

    public static LocaleDefinition? Parse(LocaleSource source, SourceProductionContext context)
    {
        string fileName = Path.GetFileName(source.Path);

        if (source.Content is null)
        {
            Report(context, Descriptors.LocaleCannotBeRead, source, null, fileName);

            return null;
        }

        string cultureName;

        try
        {
            string requestedCultureName = Path.GetFileNameWithoutExtension(source.Path);
            CultureInfo culture = CultureInfo.GetCultureInfo(requestedCultureName);
            cultureName = culture.Name;
        }
        catch (CultureNotFoundException)
        {
            Report(context, Descriptors.InvalidCultureName, source, null, fileName);

            return null;
        }

        return ExtractEntries(source, cultureName, fileName, context);
    }

    private static LocaleDefinition? ExtractEntries(
        LocaleSource source,
        string cultureName,
        string fileName,
        SourceProductionContext context)
    {
        string content = source.Content?.ToString() ?? string.Empty;
        MatchCollection matches = EntryPattern.Matches(content);
        var entries = ImmutableArray.CreateBuilder<LocalizedEntry>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        bool isValid = true;

        foreach (Match match in matches)
        {
            string key = DecodeJsonString(match.Groups["key"].Value);

            if (!keys.Add(key))
            {
                Report(context, Descriptors.DuplicateKey, source, key, fileName, key);
                isValid = false;

                continue;
            }

            if (!CSharpIdentifier.IsSupported(key) || ReservedMemberNames.Contains(key))
            {
                Report(context, Descriptors.InvalidKey, source, key, fileName, key);
                isValid = false;

                continue;
            }

            string value = DecodeJsonString(match.Groups["value"].Value);
            bool isFormatValid = LocalizationFormatParser.TryGetPlaceholders(
                value,
                out ImmutableArray<string> placeholders,
                out string? error);

            if (!isFormatValid)
            {
                Report(context, Descriptors.InvalidFormat, source, key, fileName, key, error ?? string.Empty);
                isValid = false;

                continue;
            }

            entries.Add(new LocalizedEntry(key, value, placeholders));
        }

        if (!isValid)
        {
            return null;
        }

        return new LocaleDefinition(source, cultureName, entries.ToImmutable());
    }

    private static string DecodeJsonString(string value)
    {
        var builder = new StringBuilder(value.Length);
        int index = 0;

        while (index < value.Length)
        {
            char current = value[index];

            if (current != '\\')
            {
                builder.Append(current);
                index++;

                continue;
            }

            char escaped = value[index + 1];

            if (escaped == 'u')
            {
                string hexValue = value.Substring(index + 2, 4);
                int characterValue = int.Parse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                builder.Append((char)characterValue);

                index += 6;

                continue;
            }

            builder.Append(escaped switch
            {
                '"' => '"',
                '\\' => '\\',
                '/' => '/',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => escaped
            });

            index += 2;
        }

        return builder.ToString();
    }

    private static void Report(
        SourceProductionContext context,
        DiagnosticDescriptor descriptor,
        LocaleSource source,
        string? key,
        params object[] arguments)
    {
        Location location = CreateLocation(source, key);

        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, arguments));
    }

    internal static Location CreateLocation(LocaleSource source, string? key)
    {
        if (source.Content is null || key is null)
        {
            return CreateStartLocation(source.Path);
        }

        string content = source.Content.ToString();
        int keyIndex = content.IndexOf($"\"{key}\"", StringComparison.Ordinal);

        if (keyIndex < 0)
        {
            return CreateStartLocation(source.Path);
        }

        var span = new TextSpan(keyIndex, key.Length + 2);
        LinePositionSpan lineSpan = source.Content.Lines.GetLinePositionSpan(span);

        return Location.Create(source.Path, span, lineSpan);
    }

    private static Location CreateStartLocation(string path)
    {
        var span = new TextSpan(0, 0);
        var linePosition = new LinePosition(0, 0);
        var lineSpan = new LinePositionSpan(linePosition, linePosition);

        return Location.Create(path, span, lineSpan);
    }
}
