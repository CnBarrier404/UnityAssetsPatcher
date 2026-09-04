using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnityAssetsPatcher.Application.Operations;
using UnityAssetsPatcher.Application.Updates;

namespace UnityAssetsPatcher.TUI.Pages.MainMenu;

public sealed class MainMenuLogic : IAsyncDisposable
{
    public event EventHandler? UpdateAvailable;

    public UpdateInfo? AvailableUpdate
    {
        get
        {
            lock (_sync)
            {
                return _availableUpdate;
            }
        }
    }

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MainMenuLogic> _logger;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly Lock _sync = new();
    private Task? _updateCheckTask;
    private UpdateInfo? _availableUpdate;
    private bool _isStarted;
    private bool _isDisposed;

    public MainMenuLogic(
        IServiceScopeFactory scopeFactory,
        ILogger<MainMenuLogic>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        _scopeFactory = scopeFactory;
        _logger = logger ?? NullLogger<MainMenuLogic>.Instance;
    }

    public void StartUpdateCheck()
    {
        lock (_sync)
        {
            if (_isStarted || _isDisposed)
            {
                return;
            }

            _isStarted = true;
            _updateCheckTask = CheckForUpdateAsync(_lifetimeCancellation.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task? updateCheckTask;

        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            updateCheckTask = _updateCheckTask;
        }

        await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);

        if (updateCheckTask is not null)
        {
            await updateCheckTask.ConfigureAwait(false);
        }

        _lifetimeCancellation.Dispose();
    }

    private async Task CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var updateCheckModule = scope.ServiceProvider.GetRequiredService<UpdateCheckModule>();
            var result = await updateCheckModule.CheckForUpdateAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (result is OperationSucceeded<UpdateInfo?> { Value: { } update })
            {
                PublishAvailableUpdate(update);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Main menu update check terminated unexpectedly");
        }
    }

    private void PublishAvailableUpdate(UpdateInfo update)
    {
        lock (_sync)
        {
            if (_isDisposed || _availableUpdate is not null)
            {
                return;
            }

            _availableUpdate = update;
        }

        UpdateAvailable?.Invoke(this, EventArgs.Empty);
    }
}
