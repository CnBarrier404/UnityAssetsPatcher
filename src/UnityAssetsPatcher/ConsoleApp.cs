using System.CommandLine;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Cli;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher;

public sealed class ConsoleApp
{
    private readonly RootCommand _rootCommand;
    private readonly CommandContext _context;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ConsoleApp(IAssetsFileService assetsFileService, TextWriter output, TextWriter error)
        : this(assetsFileService, Path.Combine(AppContext.BaseDirectory, "backup"), TextReader.Null, output, error) { }

    public ConsoleApp(IAssetsFileService assetsFileService, TextReader input, TextWriter output, TextWriter error)
        : this(assetsFileService, Path.Combine(AppContext.BaseDirectory, "backup"), input, output, error) { }

    public ConsoleApp(IAssetsFileService assetsFileService, string backupDirectory, TextWriter output, TextWriter error)
        : this(assetsFileService, backupDirectory, TextReader.Null, output, error) { }

    public ConsoleApp(
        IAssetsFileService assetsFileService,
        string backupDirectory,
        TextReader input,
        TextWriter output,
        TextWriter error)
    {
        _input = input;
        _output = output;
        _error = error;

        _context = new CommandContext(
            CreateServiceScopeFactory(assetsFileService),
            backupDirectory,
            output,
            error);
        _rootCommand = new CommandCatalog().BuildRootCommand(_context);
    }

    public int Run(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                return new InteractiveConsoleApp(_context, _input).Run();
            }

            return _rootCommand.Parse(args).Invoke(new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false,
                Output = _output,
                Error = _error
            });
        }
        catch (Exception exception)
        {
            _error.WriteLine(exception.Message);

            return 1;
        }
    }

    private static Func<AssetsWorkflowServiceScope> CreateServiceScopeFactory(IAssetsFileService assetsFileService)
    {
        if (assetsFileService is not IAssetsReadScopeFactory readScopeFactory)
        {
            return () => new AssetsWorkflowServiceScope(new AssetsWorkflowService(assetsFileService), null);
        }

        return () =>
        {
            IAssetsReadScope readScope = readScopeFactory.CreateReadScope();
            var service = new AssetsWorkflowService(readScope, assetsFileService);

            return new AssetsWorkflowServiceScope(service, readScope);
        };
    }
}
