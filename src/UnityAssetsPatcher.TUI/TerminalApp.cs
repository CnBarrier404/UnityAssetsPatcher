using Microsoft.Extensions.DependencyInjection;
using UnityAssetsPatcher.TUI.Lifecycle;

namespace UnityAssetsPatcher.TUI;

public sealed class TerminalApp
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TerminalApp(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
    }

    public async Task<int> RunAsync()
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        Exception? runFailure = null;
        try
        {
            var session = scope.ServiceProvider.GetRequiredService<TerminalSession>();

            await session.RunAsync().ConfigureAwait(false);

            return 0;
        }
        catch (Exception exception)
        {
            runFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposalFailure) when (runFailure is not null)
            {
                // The menu event and scope disposal can await the same failed update task.
                if (!ReferenceEquals(runFailure, disposalFailure))
                {
                    throw new AggregateException(runFailure, disposalFailure);
                }
            }
        }
    }
}
