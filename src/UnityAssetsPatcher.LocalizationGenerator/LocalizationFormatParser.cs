using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace UnityAssetsPatcher.LocalizationGenerator;

internal static class LocalizationFormatParser
{
    public static bool TryGetPlaceholders(
        string value,
        out ImmutableArray<string> placeholders,
        out string? error)
    {
        ImmutableArray<string>.Builder names = ImmutableArray.CreateBuilder<string>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;

        while (index < value.Length)
        {
            char current = value[index];

            switch (current)
            {
                case '{' when IsEscapedBrace(value, index, '{'):
                    index += 2;

                    continue;
                case '{':
                {
                    int closingIndex = value.IndexOf('}', index + 1);

                    if (closingIndex < 0)
                    {
                        placeholders = [];
                        error = "an opening brace is not closed";

                        return false;
                    }

                    string name = value.Substring(index + 1, closingIndex - index - 1);

                    if (!CSharpIdentifier.IsSupported(name))
                    {
                        placeholders = [];
                        error = $"'{name}' is not a valid placeholder name";

                        return false;
                    }

                    if (seenNames.Add(name))
                    {
                        names.Add(name);
                    }

                    index = closingIndex + 1;

                    continue;
                }
                case '}' when IsEscapedBrace(value, index, '}'):
                    index += 2;

                    continue;
                case '}':
                    placeholders = [];
                    error = "a closing brace does not have a matching opening brace";

                    return false;
                default:
                    index++;
                    break;
            }
        }

        placeholders = names.ToImmutable();
        error = null;

        return true;
    }

    public static string CreateCompositeFormat(string value, IReadOnlyDictionary<string, int> placeholderIndexes)
    {
        var builder = new StringBuilder(value.Length);
        int index = 0;

        while (index < value.Length)
        {
            char current = value[index];

            switch (current)
            {
                case '{' when IsEscapedBrace(value, index, '{'):
                    builder.Append("{{");

                    index += 2;

                    continue;
                case '{':
                {
                    int closingIndex = value.IndexOf('}', index + 1);
                    string name = value.Substring(index + 1, closingIndex - index - 1);

                    builder.Append('{');

                    builder.Append(placeholderIndexes[name]);

                    builder.Append('}');

                    index = closingIndex + 1;

                    continue;
                }
                case '}' when IsEscapedBrace(value, index, '}'):
                    builder.Append("}}");

                    index += 2;

                    continue;
                default:
                    builder.Append(current);

                    index++;
                    break;
            }
        }

        return builder.ToString();
    }

    public static string CreateDisplayText(string value)
    {
        return value.Replace("{{", "{").Replace("}}", "}");
    }

    private static bool IsEscapedBrace(string value, int index, char brace)
    {
        return index + 1 < value.Length && value[index + 1] == brace;
    }
}
