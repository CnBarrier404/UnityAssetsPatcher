namespace UnityAssetsPatcher.TUI;

// TODO(tui-refactor): Remove this legacy result after ITerminalPage.Run() is no longer used.
public readonly record struct TerminalPageResult(bool WaitForKey)
{
    public static TerminalPageResult ReturnToMenu(bool waitForKey = true)
    {
        return new TerminalPageResult(waitForKey);
    }
}
