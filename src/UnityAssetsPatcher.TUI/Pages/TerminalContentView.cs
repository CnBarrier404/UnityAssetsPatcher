using Terminal.Gui.ViewBase;
using UnityAssetsPatcher.TUI.Shell;

namespace UnityAssetsPatcher.TUI.Pages;

public abstract class TerminalContentView : View, ITerminalRenderRequester
{
    public event EventHandler? RenderRequested;

    protected bool IsDisposed { get; private set; }

    protected TerminalContentView()
    {
        Disposing += (_, _) => IsDisposed = true;
    }

    protected void RequestRender()
    {
        RenderRequested?.Invoke(this, EventArgs.Empty);
    }
}
