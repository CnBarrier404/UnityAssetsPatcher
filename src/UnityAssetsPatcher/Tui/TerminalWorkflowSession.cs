using UnityAssetsPatcher.Application.Workflows;

namespace UnityAssetsPatcher.Tui;

internal sealed class TerminalWorkflowSession : IDisposable
{
    private readonly IDisposable? _disposable;

    public TerminalWorkflowSession(AssetsWorkflowService service, IDisposable? disposable)
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
