using Terminal.Gui.App;

namespace UnityAssetsPatcher.TUI.Lifecycle;

internal sealed class TerminalUIDispatcher : ITerminalUIDispatcher
{
    private readonly IApplication _application;
    private int _isAccepting = 1;

    public TerminalUIDispatcher(IApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        _application = application;
    }

    public bool TryInvoke(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (cancellationToken.IsCancellationRequested || Volatile.Read(ref _isAccepting) == 0)
        {
            return false;
        }

        try
        {
            _application.Invoke(() =>
            {
                if (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _isAccepting) != 0)
                {
                    callback();
                }
            });

            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void StopAccepting()
    {
        Volatile.Write(ref _isAccepting, 0);
    }
}
