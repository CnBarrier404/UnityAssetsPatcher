namespace UnityAssetsPatcher.TUI.Framework;

public static class TerminalPathNormalizer
{
    public static string Normalize(string value)
    {
        string path = value.Trim();

        while (path.Length >= 2 &&
               ((path[0] == '"' && path[^1] == '"') ||
                (path[0] == '\'' && path[^1] == '\'')))
        {
            path = path[1..^1].Trim();
        }

        return path;
    }
}
