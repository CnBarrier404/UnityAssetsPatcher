using System.Globalization;
using System.Text;
using Spectre.Console;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalPrompts
{
    private readonly IAnsiConsole _console;
    private readonly TerminalRenderer _renderer;
    private readonly TerminalSelectionPrompt _selectionPrompt;

    public TerminalPrompts(IAnsiConsole console)
        : this(console, new TerminalRenderer(console)) { }

    public TerminalPrompts(IAnsiConsole console, TerminalRenderer renderer)
    {
        _console = console;
        _renderer = renderer;
        _selectionPrompt = new TerminalSelectionPrompt(console);
    }

    public string? ReadExistingFilePath(string label)
    {
        return ReadExistingPath(label, File.Exists, value => $"File not found: {value}");
    }

    public string ReadChoice(
        IReadOnlyList<string> choices,
        string cancelChoice,
        Action<int, bool> render,
        int initialSelectedIndex = 0,
        ConsoleKey acceptKey = ConsoleKey.Enter)
    {
        int? selectedIndex = ReadChoiceIndex(
            choices.Count,
            initialSelectedIndex,
            render,
            acceptKey);

        return selectedIndex is null ? cancelChoice : choices[selectedIndex.Value];
    }

    public int? ReadChoiceIndex(
        int optionCount,
        int initialSelectedIndex,
        Action<int, bool> render,
        ConsoleKey acceptKey = ConsoleKey.Enter)
    {
        return _selectionPrompt.ReadSelection(optionCount, initialSelectedIndex, render, acceptKey);
    }

    public string? ReadExistingDirectoryPath(string label)
    {
        return ReadExistingPath(label, Directory.Exists, value => $"Directory not found: {value}");
    }

    public bool Confirm(string prompt)
    {
        while (true)
        {
            _renderer.WriteConfirmationLabel(prompt);
            string? input = ReadCancelableLine();

            if (input is null)
            {
                return false;
            }

            string normalized = input.Trim().ToLowerInvariant();

            if (normalized.Length == 0 ||
                normalized is "n" or "no")
            {
                return false;
            }

            if (normalized is "y" or "yes")
            {
                return true;
            }

            _renderer.WriteError("Choose y or n.");
        }
    }

    public void WaitForKey()
    {
        _console.Cursor.Show(false);
        _console.Input.ReadKey(intercept: true);
    }

    public bool TryReadInt64(string label, out long value)
    {
        while (true)
        {
            string? input = ReadText(label);

            if (input is null)
            {
                value = 0;

                return false;
            }

            string normalized = NormalizePathInput(input);

            if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            _renderer.WriteError($"{label} must be an integer.");
        }
    }

    public bool TryReadPositiveInt(string label, out int value)
    {
        while (true)
        {
            string? input = ReadText(label);

            if (input is null)
            {
                value = 0;

                return false;
            }

            string normalized = NormalizePathInput(input);

            if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) &&
                value > 0)
            {
                return true;
            }

            _renderer.WriteError($"{label} must be greater than 0.");
        }
    }

    private string? ReadExistingPath(string label, Func<string, bool> exists, Func<string, string> missingMessage)
    {
        while (true)
        {
            string? input = ReadText(label);

            if (input is null)
            {
                return null;
            }

            string path = NormalizePathInput(input);

            if (string.IsNullOrWhiteSpace(path))
            {
                _renderer.WriteError($"{label} is required.");

                continue;
            }

            if (exists(path))
            {
                return path;
            }

            _renderer.WriteError(missingMessage(path));
        }
    }

    private string? ReadText(string label, bool allowEmpty = false)
    {
        while (true)
        {
            _renderer.WriteInputLabel(label);
            string? value = ReadCancelableLine();

            if (value is null)
            {
                return null;
            }

            if (allowEmpty || value.Length > 0)
            {
                return value;
            }

            _renderer.WriteError($"{label} is required.");
        }
    }

    private string? ReadCancelableLine()
    {
        var builder = new StringBuilder();

        _console.Cursor.Show(true);

        while (true)
        {
            var maybeKey = _console.Input.ReadKey(intercept: true);

            if (maybeKey is null)
            {
                return null;
            }

            ConsoleKeyInfo key = maybeKey.Value;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    _console.Write(new Text(Environment.NewLine));

                    return builder.ToString();
                case ConsoleKey.Escape:
                    _console.Write(new Text(Environment.NewLine));

                    return null;
                case ConsoleKey.Backspace:
                {
                    if (builder.Length > 0)
                    {
                        builder.Length--;
                        _console.Write(new Text("\b \b"));
                    }

                    continue;
                }
            }

            if (char.IsControl(key.KeyChar))
            {
                continue;
            }

            builder.Append(key.KeyChar);
            _console.Write(new Text(key.KeyChar.ToString()));
        }
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
