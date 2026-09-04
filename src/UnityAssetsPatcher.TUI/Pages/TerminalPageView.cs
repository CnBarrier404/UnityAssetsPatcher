using Terminal.Gui.Input;
using UnityAssetsPatcher.TUI.Navigation;

namespace UnityAssetsPatcher.TUI.Pages;

public abstract class TerminalPageView : TerminalContentView
{
    public event EventHandler<TerminalRoute>? NavigationRequested;

    internal event EventHandler? PageHeaderChanged;
    internal string PageTitle { get; private set; } = string.Empty;
    internal string? PageDescription { get; private set; }

    protected virtual bool CanReturnToMainMenu => true;

    protected TerminalPageView()
    {
        AddCommand(Command.Cancel, () =>
        {
            if (CanReturnToMainMenu)
            {
                RequestMainMenu();
            }

            return true;
        });
        KeyBindings.Add(Key.Esc, Command.Cancel);
    }

    protected void RequestNavigation(TerminalRoute route)
    {
        NavigationRequested?.Invoke(this, route);
    }

    protected void RequestMainMenu()
    {
        RequestNavigation(TerminalRoute.MainMenu);
    }

    protected void SetHeader(string title, string? description = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(title);

        description = string.IsNullOrEmpty(description) ? null : description;

        if (PageTitle == title && PageDescription == description)
        {
            return;
        }

        PageTitle = title;
        PageDescription = description;
        PageHeaderChanged?.Invoke(this, EventArgs.Empty);
    }
}
