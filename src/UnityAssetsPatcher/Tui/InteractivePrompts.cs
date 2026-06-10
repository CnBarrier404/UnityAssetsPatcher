using System.Globalization;
using System.Text;
using Spectre.Console;

namespace UnityAssetsPatcher.Tui;

public sealed class InteractivePrompts
{
    private readonly IAnsiConsole _console;

    public InteractivePrompts(IAnsiConsole console)
    {
        _console = console;
    }

    public string? ReadExistingFilePath(string label)
    {
        return ReadExistingPath(label, File.Exists, value => $"File not found: {value}");
    }

    public string ReadSubMenuChoice(string title, IReadOnlyList<string> choices, string cancelChoice)
    {
        var prompt = new SelectionPrompt<string>()
            .PageSize(Math.Max(choices.Count, 3))
            .MoreChoicesText("[grey](Move up and down to reveal more choices.)[/]")
            .HighlightStyle(new Style(Color.CornflowerBlue))
            .DisableSearch()
            .AddChoices(choices)
            .AddCancelResult(cancelChoice);

        if (!string.IsNullOrWhiteSpace(title))
        {
            prompt.Title($"[blue]{Markup.Escape(title)}[/]");
        }

        return _console.Prompt(prompt);
    }

    public string? ReadExistingDirectoryPath(string label)
    {
        return ReadExistingPath(label, Directory.Exists, value => $"Directory not found: {value}");
    }

    public bool Confirm(string prompt)
    {
        while (true)
        {
            WriteConfirmationLabel(prompt);
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

            WriteError("Choose y or n.");
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

            WriteError($"{label} must be an integer.");
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

            WriteError($"{label} must be greater than 0.");
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
                WriteError($"{label} is required.");

                continue;
            }

            if (exists(path))
            {
                return path;
            }

            WriteError(missingMessage(path));
        }
    }

    private string? ReadText(string label, bool allowEmpty = false)
    {
        while (true)
        {
            WriteInputLabel(label);
            string? value = ReadCancelableLine();

            if (value is null)
            {
                return null;
            }

            if (allowEmpty || value.Length > 0)
            {
                return value;
            }

            WriteError($"{label} is required.");
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

    private void WriteInputLabel(string label)
    {
        _console.Markup($"[blue]{Markup.Escape(label)}[/]: ");
    }

    private void WriteConfirmationLabel(string prompt)
    {
        _console.Markup($"[blue]{Markup.Escape(prompt)}[/] [grey]y/N[/]: ");
    }

    private void WriteError(string message)
    {
        _console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
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
