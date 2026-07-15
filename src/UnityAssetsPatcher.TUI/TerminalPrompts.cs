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
            .PageSize(pageSize)
            .HighlightStyle(Style.Parse(TerminalTheme.Selection));

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

    public long? ReadInt64(string label)
    {
        while (true)
        {
            string? input = ReadText(label);

            if (input is null)
            {
                return null;
            }

            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                return value;
            }

            _text.WriteError(Format(LocalizedStrings.Prompt_InvalidIntegerFormat, label));
        }
    }

    public int? ReadPositiveInt(string label)
    {
        while (true)
        {
            string? input = ReadText(label);

            if (input is null)
            {
                return null;
            }

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0)
            {
                return value;
            }

            _text.WriteError(Format(LocalizedStrings.Prompt_InvalidPositiveIntegerFormat, label));
        }
    }

    public bool Confirm(string prompt)
    {
        _console.Cursor.Show(false);
        _text.WriteConfirmationLabel(prompt);

        while (true)
        {
            var maybeKey = _console.Input.ReadKey(intercept: true);

            if (maybeKey is null)
            {
                return false;
            }

            ConsoleKeyInfo key = maybeKey.Value;

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    _console.Write(new Text(Environment.NewLine));

                    return false;
                case ConsoleKey.Enter:
                    _console.Write(new Text(Environment.NewLine));

                    return false;
            }

            char choice = char.ToLowerInvariant(key.KeyChar);

            switch (choice)
            {
                case 'y':
                    _console.Write(new Text($"y{Environment.NewLine}"));

                    return true;
                case 'n':
                    _console.Write(new Text($"n{Environment.NewLine}"));

                    return false;
            }
        }
    }

    public void WaitForKey()
    {
        _console.Cursor.Show(false);
        _console.Input.ReadKey(intercept: true);
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

            string path = TerminalPathNormalizer.Normalize(input);

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

    private string? ReadText(string label)
    {
        while (true)
        {
            _text.WriteInputLabel(label);
            string? value = ReadCancelableLine();

            if (value is null)
            {
                return null;
            }

            if (value.Length > 0)
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

    private static string Format(string format, params object[] args)
    {
        return string.Format(CultureInfo.CurrentUICulture, format, args);
    }
}
