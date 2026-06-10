namespace UnityAssetsPatcher.Tui;

public enum TerminalPageAction
{
    ReturnToMenu,
    Exit,
}

public readonly record struct TerminalPageResult(TerminalPageAction Action, bool WaitForKey)
{
    public static TerminalPageResult ReturnToMenu(bool waitForKey = true)
    {
        return new TerminalPageResult(TerminalPageAction.ReturnToMenu, waitForKey);
    }

    public static TerminalPageResult Exit()
    {
        return new TerminalPageResult(TerminalPageAction.Exit, false);
    }
}
