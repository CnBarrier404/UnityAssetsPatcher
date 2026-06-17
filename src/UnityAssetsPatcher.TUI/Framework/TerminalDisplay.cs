namespace UnityAssetsPatcher.TUI.Framework;

internal static class TerminalDisplay
{
    public static IReadOnlyList<string> Wrap(string value, int width)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [string.Empty];
        }

        var lines = new List<string>();
        var line = new List<char>();
        int lineWidth = 0;

        foreach (char character in value)
        {
            int characterWidth = IsWide(character) ? 2 : 1;

            if (lineWidth + characterWidth > width && line.Count > 0)
            {
                lines.Add(new string(line.ToArray()).TrimEnd());
                line.Clear();
                lineWidth = 0;

                if (char.IsWhiteSpace(character))
                {
                    continue;
                }
            }

            line.Add(character);
            lineWidth += characterWidth;
        }

        if (line.Count > 0)
        {
            lines.Add(new string(line.ToArray()).TrimEnd());
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    public static string PadRight(string value, int totalWidth)
    {
        int padding = Math.Max(totalWidth - GetWidth(value), 0);

        return value + new string(' ', padding);
    }

    private static int GetWidth(string value)
    {
        return value.Sum(character => IsWide(character) ? 2 : 1);
    }

    private static bool IsWide(char character)
    {
        return character is
            >= '\u1100' and <= '\u115f' or
            >= '\u2e80' and <= '\ua4cf' or
            >= '\uac00' and <= '\ud7a3' or
            >= '\uf900' and <= '\ufaff' or
            >= '\ufe10' and <= '\ufe19' or
            >= '\ufe30' and <= '\ufe6f' or
            >= '\uff00' and <= '\uff60' or
            >= '\uffe0' and <= '\uffe6';
    }
}
