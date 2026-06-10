using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalRenderer
{
    public const string ShortcutHint = "Shortcuts: ↑/↓ to choose | Esc to cancel | Ctrl + C to exit";

    private const string ReturnToMainMenuPrompt = "Press any key to return to the main menu.";

    private readonly IAnsiConsole _console;

    public TerminalRenderer(IAnsiConsole console)
    {
        _console = console;
    }

    public void ShowPage(
        string title,
        string? description = null,
        string shortcutHint = ShortcutHint,
        bool clear = true)
    {
        TerminalOutputFormatter.WritePageHeader(_console, title, description, shortcutHint, clear);
    }

    public void ShowReturnHint()
    {
        TerminalOutputFormatter.WriteBottomFooterHint(_console, ReturnToMainMenuPrompt);
    }

    public void ShowShortcutHint()
    {
        TerminalOutputFormatter.WriteBottomFooterHint(_console, ShortcutHint);
    }

    public void PrepareOutputArea()
    {
        WriteBlankLine();
        TerminalOutputFormatter.ClearBottomFooterArea(_console);
    }

    public void WriteBlankLine()
    {
        TerminalOutputFormatter.WriteBlankLine(_console);
    }

    public void WriteInfo(string message)
    {
        TerminalOutputFormatter.WriteInfo(_console, message);
    }

    public void WriteInputLabel(string label)
    {
        TerminalOutputFormatter.WriteInputLabel(_console, label);
    }

    public void WriteConfirmationLabel(string prompt)
    {
        TerminalOutputFormatter.WriteConfirmationLabel(_console, prompt);
    }

    public void WriteError(string message)
    {
        TerminalOutputFormatter.WriteError(_console, message);
    }

    public void WriteAssetSummary(IReadOnlyList<AssetsInfo> assets, int? limit)
    {
        TerminalOutputFormatter.WriteAssetSummary(_console, assets, limit);
    }

    public void WriteAssetFields(AssetsFieldInfo fieldTree)
    {
        TerminalOutputFormatter.WriteAssetFields(_console, fieldTree);
    }

    public void WriteFindResults(IReadOnlyList<AssetMatch> matches)
    {
        TerminalOutputFormatter.WriteFindResults(_console, matches);
    }

    public void WriteInstallPreview(InstallPreviewResult preview, TerminalSettings settings)
    {
        TerminalOutputFormatter.WriteInstallPreview(_console, preview, settings);
    }

    public void WriteInstallResult(InstallModResult result, TerminalSettings settings)
    {
        TerminalOutputFormatter.WriteInstallResult(_console, result, settings);
    }

    internal void WriteMainMenu(IReadOnlyList<ITerminalPage> pages, int selectedIndex)
    {
        TerminalOutputFormatter.WriteMainMenu(_console, pages, selectedIndex);
    }

    internal void WriteSettings(IReadOnlyList<TerminalSettingDisplay> settings, int selectedIndex)
    {
        TerminalOutputFormatter.WriteSettings(_console, settings, selectedIndex);
    }
}
