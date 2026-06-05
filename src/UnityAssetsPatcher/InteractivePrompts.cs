using System.Globalization;

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

    public bool TryReadInt64(string label, out long value)
    {
        while (true)
        {
            _output.Write($"{label}: ");
            string? input = _input.ReadLine();

            if (input is null)
            {
                value = 0;

                return false;
            }

            string normalized = NormalizePathInput(input);

            if (IsQuit(normalized))
            {
                value = 0;

                return false;
            }

            if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            _output.WriteLine($"{label} must be an integer.");
        }
    }

    public bool TryReadPositiveInt(string label, out int value)
    {
        while (true)
        {
            _output.Write($"{label}: ");
            string? input = _input.ReadLine();

            if (input is null)
            {
                value = 0;

                return false;
            }

            string normalized = NormalizePathInput(input);

            if (IsQuit(normalized))
            {
                value = 0;

                return false;
            }

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                value > 0)
            {
                return true;
            }

            _output.WriteLine($"{label} must be greater than 0.");
        }
    }

    public bool TryReadOptionalPath(string label, out string? path)
    {
        _output.Write($"{label}: ");
        string? value = _input.ReadLine();

        if (value is null)
        {
            path = null;

            return false;
        }

        string normalized = NormalizePathInput(value);

        if (IsQuit(normalized))
        {
            path = null;

            return false;
        }

        path = string.IsNullOrWhiteSpace(normalized) ? null : normalized;

        return true;
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

            if (exists(path))
            {
                return path;
            }

            _output.WriteLine(missingMessage(path));
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
