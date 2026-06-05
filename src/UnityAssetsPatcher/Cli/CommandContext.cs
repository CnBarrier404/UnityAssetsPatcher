using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Cli;

public sealed class CommandContext
{
    public CommandContext(AssetsWorkflowService service, string backupDirectory,
        TextWriter output, TextWriter error)
        : this(() => new AssetsWorkflowServiceScope(service, null), backupDirectory, output, error) { }

    public CommandContext(Func<AssetsWorkflowServiceScope> serviceScopeFactory,
        string backupDirectory,
        TextWriter output, TextWriter error)
    {
        _serviceScopeFactory = serviceScopeFactory;
        BackupDirectory = backupDirectory;
        Output = output;
        Error = error;
    }

    private readonly Func<AssetsWorkflowServiceScope> _serviceScopeFactory;

    public string BackupDirectory { get; }
    public TextWriter Output { get; }
    public TextWriter Error { get; }

    public int UseService(Func<AssetsWorkflowService, int> action)
    {
        using AssetsWorkflowServiceScope scope = _serviceScopeFactory();

        return action(scope.Service);
    }
}

public sealed class AssetsWorkflowServiceScope : IDisposable
{
    private readonly IDisposable? _disposable;

    public AssetsWorkflowServiceScope(AssetsWorkflowService service, IDisposable? disposable)
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
