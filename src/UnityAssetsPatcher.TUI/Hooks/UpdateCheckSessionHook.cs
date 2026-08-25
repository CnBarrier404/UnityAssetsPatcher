using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;
using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI.Hooks;

public sealed class UpdateCheckSessionHook : ITerminalSessionHook
{
    private readonly UpdateCheckModule _updateCheckModule;

    public UpdateCheckSessionHook(UpdateCheckModule updateCheckModule)
    {
        ArgumentNullException.ThrowIfNull(updateCheckModule);

        _updateCheckModule = updateCheckModule;
    }

    public async Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var result = await _updateCheckModule.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);

            if (result is not OperationSucceeded<UpdateInfo?> { Value: { } update })
            {
                return;
            }

            context.UIDispatcher.TryInvoke(() => context.Navigator.ShowAvailableUpdate(update), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
