using Spectre.Console;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tui.Pages;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalApp
{
    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;
    private readonly MainMenuTerminalPage _mainMenuPage;

    public TerminalApp(
        IAssetsFileReader assetsReader,
        IAssetsFileWriter assetsPatchWriter,
        IAnsiConsole console)
        : this(
            () => assetsReader,
            assetsPatchWriter,
            Path.Combine(AppContext.BaseDirectory, "backup"),
            console) { }

    public TerminalApp(
        IAssetsFileReader assetsReader,
        IAssetsFileWriter assetsPatchWriter,
        string backupDirectory,
        IAnsiConsole console)
        : this(() => assetsReader, assetsPatchWriter, backupDirectory, console, console) { }

    public TerminalApp(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter,
        IAnsiConsole console)
        : this(
            createAssetsReader,
            assetsPatchWriter,
            Path.Combine(AppContext.BaseDirectory, "backup"),
            console) { }

    public TerminalApp(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter,
        string backupDirectory,
        IAnsiConsole console,
        IAnsiConsole error)
    {
        _context = new TerminalAppContext(
            new TerminalWorkflowSessionFactory(createAssetsReader, assetsPatchWriter),
            backupDirectory,
            console,
            error);
        _prompts = new InteractivePrompts(console);
        IReadOnlyList<TerminalPage> pages =
        [
            new InstallTerminalPage(_context, _prompts),
            new SettingsTerminalPage(_context),
        ];
        _mainMenuPage = new MainMenuTerminalPage(_context, _prompts, pages);
    }

    private TerminalApp(
        Func<IAssetsFileReader> createAssetsReader,
        IAssetsFileWriter assetsPatchWriter,
        string backupDirectory,
        IAnsiConsole console)
        : this(createAssetsReader, assetsPatchWriter, backupDirectory, console, console) { }

    public int Run()
    {
        try
        {
            while (true)
            {
                TerminalPage? page = _mainMenuPage.ReadSelection();

                if (page is null)
                {
                    return 0;
                }

                bool waitBeforeReturningToMenu = RunMenuAction(page.Run);

                if (!waitBeforeReturningToMenu)
                {
                    continue;
                }

                _context.Layout.ShowReturnHint();
                _prompts.WaitForKey();
            }
        }
        catch (Exception exception)
        {
            TerminalOutputFormatter.WriteError(_context.Error, exception.Message);

            return 1;
        }
        finally
        {
            _context.Console.Cursor.Show(true);
        }
    }

    private bool RunMenuAction(Func<bool> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception)
        {
            TerminalOutputFormatter.WriteError(_context.Error, exception.Message);
            return true;
        }
    }
}
