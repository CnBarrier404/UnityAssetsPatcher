using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.Application.Contracts;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Repository;
using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI.Hooks;

public sealed class RepositoryInitializationStartupHook : ITerminalStartupHook
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RepositoryInitializationStartupHook(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    public Task RunAsync(TerminalLifecycleContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        OperationResult<RepositoryRecoveryReport> result;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            result = InitializeRepository();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        catch (Exception)
        {
            result = new OperationFailed<RepositoryRecoveryReport>(
                new OperationError(RepositoryErrorCodes.Unsafe));
        }

        context.UIDispatcher.TryInvoke(
            () => context.Navigator.ShowRepositoryInitializationResult(result),
            cancellationToken);

        return Task.CompletedTask;
    }

    private OperationResult<RepositoryRecoveryReport> InitializeRepository()
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository>();

        return new RepositoryInitializationModule(repository).Initialize();
    }
}
