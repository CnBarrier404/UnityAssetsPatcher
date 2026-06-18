using Spectre.Console;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Core;
using UnityAssetsPatcher.TUI.Pages;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly TerminalAppContext _context;
    private readonly MainMenuTerminalPage _mainMenuPage;

    public TerminalApp(IWorkflowService workflowService,
        string backupDirectory,
        AppInfo appInfo,
        IAnsiConsole console,
        IAnsiConsole errorConsole)
    {
        ArgumentNullException.ThrowIfNull(workflowService);
        ArgumentNullException.ThrowIfNull(backupDirectory);
        ArgumentNullException.ThrowIfNull(appInfo);
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(errorConsole);

        _context = new TerminalAppContext(workflowService, backupDirectory, appInfo, console, errorConsole);

        IReadOnlyList<ITerminalPage> pages =
        [
            new InstallTerminalPage(_context),
            new UninstallTerminalPage(_context),
            new SettingsTerminalPage(_context),
        ];

        _mainMenuPage = new MainMenuTerminalPage(_context, pages);
    }

    public int Run()
    {
        try
        {
            while (true)
            {
                ITerminalPage? page = _mainMenuPage.ReadSelection();

                if (page is null)
                {
                    return 0;
                }

                TerminalPageResult result = RunMenuAction(page.Run);

                if (result.Action == TerminalPageAction.Exit)
                {
                    return 0;
                }

                if (!result.WaitForKey)
                {
                    continue;
                }

                _context.Ui.Layout.ShowReturnHint();
                _context.Prompts.WaitForKey();
            }
        }
        catch (Exception exception)
        {
            _context.ErrorUi.Text.WriteError(exception.Message);

            return 1;
        }
        finally
        {
            _context.Console.Cursor.Show(true);
        }
    }

    private TerminalPageResult RunMenuAction(Func<TerminalPageResult> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            _context.ErrorUi.Text.WriteError(exception.Message);
            return TerminalPageResult.ReturnToMenu();
        }
    }
}
