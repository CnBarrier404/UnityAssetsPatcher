namespace UnityAssetsPatcher.TUI.Pages;

internal sealed class MainMenuTerminalInput
{
    private readonly TerminalPrompts _prompts;

    public MainMenuTerminalInput(TerminalPrompts prompts)
    {
        _prompts = prompts;
    }

    public int? ReadSelection(
        int pageCount,
        int selectedIndex,
        Action<int, bool> render)
    {
        return _prompts.ReadChoiceIndex(pageCount, selectedIndex, render);
    }
}
