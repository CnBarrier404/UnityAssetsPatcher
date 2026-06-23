using System.Globalization;
using System.Text;
using Spectre.Console;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Localization;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalPrompts
{
    private readonly IAnsiConsole _console;
    private readonly TerminalText _text;
    private readonly TerminalSelectionPrompt _selectionPrompt;

    public TerminalPrompts(IAnsiConsole console)
        : this(console, new TerminalUI(console).Text) { }

    public TerminalPrompts(IAnsiConsole console, TerminalText text)
    {
        _console = console;
        _text = text;
        _selectionPrompt = new TerminalSelectionPrompt(console);
    }

    public string? ReadExistingFilePath(string label)
    {
        return ReadExistingPath(label, File.Exists, value => Format(LocalizedStrings.Prompt_FileNotFoundFormat, value));
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

    public IReadOnlyList<string> ReadMultiChoice(
        IReadOnlyList<string> choices,
        string title,
        int pageSize = 10)
    {
        var prompt = new MultiSelectionPrompt<string>()
            .Title($"[{TerminalTheme.Label}]{TerminalText.Escape(title)}[/]")
            .InstructionsText(
                $"[{TerminalTheme.Muted}]{TerminalText.Escape(LocalizedStrings.Prompt_MultiSelectionInstructions)}[/]")
            .NotRequired()
            .PageSize(pageSize);

        foreach (string choice in choices)
        {
            prompt.AddChoice(choice);
        }

        return _console.Prompt(prompt);
    }

    public string? ReadExistingDirectoryPath(string label)
    {
        return ReadExistingPath(label, Directory.Exists,
            value => Format(LocalizedStrings.Prompt_DirectoryNotFoundFormat, value));
    }

    public bool Confirm(string prompt)
    {
        return ConfirmOrCancel(prompt) == ConfirmChoice.Yes;
    }

    private ConfirmChoice ConfirmOrCancel(string prompt)
    {
        _console.Cursor.Show(false);
        _text.WriteConfirmationLabel(prompt);

        while (true)
        {
            var maybeKey = _console.Input.ReadKey(intercept: true);

            if (maybeKey is null)
            {
                return ConfirmChoice.Canceled;
            }

            ConsoleKeyInfo key = maybeKey.Value;

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    _console.Write(new Text(Environment.NewLine));

                    return ConfirmChoice.Canceled;
                case ConsoleKey.Enter:
                    _console.Write(new Text(Environment.NewLine));

                    return ConfirmChoice.No;
            }

            char choice = char.ToLowerInvariant(key.KeyChar);

            switch (choice)
            {
                case 'y':
                    _console.Write(new Text($"y{Environment.NewLine}"));

                    return ConfirmChoice.Yes;
                case 'n':
                    _console.Write(new Text($"n{Environment.NewLine}"));

                    return ConfirmChoice.No;
            }
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

            _text.WriteError(Format(LocalizedStrings.Prompt_LabelMustBeIntegerFormat, label));
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

            _text.WriteError(Format(LocalizedStrings.Prompt_LabelMustBeGreaterThanZeroFormat, label));
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
                _text.WriteError(Format(LocalizedStrings.Prompt_LabelRequiredFormat, label));

                continue;
            }

            if (exists(path))
            {
                return path;
            }

            _text.WriteError(missingMessage(path));
        }
    }

    private string? ReadText(string label, bool allowEmpty = false)
    {
        while (true)
        {
            _text.WriteInputLabel(label);
            string? value = ReadCancelableLine();

            if (value is null)
            {
                return null;
            }

            if (allowEmpty || value.Length > 0)
            {
                return value;
            }

            _text.WriteError(Format(LocalizedStrings.Prompt_LabelRequiredFormat, label));
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

    private static string Format(string format, params object[] args)
    {
        return string.Format(CultureInfo.CurrentUICulture, format, args);
    }
}
