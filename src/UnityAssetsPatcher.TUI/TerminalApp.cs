using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly TerminalGUINavigator _terminalGuiNavigator;
    private readonly ILogger<TerminalApp> _logger;

    public TerminalApp(TerminalGUINavigator terminalGuiNavigator, ILogger<TerminalApp>? logger = null)
    {
        _terminalGuiNavigator = terminalGuiNavigator;
        _logger = logger ?? NullLogger<TerminalApp>.Instance;
    }

    public int Run()
    {
        try
        {
            return _terminalGuiNavigator.Run();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Terminal application terminated unexpectedly");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
