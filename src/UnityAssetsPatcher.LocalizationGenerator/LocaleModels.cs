using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal sealed class LocaleSource
{
    public string Path { get; }

    public SourceText? Content { get; }

    public LocaleSource(string path, SourceText? content)
    {
        Path = path;
        Content = content;
    }
}

internal sealed class LocaleDefinition
{
    public LocaleSource Source { get; }

    public string CultureName { get; }

    public ImmutableArray<LocalizedEntry> Entries { get; }

    public LocaleDefinition(LocaleSource source, string cultureName, ImmutableArray<LocalizedEntry> entries)
    {
        Source = source;
        CultureName = cultureName;
        Entries = entries;
    }
}

internal sealed class LocalizedEntry
{
    public string Key { get; }

    public string Value { get; }

    public ImmutableArray<string> Placeholders { get; }

    public LocalizedEntry(string key, string value, ImmutableArray<string> placeholders)
    {
        Key = key;
        Value = value;
        Placeholders = placeholders;
    }
}
