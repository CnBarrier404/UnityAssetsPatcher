using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher.Tui;

public sealed class TerminalApp
{
    private readonly TerminalAppContext _context;
    private readonly InteractivePrompts _prompts;
    private readonly InstallTerminalPage _installPage;
    private readonly InspectTerminalPage _inspectPage;
    private readonly FindTerminalPage _findPage;
    private readonly PatchTerminalPage _patchPage;

    public TerminalApp(IAssetsFileService assetsFileService, TextReader input, TextWriter output, TextWriter error)
        : this(assetsFileService, Path.Combine(AppContext.BaseDirectory, "backup"), input, output, error) { }

    public TerminalApp(
        IAssetsFileService assetsFileService,
        string backupDirectory,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        _context = new TerminalAppContext(
            CreateServiceScopeFactory(assetsFileService),
            backupDirectory,
            output,
            error);
        _prompts = new InteractivePrompts(input, output);
        _installPage = new InstallTerminalPage(_context, _prompts);
        _inspectPage = new InspectTerminalPage(_context, _prompts);
        _findPage = new FindTerminalPage(_context, _prompts);
        _patchPage = new PatchTerminalPage(_context, _prompts);
    }

    public int Run()
    {
        try
        {
            while (true)
            {
                WriteMainMenu();

                string? choice = _prompts.ReadRawLine();

                if (choice is null)
                {
                    return 0;
                }

                switch (choice.Trim().ToLowerInvariant())
                {
                    case "1":
                        RunMenuAction(() => _installPage.Run(apply: true));
                        break;
                    case "2":
                        RunMenuAction(() => _installPage.Run(apply: false));
                        break;
                    case "3":
                        RunMenuAction(_inspectPage.Run);
                        break;
                    case "4":
                        RunMenuAction(_findPage.Run);
                        break;
                    case "5":
                        RunMenuAction(_patchPage.Run);
                        break;
                    case "6":
                        return 0;
                    default:
                        _context.Output.WriteLine("Invalid option. Enter 1, 2, 3, 4, 5, or 6.");
                        break;
                }

                _context.Output.WriteLine();
            }
        }
        catch (Exception exception)
        {
            _context.Error.WriteLine(exception.Message);

            return 1;
        }
    }

    private void WriteMainMenu()
    {
        _context.Output.WriteLine("========================================");
        _context.Output.WriteLine("        Unity Assets Patcher");
        _context.Output.WriteLine("========================================");
        _context.Output.WriteLine();
        _context.Output.WriteLine("1. Install a mod");
        _context.Output.WriteLine("2. Preview a mod install");
        _context.Output.WriteLine("3. Inspect assets");
        _context.Output.WriteLine("4. Find assets");
        _context.Output.WriteLine("5. Patch assets");
        _context.Output.WriteLine("6. Exit");
        _context.Output.WriteLine();
        _context.Output.Write("Select an option: ");
    }

    private void RunMenuAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            _context.Error.WriteLine(exception.Message);
        }
    }

    private static Func<TerminalAssetsWorkflowServiceScope> CreateServiceScopeFactory(
        IAssetsFileService assetsFileService)
    {
        if (assetsFileService is not IAssetsReadScopeFactory readScopeFactory)
        {
            return () => new TerminalAssetsWorkflowServiceScope(new AssetsWorkflowService(assetsFileService), null);
        }

        return () =>
        {
            IAssetsReadScope readScope = readScopeFactory.CreateReadScope();
            var service = new AssetsWorkflowService(readScope, assetsFileService);

            return new TerminalAssetsWorkflowServiceScope(service, readScope);
        };
    }
}

internal sealed class TerminalAppContext
{
    private readonly Func<TerminalAssetsWorkflowServiceScope> _serviceScopeFactory;

    public TerminalAppContext(
        Func<TerminalAssetsWorkflowServiceScope> serviceScopeFactory,
        string backupDirectory,
        TextWriter output,
        TextWriter error)
    {
        _serviceScopeFactory = serviceScopeFactory;
        BackupDirectory = backupDirectory;
        Output = output;
        Error = error;
    }

    public string BackupDirectory { get; }
    public TextWriter Output { get; }
    public TextWriter Error { get; }

    public void UseService(Func<AssetsWorkflowService, int> action)
    {
        using TerminalAssetsWorkflowServiceScope scope = _serviceScopeFactory();

        action(scope.Service);
    }
}

internal sealed class TerminalAssetsWorkflowServiceScope : IDisposable
{
    private readonly IDisposable? _disposable;

    public TerminalAssetsWorkflowServiceScope(AssetsWorkflowService service, IDisposable? disposable)
    {
        Service = service;
        _disposable = disposable;
    }

    public AssetsWorkflowService Service { get; }

    public void Dispose()
    {
        _disposable?.Dispose();
    }
}
