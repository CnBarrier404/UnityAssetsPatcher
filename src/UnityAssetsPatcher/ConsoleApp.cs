using System.CommandLine;
using UnityAssetsPatcher.Application.Workflows;
using UnityAssetsPatcher.Cli;
using UnityAssetsPatcher.Core.Assets;

namespace UnityAssetsPatcher;

public sealed class ConsoleApp
{
    private readonly RootCommand _rootCommand;
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public ConsoleApp(IAssetsFileService assetsFileService, TextWriter output, TextWriter error)
        : this(assetsFileService, Path.Combine(AppContext.BaseDirectory, "backup"), output, error) { }

    public ConsoleApp(IAssetsFileService assetsFileService, string backupDirectory, TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;

        var context = new CommandContext(
            CreateServiceScopeFactory(assetsFileService),
            backupDirectory,
            output,
            error);
        _rootCommand = new CommandCatalog().BuildRootCommand(context);
    }

    public int Run(string[] args)
    {
        try
        {
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
