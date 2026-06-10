using Spectre.Console;
using UnityAssetsPatcher.Core.Assets;
using UnityAssetsPatcher.Tui.Pages;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalApp
{
    private readonly TerminalAppContext _context;
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
        IReadOnlyList<ITerminalPage> pages =
        [
            new InstallTerminalPage(_context),
            new InspectTerminalPage(_context),
            new FindTerminalPage(_context),
            new SettingsTerminalPage(_context),
        ];
        _mainMenuPage = new MainMenuTerminalPage(_context, pages);
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

                _context.Renderer.ShowReturnHint();
                _context.Prompts.WaitForKey();
            }
        }
        catch (Exception exception)
        {
            _context.ErrorRenderer.WriteError(exception.Message);

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
            _context.ErrorRenderer.WriteError(exception.Message);
            return TerminalPageResult.ReturnToMenu();
        }
    }
}
