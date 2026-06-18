using Spectre.Console;
using UnityAssetsPatcher.TUI.Framework;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly IAnsiConsole _console;
    private readonly TerminalUI _ui;
    private readonly TerminalNavigator _navigator;

    internal TerminalApp(
        IAnsiConsole console,
        TerminalUI ui,
        TerminalNavigator navigator)
    {
        _console = console;
        _ui = ui;
        _navigator = navigator;
    }

    public int Run()
    {
        try
        {
            return _navigator.Run();
        }
        catch (Exception exception)
        {
            _ui.Text.WriteError(exception.Message);

            return 1;
        }
        finally
        {
            _console.Cursor.Show(true);
        }
    }
}
