using Spectre.Console;

namespace UnityAssetsPatcher.TUI.Framework;

public sealed class TerminalLayout
{
    public const string ShortcutHint = "Shortcuts: ↑/↓ to choose | Esc to cancel | Ctrl + C to exit";
    private const string ApplicationTitle = "Unity Assets Patcher";
    private const int ApplicationTitleHorizontalPadding = 2;
    private const string ReturnToMainMenuPrompt = "Press any key to return to the main menu.";
    private const string SaveCursor = "\e[s";
    private const string RestoreCursor = "\e[u";

    private readonly IAnsiConsole _console;
    private readonly TerminalText _text;

    public TerminalLayout(IAnsiConsole console, TerminalText text)
    {
        _console = console;
        _text = text;
    }

    public void ShowPage(
        string title,
        string? description = null,
        string shortcutHint = ShortcutHint,
        bool clear = true)
    {
        WriteApplicationHeader(shortcutHint, clear);
        _console.MarkupLine($"[bold blue]{TerminalText.Escape(title)}[/]");

        if (!string.IsNullOrWhiteSpace(description))
        {
            _console.MarkupLine($"[grey]{TerminalText.Escape(description)}[/]");
        }

        _text.WriteBlankLine();
    }

    public void ShowReturnHint()
    {
        WriteBottomFooterHint(ReturnToMainMenuPrompt);
    }

    public void ShowShortcutHint()
    {
        WriteBottomFooterHint(ShortcutHint);
    }

    public void PrepareOutputArea()
    {
        _text.WriteBlankLine();
        ClearBottomFooterArea();
    }

    public void WriteBottomFooterHint(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        int height = _console.Profile.Height;

        if (height <= 0)
        {
            WriteFooterHint(message);
            return;
        }

        _console.Cursor.SetPosition(1, height);
        _console.Markup($"[grey]{TerminalText.Escape(FitToWidth(message, _console.Profile.Width))}[/]");
        ClearBottomFooterArea(clearFooterLine: false, preserveCursor: false);
    }

    public void ClearBottomFooterArea()
    {
        ClearBottomFooterArea(clearFooterLine: true, preserveCursor: true);
    }

    private void WriteFooterHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _text.WriteBlankLine();
        _text.WriteInfo(message);
    }

    private void ClearBottomFooterArea(bool clearFooterLine, bool preserveCursor)
    {
        int height = _console.Profile.Height;

        if (height <= 1)
        {
            return;
        }

        string clearLine = new(' ', Math.Max(_console.Profile.Width, 0));
        int contentLine = Math.Max(height - 2, 1);

        if (preserveCursor)
        {
            _console.Write(new Text(SaveCursor));
        }

        if (clearFooterLine)
        {
            _console.Cursor.SetPosition(1, height);
            _console.Write(new Text(clearLine));
        }

        _console.Cursor.SetPosition(1, height - 1);
        _console.Write(new Text(clearLine));
        _console.Cursor.SetPosition(1, contentLine);
        _console.Write(new Text(clearLine));
        _console.Cursor.SetPosition(1, contentLine);

        if (preserveCursor)
        {
            _console.Write(new Text(RestoreCursor));
        }
    }

    private void WriteApplicationHeader(string? footerHint = null, bool clear = true)
    {
        if (clear)
        {
            _console.Clear(home: true);
        }
        else
        {
            _console.Cursor.SetPosition(1, 1);
        }

        WriteBottomFooterHint(footerHint);
        _console.Cursor.SetPosition(1, 1);
        WriteApplicationTitle();
        _text.WriteBlankLine();
    }

    private void WriteApplicationTitle()
    {
        string title = $"{ApplicationTitle} ({BuildInfo.DisplayVersion})";
        int boxWidth = title.Length + (ApplicationTitleHorizontalPadding * 2) + 2;

        WriteTitleBoxLine("╭", "─", "╮", boxWidth);
        WriteTitleContentLine(title, greyStartIndex: ApplicationTitle.Length + 1);
        WriteTitleBoxLine("╰", "─", "╯", boxWidth);
    }

    private static string FitToWidth(string value, int width)
    {
        if (width <= 0)
        {
            return value;
        }

        return value.Length <= width
            ? value.PadRight(width)
            : value[..width];
    }

    private void WriteTitleBoxLine(
        string left,
        string horizontal,
        string right,
        int width)
    {
        _console.MarkupLine(
            $"[grey]{TerminalText.Escape(left + string.Concat(Enumerable.Repeat(horizontal, width - 2)) + right)}[/]");
    }

    private void WriteTitleContentLine(
        string content,
        int? greyStartIndex = null,
        int? boldStartIndex = null)
    {
        string paddedContent =
            $"{new string(' ', ApplicationTitleHorizontalPadding)}{content}{new string(' ', ApplicationTitleHorizontalPadding)}";

        _console.Markup("[grey]│[/]");

        if (greyStartIndex is null)
        {
            _console.Markup(TerminalText.Escape(paddedContent));
        }
        else
        {
            int greyIndex = Math.Clamp(greyStartIndex.Value + ApplicationTitleHorizontalPadding, 0,
                paddedContent.Length);
            string normal = paddedContent[..greyIndex];
            string grey = paddedContent[greyIndex..];

            _console.Markup(TerminalText.Escape(normal));

            if (boldStartIndex is null)
            {
                _console.Markup($"[grey]{TerminalText.Escape(grey)}[/]");
            }
            else
            {
                int boldIndex = Math.Clamp(boldStartIndex.Value, greyIndex, paddedContent.Length);
                string label = paddedContent[greyIndex..boldIndex];
                string value = paddedContent[boldIndex..];

                _console.Markup($"[grey]{TerminalText.Escape(label)}[/]");
                _console.Markup($"[bold]{TerminalText.Escape(value)}[/]");
            }
        }

        _console.MarkupLine("[grey]│[/]");
    }
}
