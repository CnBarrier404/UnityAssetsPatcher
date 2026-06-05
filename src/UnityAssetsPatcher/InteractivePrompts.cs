namespace UnityAssetsPatcher;

public sealed class InteractivePrompts
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public InteractivePrompts(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    public string? ReadRawLine()
    {
        return _input.ReadLine();
    }

    public string? ReadExistingFilePath(string label)
    {
        return ReadExistingPath(label, File.Exists, value => $"File not found: {value}");
    }

    public string? ReadExistingDirectoryPath(string label)
    {
        return ReadExistingPath(label, Directory.Exists, value => $"Directory not found: {value}");
    }

    public bool Confirm(string prompt)
    {
        _output.Write($"{prompt} ");
        string? value = _input.ReadLine();

        return string.Equals(value?.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    private string? ReadExistingPath(string label, Func<string, bool> exists, Func<string, string> missingMessage)
    {
        while (true)
        {
            _output.Write($"{label}: ");
            string? value = _input.ReadLine();

            if (value is null)
            {
                return null;
            }

            string path = NormalizePathInput(value);

            if (IsQuit(path))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                _output.WriteLine($"{label} is required.");
                continue;
            }

            if (!exists(path))
            {
                _output.WriteLine(missingMessage(path));
                continue;
            }

            return path;
        }
    }

    private static bool IsQuit(string value)
    {
        return string.Equals(value, "q", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "quit", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "exit", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathInput(string value)
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
