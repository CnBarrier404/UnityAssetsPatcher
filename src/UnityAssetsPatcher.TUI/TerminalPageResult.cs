namespace UnityAssetsPatcher.TUI;

public readonly record struct TerminalPageResult(bool WaitForKey)
{
    public static TerminalPageResult ReturnToMenu(bool waitForKey = true)
    {
        return new TerminalPageResult(waitForKey);
    }
}
