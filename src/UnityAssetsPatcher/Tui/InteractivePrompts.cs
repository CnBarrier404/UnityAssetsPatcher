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

    internal int? ReadMainMenuChoice(
        IReadOnlyList<TerminalPage> choices,
        Action<int, bool> render,
        Action renderHeader)
    {
        if (!SupportsSelectionPrompt())
        {
            renderHeader();
            return ReadNumberedMainMenuChoice(choices);
        }

        int selectedIndex = 0;
        bool clear = true;

        _console.Cursor.Show(false);

        while (true)
        {
            render(selectedIndex, clear);
            clear = false;

            var maybeKey = _console.Input.ReadKey(intercept: true);

            if (maybeKey is null)
            {
                return null;
            }

            ConsoleKeyInfo key = maybeKey.Value;

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    return selectedIndex;
                case ConsoleKey.Escape:
                    return null;
                case ConsoleKey.UpArrow:
                    selectedIndex = selectedIndex == 0 ? choices.Count - 1 : selectedIndex - 1;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = selectedIndex == choices.Count - 1 ? 0 : selectedIndex + 1;
                    break;
            }
        }
    }

    public string ReadSubMenuChoice(string title, IReadOnlyList<string> choices, string cancelChoice)
    {
        if (!SupportsSelectionPrompt())
        {
            return ReadNumberedMenuChoice(title, choices, cancelChoice);
        }

        var prompt = CreateSelectionPrompt(title, choices)
            .AddCancelResult(cancelChoice);

        return _console.Prompt(prompt);
    }

    private static SelectionPrompt<string> CreateSelectionPrompt(string title, IReadOnlyList<string> choices)
    {
        var prompt = new SelectionPrompt<string>()
            .PageSize(Math.Max(choices.Count, 3))
            .MoreChoicesText("[grey](Move up and down to reveal more choices.)[/]")
            .HighlightStyle(new Style(Color.CornflowerBlue))
            .DisableSearch()
            .AddChoices(choices);

        if (!string.IsNullOrWhiteSpace(title))
        {
            prompt.Title($"[blue]{Markup.Escape(title)}[/]");
        }

        return prompt;
    }

    private string ReadNumberedMenuChoice(
        string title,
        IReadOnlyList<string> choices,
        string? cancelChoice = null)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            WriteLine(title);
        }

        for (int i = 0; i < choices.Count; i++)
        {
            WriteLine($"{i + 1}. {choices[i]}");
        }

        while (true)
        {
            string? rawInput = ReadText($"Select an option [1-{choices.Count}]");

            if (rawInput is null)
            {
                if (cancelChoice is not null)
                {
                    return cancelChoice;
                }

                WriteError($"Invalid option. Choose a number from 1 to {choices.Count}.");
                continue;
            }

            string input = NormalizePathInput(rawInput);

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
                index >= 1 &&
                index <= choices.Count)
            {
                return choices[index - 1];
            }

            WriteError($"Invalid option. Choose a number from 1 to {choices.Count}.");
        }
    }

    private int? ReadNumberedMainMenuChoice(IReadOnlyList<TerminalPage> choices)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            TerminalPage page = choices[i];
            WriteLine($"{i + 1}. {page.Title}");
            WriteLine($"   {page.Description}");
            WriteLine(string.Empty);
        }

        while (true)
        {
            string? rawInput = ReadText($"Select an option [1-{choices.Count}]");

            if (rawInput is null)
            {
                return null;
            }

            string input = NormalizePathInput(rawInput);

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
                index >= 1 &&
                index <= choices.Count)
            {
                return index - 1;
            }

            WriteError($"Invalid option. Choose a number from 1 to {choices.Count}.");
        }
    }

    private bool SupportsSelectionPrompt()
    {
        bool? supportsAnsi = GetBooleanCapability("Ansi");

        if (supportsAnsi is not null)
        {
            return supportsAnsi.Value;
        }

        bool? isLegacy = GetBooleanCapability("Legacy");

        return isLegacy is null || !isLegacy.Value;
    }

    private bool? GetBooleanCapability(string name)
    {
        object capabilities = _console.Profile.Capabilities;

        return capabilities.GetType().GetProperty(name)?.GetValue(capabilities) as bool?;
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

    public bool TryReadOptionalPath(string label, out string? path)
    {
        string? input = ReadText(label, allowEmpty: true);

        if (input is null)
        {
            path = null;

            return false;
        }

        string normalized = NormalizePathInput(input);

        path = string.IsNullOrWhiteSpace(normalized) ? null : normalized;

        return true;
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

            if (key.Key is ConsoleKey.Enter)
            {
                _console.Write(new Text(Environment.NewLine));

                return builder.ToString();
            }

            if (key.Key is ConsoleKey.Escape)
            {
                _console.Write(new Text(Environment.NewLine));

                return null;
            }

            if (key.Key is ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                    _console.Write(new Text("\b \b"));
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
                _console.Write(new Text(key.KeyChar.ToString()));
            }
        }
    }

    private void WriteInputLabel(string label)
    {
        if (SupportsSelectionPrompt())
        {
            _console.Markup($"[blue]{Markup.Escape(label)}[/]: ");
            return;
        }

        _console.Write(new Text($"{label}: "));
    }

    private void WriteConfirmationLabel(string prompt)
    {
        if (SupportsSelectionPrompt())
        {
            _console.Markup($"[blue]{Markup.Escape(prompt)}[/] [grey]y/N[/]: ");
            return;
        }

        _console.Write(new Text($"{prompt} y/N: "));
    }

    private void WriteError(string message)
    {
        if (SupportsSelectionPrompt())
        {
            _console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
            return;
        }

        WriteLine(message);
    }

    private void WriteLine(string message)
    {
        _console.Write(new Text(message + Environment.NewLine));
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
