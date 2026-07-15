using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly TerminalGUINavigator _terminalGuiNavigator;

    public TerminalApp(TerminalGUINavigator terminalGuiNavigator)
    {
        _terminalGuiNavigator = terminalGuiNavigator;
    }

    public int Run()
    {
        try
        {
            return _terminalGuiNavigator.Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
