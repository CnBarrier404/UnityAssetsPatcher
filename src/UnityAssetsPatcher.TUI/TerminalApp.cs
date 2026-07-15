using Spectre.Console;
using UnityAssetsPatcher.TUI.Framework;
using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly IAnsiConsole _console;
    private readonly TerminalUI _ui;
    private readonly TerminalGUINavigator _terminalGuiNavigator;

    public TerminalApp(
        IAnsiConsole console,
        TerminalUI ui,
        TerminalGUINavigator terminalGuiNavigator)
    {
        _console = console;
        _ui = ui;
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
            _ui.Text.WriteError(exception.Message);

            // TODO: a temp solution, needs refactor
            _ui.Text.WriteBlankLine();
            _console.Input.ReadKey(intercept: true);

            return 1;
        }
        finally
        {
            _console.Cursor.Show(true);
        }
    }
}
